using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Reactive.Testing;
using Transporter.Integrations.AisStream;
using Transporter.Model;
using Transporter.Recording;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Integrations.AisStream;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Tracking;

#pragma warning disable RSA1010

public class VesselReplayPlaybackTests
{
    /// <summary>B-008: rewinding a sole vessel never clears the bound fleet, even briefly.</summary>
    /// <returns>The loop-boundary assertions.</returns>
    [Fact]
    public async Task GivenOneVesselAtBothEnds_WhenReplayRewinds_ThenNoBoundChangeRemovesIt()
    {
        var first = new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.Zero);
        var last = first.AddMinutes(11);
        var lines = Line(0, VesselPayloads.WithReportedTime(VesselPayloads.PositionReport, ProviderTime(first)))
            + Line(1, VesselPayloads.WithReportedTime(VesselPayloads.MovedPositionReport, ProviderTime(last)));
        using var recording = new MemoryStream(Encoding.UTF8.GetBytes(lines));
        using var cache = new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi);
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var clock = new ObservedClock();
        var playback = new VesselReplayPlayback(
            new RecordingPacer(recording, schedulers, NullLogger<RecordingPacer>.Instance), cache, clock, schedulers);
        var source = VesselTrackerSource.FromSubscription(static () => Disposable.Empty, cache, new VesselSnapshotMapper());
        using FleetTracker fleet = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock)
            .WithProvider(schedulers)
            .WithObservable(new BehaviorSubject<FleetSourceDescription>(VesselFleetDescription.Offered));
        var changes = new List<IChangeSet<TrackedVehicle, string>>();
        using var subscription = fleet.Fleet.Bind(out var rows).Subscribe(changes.Add);

        await playback.Next(CancellationToken.None);
        await Advance(playback, scheduler);
        rows.Should().ContainSingle(static vessel => vessel.Vehicle.Key == "366999001");
        var beforeRewind = changes.Count;

        await Advance(playback, scheduler);

        clock.Current.Should().Be(first);
        rows.Should().ContainSingle(static vessel => vessel.Vehicle.Key == "366999001");
        changes.Skip(beforeRewind).SelectMany(static batch => batch)
            .Should().NotContain(static change => change.Reason == ChangeReason.Remove);
    }

    /// <summary>B-017: a pacer completing after cancellation cannot publish its frame.</summary>
    /// <returns>The cancellation assertions.</returns>
    [Fact]
    public async Task GivenAPacerReturnsAfterCancellation_WhenNextCompletes_ThenNoStateChanges()
    {
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var cache = new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi);
        var clock = new ObservedClock();
        var pacer = new LatePacer();
        var playback = new VesselReplayPlayback(pacer, cache, clock, schedulers);
        using var cancellation = new CancellationTokenSource();
        var next = playback.Next(cancellation.Token);
        cancellation.Cancel();
        pacer.Release(VesselPayloads.PositionReport);

        Func<Task> finish = () => next;
        await finish.Should().ThrowAsync<OperationCanceledException>();
        cache.Count.Should().Be(0);
        clock.Current.Should().Be(DateTimeOffset.MinValue);
    }

    /// <summary>B-008/B-009: an untimed first frame leaves contact held until the first timed frame rebases it.</summary>
    /// <returns>The playback assertions.</returns>
    [Fact]
    public async Task GivenAnUntimedFirstFrame_WhenReplayRewinds_ThenContactWaitsForTheFirstTimedFrame()
    {
        var first = new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.Zero);
        var last = first.AddMinutes(11);
        var lines = Line(0, VesselPayloads.WithReportedTime(VesselPayloads.StaticData, null))
            + Line(1, VesselPayloads.WithReportedTime(VesselPayloads.PositionReport, ProviderTime(first)))
            + Line(2, VesselPayloads.WithReportedTime(VesselPayloads.MovedPositionReport, ProviderTime(last)));
        using var recording = new MemoryStream(Encoding.UTF8.GetBytes(lines));
        using var cache = new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi);
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var clock = new ObservedClock();
        var playback = new VesselReplayPlayback(
            new RecordingPacer(recording, schedulers, NullLogger<RecordingPacer>.Instance), cache, clock, schedulers);

        await playback.Next(CancellationToken.None);
        clock.Current.Should().Be(DateTimeOffset.MinValue);
        await Advance(playback, scheduler);
        await Advance(playback, scheduler);
        clock.Current.Should().Be(last);

        await Advance(playback, scheduler);
        clock.Current.Should().Be(last, "an untimed rewind frame cannot manufacture an observed instant");
        cache.Lookup("366999001").Value.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(last);
        await Advance(playback, scheduler);
        clock.Current.Should().Be(first);
        cache.Lookup("366999001").Value.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(first);
    }

    /// <summary>B-017: dropping the strategy subscription cancels the pending paced read.</summary>
    /// <returns>The playback assertions.</returns>
    [Fact(Timeout = 10000)]
    public async Task GivenReplayWaitingForItsNextFrame_WhenItsStrategyIsUnsubscribed_ThenPlaybackIsCancelled()
    {
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var cache = new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi);
        var pacer = new WaitingPacer();
        var playback = new VesselReplayPlayback(pacer, cache, new ObservedClock(), schedulers);
        var source = VesselTrackerSource.FromSubscription(playback.Start, cache, new VesselSnapshotMapper());
        var subscription = source.Connect().Subscribe(static _ => { });

        scheduler.AdvanceBy(1);
        await pacer.Started;
        subscription.Dispose();
        await pacer.Cancelled;

        cache.Count.Should().Be(0, "no frame arriving after the swap may enter replay's cache");
    }

    /// <summary>B-008/B-009/B-010: only EOF resets provider time; retained vessels never leave as one batch.</summary>
    /// <returns>The playback assertions.</returns>
    [Fact]
    public async Task GivenTwoPassesWithLateAndUntimedFrames_WhenReplayLoops_ThenClockAndContactsRebaseWithoutEmptyingTheFleet()
    {
        var first = new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.Zero);
        var late = first.AddSeconds(-1);
        var final = first.AddMinutes(11).AddSeconds(1);
        var bodies = new[]
        {
            VesselPayloads.WithReportedTime(VesselPayloads.PositionReport, ProviderTime(first)),
            VesselPayloads.WithReportedTime(VesselPayloads.MovedPositionReport, ProviderTime(late)),
            VesselPayloads.WithReportedTime(VesselPayloads.SecondPositionReport, ProviderTime(final)),
            VesselPayloads.WithReportedTime(VesselPayloads.StaticData, null),
        };
        var lines = string.Concat(bodies.Select(static (body, index) => Line(index, body)));
        using var recording = new MemoryStream(Encoding.UTF8.GetBytes(lines));
        using var cache = new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi);
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var clock = new ObservedClock();
        var pacer = new RecordingPacer(recording, schedulers, NullLogger<RecordingPacer>.Instance);
        var playback = new VesselReplayPlayback(pacer, cache, clock, schedulers);
        var source = VesselTrackerSource.FromSubscription(
            static () => Disposable.Empty, cache, new VesselSnapshotMapper());
        using FleetTracker fleet = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock)
            .WithProvider(schedulers)
            .WithObservable(new BehaviorSubject<FleetSourceDescription>(VesselFleetDescription.Offered));
        var changes = new List<IChangeSet<TrackedVehicle, string>>();
        using var subscription = fleet.Fleet.Bind(out var rows).Subscribe(changes.Add);

        await playback.Next(CancellationToken.None);
        clock.Current.Should().Be(first);
        cache.Lookup("366999001").Value.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(first);
        await Advance(playback, scheduler);
        clock.Current.Should().Be(first, "an older frame inside one pass cannot reset provider time");
        cache.Lookup("366999001").Value.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(first);
        await Advance(playback, scheduler);
        clock.Current.Should().Be(final);
        rows.Should().ContainSingle(static vessel => vessel.Vehicle.Key == "366999002", "the first vessel is silent beyond ten minutes");
        await Advance(playback, scheduler);
        clock.Current.Should().Be(final, "an untimed frame cannot borrow arrival time");

        var beforeRewind = changes.Count;
        await Advance(playback, scheduler);
        pacer.RewindCount.Should().Be(1);
        clock.Current.Should().Be(first, "only genuine EOF restarts observed provider time");
        cache.Lookup("366999001").Value.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(first);
        rows.Should().NotBeEmpty("the retained replay cache must not empty the fleet at the loop boundary");
        rows.Should().Contain(static vessel => vessel.Vehicle.Key == "366999001", "the first vessel must reappear on the new pass");
        changes.Skip(beforeRewind).Should().NotContain(static batch => batch.All(static change => change.Reason == ChangeReason.Remove));

        await Advance(playback, scheduler);
        clock.Current.Should().Be(first);
        cache.Lookup("366999001").Value.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(first);
        await Advance(playback, scheduler);
        clock.Current.Should().Be(final);
        rows.Should().ContainSingle(static vessel => vessel.Vehicle.Key == "366999002", "silence removal recurs in the second pass");
        changes.SelectMany(static batch => batch)
            .Count(static change => change.Reason == ChangeReason.Remove && change.Key == "366999001")
            .Should().Be(2, "the first vessel expires once on each pass");
    }

    private static async Task Advance(VesselReplayPlayback playback, TestScheduler scheduler)
    {
        var next = playback.Next(CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        await next;
    }

    private static string Line(int seconds, string body)
    {
        var arrival = new DateTimeOffset(2026, 10, 21, 16, 0, 0, TimeSpan.Zero).AddSeconds(seconds)
            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        return $"{{\"receivedAt\":\"{arrival}\",\"body\":{body}}}\n";
    }

    private static string ProviderTime(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss +0000 UTC", CultureInfo.InvariantCulture);

    private sealed class WaitingPacer : IRecordingPacer
    {
        public Task Started => _started.Task;

        public Task Cancelled => _cancelled.Task;

        /// <inheritdoc/>
        public long RewindCount => 0;

        /// <inheritdoc/>
        public Task<string> Next(CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            cancellationToken.Register(() =>
            {
                _cancelled.TrySetResult();
                _pending.TrySetCanceled(cancellationToken);
            });
            return _pending.Task;
        }

        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<string> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class LatePacer : IRecordingPacer
    {
        /// <inheritdoc/>
        public long RewindCount => 0;

        /// <inheritdoc/>
        public Task<string> Next(CancellationToken cancellationToken) => _pending.Task;

        public void Release(string body) => _pending.SetResult(body);

        private readonly TaskCompletionSource<string> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
