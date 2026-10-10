using System.Reactive.Linq;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Tracking;
using Transporter.Tracking.Container;
using Transporter.Tracking.Fleet;
using Transporter.Tracking.Sources;

namespace Transporter.UnitTests.Integrations.OpenSky.Replay;

// RSA1010 asks for ObserveOn before every Bind, for the reason StalenessTests gives: the pipeline
// marshals for nobody and the consumer here is a test, which has no user-interface thread.
#pragma warning disable RSA1010

public class ReplaySwapTests
{
    /// <summary>
    /// B-016. Nothing about the swap reaches the seam as anything but data: no marker arrives
    /// alongside the changesets, the stream does not complete, and it does not error. A consumer
    /// cannot tell from the seam that it is now reading a recording, which is both what makes replay
    /// honest as a demonstration and what keeps every stage above it from growing a branch per
    /// source.
    /// </summary>
    [Fact]
    public void GivenTheFleetIsObserved_WhenTheSourceSwapsToReplay_ThenNoMarkerCompletionOrErrorReachesTheSeam()
    {
        // Given
        var scheduler = new TestScheduler();
        var recordings = new RecordedFiles().Holding(Rehearsal, ReplayRecordingCases.SpanningSixMinutes);
        var settings = ReplayComposition.Settings(aircraft: Rehearsal);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISchedulerProvider>(ReplayComposition.Schedulers(scheduler));
        services.AddOpenSky(settings);
        services.AddAircraftReplay(settings, recordings);
        services.AddFleetTracking();
        services.AddSingleton<IAircraftSnapshotClient>(static provider =>
            new LiveSnapshots(provider.GetRequiredService<SourceCache<AircraftSnapshot, string>>()));
        using var composed = services.BuildServiceProvider();
        var selector = composed.GetRequiredService<SwappingTrackerSource>();
        var reasons = new List<ChangeReason>();
        var completed = false;
        Exception? failed = null;
        using var subscription = composed.GetRequiredService<ITrackerSource>()
            .Connect()
            .Subscribe(
                changes => reasons.AddRange(changes.Select(static change => change.Reason)),
                error => failed = error,
                () => completed = true);

        // When
        selector.Select(selector.Entries.Single(static entry => entry.Source is IAircraftReplayTrackerSource));
        scheduler.AdvanceBy(1);

        // Then
        completed.Should().BeFalse("a swap is not the end of the fleet");
        failed.Should().BeNull("and it is not a failure of it either");
        reasons.Should().NotBeEmpty("the recording's aircraft arrived");
        reasons.Should().OnlyContain(
            static reason => reason == ChangeReason.Add || reason == ChangeReason.Update
                || reason == ChangeReason.Remove || reason == ChangeReason.Refresh,
            "every change is an ordinary one about a vehicle, so nothing in the stream says the source changed");
    }

    /// <summary>
    /// B-017. The outgoing source is stopped rather than left running: a swapped-out poller spends
    /// no more credits, which on a provider that rate-limits by credit is the difference between a
    /// talk that can swap back and one that cannot.
    /// </summary>
    [Fact]
    public void GivenALivePollerIsRunning_WhenReplayBecomesTheLiveSource_ThenTheOutgoingPollStops()
    {
        // Given
        var scheduler = new TestScheduler();
        var recordings = new RecordedFiles().Holding(Rehearsal, ReplayRecordingCases.SpanningSixMinutes);
        var settings = ReplayComposition.Settings(aircraft: Rehearsal);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISchedulerProvider>(ReplayComposition.Schedulers(scheduler));
        services.AddOpenSky(settings);
        services.AddAircraftReplay(settings, recordings);
        services.AddFleetTracking();
        services.AddSingleton<IAircraftSnapshotClient>(static provider =>
            new LiveSnapshots(provider.GetRequiredService<SourceCache<AircraftSnapshot, string>>()));
        using var composed = services.BuildServiceProvider();
        var selector = composed.GetRequiredService<SwappingTrackerSource>();
        var live = (LiveSnapshots) composed.GetRequiredService<IAircraftSnapshotClient>();
        using var subscription = composed.GetRequiredService<ITrackerSource>().Connect().Subscribe();
        var whileLive = (live.Polls, live.Stops);

        // When
        selector.Select(selector.Entries.Single(static entry => entry.Source is IAircraftReplayTrackerSource));
        scheduler.AdvanceBy(1);

        // Then
        whileLive.Should().Be((1, 0), "subscribing the seam started the live poll and nothing had stopped it");
        live.Stops.Should().Be(1, "the swap disposes the outgoing subscription, which is what owns the poll");
        live.Polls.Should().Be(1, "and nothing started it again");
    }

