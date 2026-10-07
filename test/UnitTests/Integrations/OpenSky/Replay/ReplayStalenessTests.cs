using System.Reactive.Disposables;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Transponder.Integrations.OpenSky;
using Transponder.Integrations.OpenSky.Container;
using Transponder.Tracking;
using Transponder.Tracking.Sources;
using Transponder.UnitTests.Tracking;

namespace Transponder.UnitTests.Integrations.OpenSky.Replay;

// RSA1010 asks for ObserveOn before every Bind. This binds without one for the reason
// StalenessTests gives: the pipeline marshals for nobody and the consumer here is a test, which
// has no user-interface thread.
#pragma warning disable RSA1010

public class ReplayStalenessTests
{
    /// <summary>
    /// B-010. Staleness under replay runs on the recording's time base: the aircraft reports once,
    /// and the recording's second payload reports an instant six minutes later, so the fleet ages
    /// exactly where it aged live — in microseconds of wall clock, with no ambient clock anywhere
    /// in the chain. A chain measuring against the arrival instants would have the aircraft fifteen
    /// seconds silent and would never mark it.
    /// <para>
    /// The payloads are fetched rather than polled: the poll loop's own waits are what
    /// <see cref="ReplayCadenceTests"/> is about, and a test that asserts on a loop running past it
    /// asserts on when a thread pool got around to the continuation. The strategy is handed a
    /// client that polls nothing for the same reason, and the cache, the projection and the tracker
    /// below it are the real ones.
    /// </para>
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAnAircraftLastReportedSixMinutesBeforeTheRecordingEnds_WhenTheRecordingIsReplayed_ThenItIsMarkedStaleAtThatPointAndKept()
    {
        // Given
        var scheduler = new TestScheduler();
        using var recording = ReplayComposition.Recorded(ReplayRecordingCases.SilentSixMinutesIn);
        using var composed = ReplayComposition.Over(recording, scheduler);
        var clock = composed.GetRequiredService<ObservedClock>();
        var polls = Substitute.For<IAircraftSnapshotClient>();
        polls.Poll().Returns(Disposable.Empty);
        var strategy = new AircraftTrackerSource(
            polls,
            composed.GetRequiredKeyedService<SourceCache<AircraftSnapshot, string>>(OpenSkyReplayRegistration.Chain),
            composed.GetRequiredService<AircraftSnapshotMapper>());
        FleetTracker tracker = new FleetTrackerFixture().WithSource(strategy).WithClock(clock).WithTicks(clock);
        using var subscription = tracker.Fleet.Bind(out var rows).Subscribe();
        var replay = (AircraftSnapshotClient) composed.GetRequiredKeyedService<IAircraftSnapshotClient>(OpenSkyReplayRegistration.Chain);

        // When
        await replay.Fetch(CancellationToken.None);
        var atTheFirstPayload = rows.Single().IsStale;
        var second = replay.Fetch(CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(15).Ticks);
        await second;

        // Then
        atTheFirstPayload.Should().BeFalse("the aircraft was last heard from at the instant the first payload reports");
        rows.Should().ContainSingle().Which.IsStale.Should().BeTrue("six minutes of the recording's own time is past the tracker's five");
        rows.Single().Vehicle.Key.Should().Be("a1b2c3", "it is marked, not removed");
    }
}
