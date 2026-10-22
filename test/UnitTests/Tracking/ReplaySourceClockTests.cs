using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using DynamicData;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.AisStream;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Contracts;
using Transporter.Integrations.OpenSky.Replay;
using Transporter.Model;
using Transporter.Recording;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Integrations.AisStream;
using Transporter.UnitTests.Integrations.AisStream.Fixtures;
using Transporter.UnitTests.Integrations.OpenSky;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Tracking;

#pragma warning disable RSA1010

public class ReplaySourceClockTests
{
    /// <summary>B-009/B-010: retained aircraft regain their own time before a paced response can arrive.</summary>
    /// <returns>The source activation assertions.</returns>
    [Fact]
    public async Task GivenFreshAircraftAndLaterVesselTime_WhenAircraftReplayIsReselected_ThenItsClockAndFreshnessReturnBeforeTheNextPayload()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        ObservedClock clock = new ObservedClockFixture();
        using var aircraftCache = new SourceCache<AircraftSnapshot, string>(static snapshot => snapshot.Icao24);
        using var vesselCache = new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi);
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var aircraftPacer = Substitute.For<IRecordingPacer>();
        aircraftPacer.Next(Arg.Any<CancellationToken>()).Returns(Task.FromResult((string) OpenSkyPayloads.ThreeRows), pending.Task);
        ReplayOpenSkyApi api = new ReplayClockApiFixture().WithPacer(aircraftPacer);
        AircraftSnapshotClient client = new AircraftSnapshotClientFixture().WithApi(api).WithCache(aircraftCache)
            .WithWriter(clock).WithProvider(schedulers);
        AircraftTrackerSource aircraft = new AircraftTrackerSourceFixture().WithClient(client).WithSnapshots(aircraftCache);
        var later = OpenSkyPayloads.ReportedInstant.AddDays(2);
        var vesselPacer = Substitute.For<IRecordingPacer>();
        vesselPacer.Next(Arg.Any<CancellationToken>()).Returns(Task.FromResult(VesselPayloads.WithReportedTime(
            VesselPayloads.PositionReport, later.ToString("yyyy-MM-dd HH:mm:ss +0000 UTC", CultureInfo.InvariantCulture))));
        VesselReplayPlayback playback = new ReplayClockPlaybackFixture().WithPacer(vesselPacer).WithSnapshots(vesselCache)
            .WithWriter(clock).WithProvider(schedulers);
        VesselSnapshotMapper mapper = new VesselSnapshotMapperFixture();
        var vessels = VesselTrackerSource.FromSubscription(static () => Disposable.Empty, vesselCache, mapper);
        var aircraftEntry = new TrackerSourceEntry(aircraft, "Aircraft replay", new NoPollStatus(), AircraftFleetDescription.Offered);
        var vesselEntry = new TrackerSourceEntry(vessels, "Vessel replay", new NoPollStatus(), VesselFleetDescription.Offered);
        SwappingTrackerSource selector = new SwappingTrackerSourceFixture().WithEnumerable([aircraftEntry, vesselEntry]);
        await client.Fetch(CancellationToken.None);
        using FleetTracker fleet = new FleetTrackerFixture().WithSource(selector).WithClock(clock).WithTicks(clock)
            .WithProvider(schedulers).WithObservable(selector.Description);
        using var subscription = fleet.Fleet.Bind(out var rows).Subscribe();
        rows.Should().OnlyContain(static row => !row.IsStale);
        selector.Select(vesselEntry);
        await playback.Next(CancellationToken.None);
        clock.Current.Should().Be(later);

        // When
        selector.Select(aircraftEntry);

        // Then
        using var assertions = new AssertionScope();
        clock.Current.Should().Be(OpenSkyPayloads.ReportedInstant, "activation restores the aircraft time before any next payload");
        rows.Should().HaveCount(3).And.OnlyContain(
            static row => !row.IsStale,
            "the outgoing vessel day must not falsely age retained aircraft");
        pending.Task.IsCompleted.Should().BeFalse("selection does not bypass recorded spacing");
        _ = aircraftPacer.Received(1).Next(Arg.Any<CancellationToken>());
    }

    /// <summary>B-009/B-010: retained vessels are not removed against the outgoing source's day.</summary>
    /// <returns>The source activation assertions.</returns>
    [Fact]
    public async Task GivenRetainedVesselsAndLaterSourceTime_WhenVesselReplayIsReselected_ThenItsClockAndFleetReturnBeforeTheNextFrame()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        ObservedClock clock = new ObservedClockFixture();
        using var cache = new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi);
        var instant = new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero);
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pacer = Substitute.For<IRecordingPacer>();
        pacer.Next(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(VesselPayloads.WithReportedTime(
                VesselPayloads.PositionReport,
                instant.ToString("yyyy-MM-dd HH:mm:ss +0000 UTC", CultureInfo.InvariantCulture))),
            pending.Task);
        VesselReplayPlayback playback = new ReplayClockPlaybackFixture().WithPacer(pacer).WithSnapshots(cache)
            .WithWriter(clock).WithProvider(schedulers);
        VesselSnapshotMapper mapper = new VesselSnapshotMapperFixture();
        var vessels = VesselTrackerSource.FromSubscription(playback.Start, cache, mapper);
        var other = Substitute.For<ITrackerSourceStrategy>();
        other.Connect().Returns(Observable.Defer(() =>
        {
            ((IObservedClockWriter) clock).Observe(instant.AddDays(2));
            return Observable.Never<IChangeSet<TransportVehicle, string>>();
        }));
        var vesselEntry = new TrackerSourceEntry(vessels, "Vessel replay", new NoPollStatus(), VesselFleetDescription.Offered);
        var otherEntry = new TrackerSourceEntry(other, "Another source", new NoPollStatus(), AircraftFleetDescription.Offered);
        SwappingTrackerSource selector = new SwappingTrackerSourceFixture().WithEnumerable([vesselEntry, otherEntry]);
        await playback.Next(CancellationToken.None);
        using FleetTracker fleet = new FleetTrackerFixture().WithSource(selector).WithClock(clock).WithTicks(clock)
            .WithProvider(schedulers).WithObservable(selector.Description);
        using var subscription = fleet.Fleet.Bind(out var rows).Subscribe();
        rows.Should().ContainSingle();
        selector.Select(otherEntry);
        clock.Current.Should().Be(instant.AddDays(2));

        // When
        selector.Select(vesselEntry);

        // Then
        using var assertions = new AssertionScope();
        clock.Current.Should().Be(instant, "activation restores the vessel time before any next frame");
        rows.Should().ContainSingle(
            static row => row.Vehicle.Key == "366999001",
            "the outgoing source day must not remove retained vessels");
        pending.Task.IsCompleted.Should().BeFalse("selection does not bypass recorded spacing");
        _ = pacer.Received(1).Next(Arg.Any<CancellationToken>());
    }

    /// <summary>B-017: a disposed aircraft request cannot overwrite the active source's clock or cache.</summary>
    /// <returns>The late response assertions.</returns>
    [Fact]
    public async Task GivenAnAircraftReplyHeldAcrossCancellation_WhenItCompletes_ThenTheActiveClockAndOutgoingCacheStayUnchanged()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        ObservedClock clock = new ObservedClockFixture();
        var activeInstant = OpenSkyPayloads.ReportedInstant.AddDays(2);
        ((IObservedClockWriter) clock).Observe(activeInstant);
        using var cache = new SourceCache<AircraftSnapshot, string>(static snapshot => snapshot.Icao24);
        var pending = new TaskCompletionSource<OpenSkyStatesResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = Substitute.For<IOpenSkyApi>();
        api.GetStates(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>()).Returns(pending.Task);
        AircraftSnapshotClient client = new AircraftSnapshotClientFixture().WithApi(api).WithCache(cache)
            .WithWriter(clock).WithProvider(schedulers);
        using var cancellation = new CancellationTokenSource();
        var fetch = client.Fetch(cancellation.Token);
        cancellation.Cancel();

        // When
        pending.SetResult(OpenSkyPayloads.ThreeRows.Response);

        // Then
        Func<Task> finish = async () => await fetch;
        using var assertions = new AssertionScope();
        await finish.Should().ThrowAsync<OperationCanceledException>();
        cache.Count.Should().Be(0, "the disposed source cannot accept its late reply");
        clock.Current.Should().Be(activeInstant, "the disposed source cannot rewind the active source's clock");
    }

    /// <summary>B-009/B-010: the live vessel writer restores retained contact time before reconnecting.</summary>
    /// <returns>The source reactivation assertions.</returns>
    [Fact]
    public async Task GivenLiveVesselsAndLaterSourceTime_WhenVesselsAreReselected_ThenRetainedRowsReturnBeforeTheSocketOpens()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        ObservedClock clock = new ObservedClockFixture();
        using var cache = new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi);
        var instant = new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero);
        var events = new List<string>();
        var first = new ScriptedVesselSocket("first", events);
        var second = new ScriptedVesselSocket("second", events);
        var sockets = new Queue<IVesselSocket>([first, second]);
        VesselClient client = new VesselClientFixture().WithSockets(sockets.Dequeue).WithVessels(cache)
            .WithWriter((IObservedClockWriter) clock).WithProvider(schedulers);
        VesselTrackerSource vessels = new VesselTrackerSourceFixture().WithClient(client).WithSnapshots(cache);
        var other = Substitute.For<ITrackerSourceStrategy>();
        other.Connect().Returns(Observable.Defer(() =>
        {
            ((IObservedClockWriter) clock).Observe(instant.AddDays(2));
            return Observable.Never<IChangeSet<TransportVehicle, string>>();
        }));
        var vesselEntry = new TrackerSourceEntry(vessels, "Vessels", new NoPollStatus(), VesselFleetDescription.Offered);
        var otherEntry = new TrackerSourceEntry(other, "Another source", new NoPollStatus(), AircraftFleetDescription.Offered);
        SwappingTrackerSource selector = new SwappingTrackerSourceFixture().WithEnumerable([vesselEntry, otherEntry]);
        using FleetTracker fleet = new FleetTrackerFixture().WithSource(selector).WithClock(clock).WithTicks(clock)
            .WithProvider(schedulers).WithObservable(selector.Description);
        using var subscription = fleet.Fleet.Bind(out var rows).Subscribe();
        await Task.Run(() => scheduler.AdvanceBy(1));
        await Task.Run(() => first.Receive(VesselPayloads.WithReportedTime(
            VesselPayloads.PositionReport, instant.ToString("yyyy-MM-dd HH:mm:ss +0000 UTC", CultureInfo.InvariantCulture))));
        rows.Should().ContainSingle();
        clock.Current.Should().Be(instant);
        selector.Select(otherEntry);
        await first.Closed;
        clock.Current.Should().Be(instant.AddDays(2));

        // When
        selector.Select(vesselEntry);

        // Then
        using var assertions = new AssertionScope();
        clock.Current.Should().Be(instant, "the writer restores its own time synchronously on activation");
        rows.Should().ContainSingle(
            static row => row.Vehicle.Key == "366999001",
            "a retained vessel must not vanish while its next connection is scheduled");
        second.IsOpen.Should().BeFalse("restoration happens before opening a socket or receiving another frame");
        second.Reads.Should().Be(0);
    }
}

/// <summary>Builds the real aircraft replay contract for clock handoff tests.</summary>
[AutoFixture(typeof(ReplayOpenSkyApi))]
internal partial class ReplayClockApiFixture;

/// <summary>Builds the real vessel playback for clock handoff tests.</summary>
[AutoFixture(typeof(VesselReplayPlayback))]
internal partial class ReplayClockPlaybackFixture;