    /// <summary>
    /// B-018. The swap happens below everything the audience is looking at: a filter, a sort and a
    /// grouping stay in place across it, the bound collection is never rebuilt, and the predicate
    /// applied to the live feed still excludes the recording's aircraft that cannot satisfy it. A
    /// pipeline rebuilt per source is the failure this claim exists for — it works on the first
    /// swap, drops the audience's filter, and is indistinguishable from a bug in the grid.
    /// </summary>
    [Fact]
    public void GivenAFilterASortAndAGroupInPlace_WhenTheSourceSwapsToReplay_ThenNoneIsRebuiltAndTheBindingSurvives()
    {
        // Given
        var scheduler = new TestScheduler();
        var recordings = new RecordedFiles().Holding(Rehearsal, ReplayRecordingCases.SpanningSixMinutes);
        var settings = ReplayComposition.Settings(aircraft: Rehearsal);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISchedulerProvider>(ReplayComposition.Schedulers(scheduler));
        services.AddOpenSky(settings);
        services.AddAircraftReplay(settings, recordings);
        services.AddFleetTracking();
        services.AddSingleton<IAircraftSnapshotClient>(static provider =>
            new LiveSnapshots(provider.GetRequiredService<SourceCache<AircraftSnapshot, string>>()));
        using var composed = services.BuildServiceProvider();
        var selector = composed.GetRequiredService<SwappingTrackerSource>();
        var live = (LiveSnapshots) composed.GetRequiredService<IAircraftSnapshotClient>();
        var tracker = composed.GetRequiredService<IFleetTracker>();
        tracker.Filter(static vehicle => vehicle.Position.IsSome);
        var grouped = new List<FleetGroup>();
        using var groups = tracker.Groups.Subscribe(changes => grouped.AddRange(changes.Select(static change => change.Current)));
        using var bound = tracker.Fleet.SortAndBind(out var rows, tracker.Order).Subscribe();
        IComparer<TrackedVehicle>? order = null;
        using var sorting = tracker.Order.Subscribe(comparer => order = comparer);
        var sameCollection = rows;
        var whileLive = rows.Select(static row => row.Vehicle.Key).ToArray();

        // When
        selector.Select(selector.Entries.Single(static entry => entry.Source is IAircraftReplayTrackerSource));
        scheduler.AdvanceBy(1);

        // Then
        whileLive.Should().Equal([LiveSnapshots.Key], "the live aircraft reports a position, so the filter admitted it");
        rows.Should().BeSameAs(sameCollection, "the collection a view binds to is never rebuilt by a swap");
        rows.Select(static row => row.Vehicle.Key)
            .Should()
            .BeEquivalentTo(["a1b2c3", "070809"], "the recording's positioned aircraft arrived, and the filter still excludes the one reporting no position");
        rows.Should().BeInAscendingOrder(order!, "the sort in place still orders what arrives, by the comparer it was already using");
        grouped.Select(static group => group.Key)
            .Should()
            .Contain(["Testland", "Exampleland"], "the grouping re-formed over the incoming feed rather than being rebuilt");
        live.Polls.Should().Be(1, "nothing below the seam was rebuilt, so the outgoing source was never re-subscribed");
    }

    /// <summary>The recording a rehearsal left behind, named the way ADR-0004 names one.</summary>
    private const string Rehearsal = "aircraft-2026-10-04T14-32-11Z.ndjson";
}
