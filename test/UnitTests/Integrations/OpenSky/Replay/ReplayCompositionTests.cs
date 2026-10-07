using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using Transponder.Integrations.OpenSky;
using Transponder.Integrations.OpenSky.Container;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Integrations.OpenSky.Replay;
using Transponder.Tracking.Sources;

namespace Transponder.UnitTests.Integrations.OpenSky.Replay;

public class ReplayCompositionTests
{
    /// <summary>
    /// B-022, the half a test can prove. Replay substitutes at the provider's own contract and
    /// brings nothing of its own above it: what the chain resolves is the live
    /// <see cref="AircraftSnapshotClient"/> class over <see cref="ReplayOpenSkyApi"/>. The claim's
    /// other half — that the replay namespaces declare no client, cache, converter or projection —
    /// is a review, because it is about types nobody may introduce.
    /// </summary>
    [Fact]
    public void GivenTheReplayChainIsRegistered_WhenItIsResolved_ThenTheClientIsTheLiveClassOverTheReplayContract()
    {
        // Given
        var scheduler = new TestScheduler();
        using var recording = ReplayComposition.Recorded(ReplayRecordingCases.TwoPollsFifteenSecondsApart);

        // When
        using var composed = ReplayComposition.Over(recording, scheduler);

        // Then
        composed.GetRequiredKeyedService<IAircraftSnapshotClient>(OpenSkyReplayRegistration.Chain)
            .Should()
            .BeOfType<AircraftSnapshotClient>("the client is the live class, not a replay client");
        composed.GetRequiredKeyedService<IOpenSkyApi>(OpenSkyReplayRegistration.Chain)
            .Should()
            .BeOfType<ReplayOpenSkyApi>("and what it was handed is the contract over a recording");
        composed.GetRequiredService<IAircraftReplayTrackerSource>()
            .Should()
            .BeOfType<AircraftReplayTrackerSource>("the chain reaches the seam under a strategy of its own");
    }

    /// <summary>
    /// B-022's "the same client class, not a second one" means a second instance: the replay chain
    /// holds its own cache and its own options, because a shared cache would leave the outgoing
    /// feed's aircraft in the collection under the incoming one (B-016) and a shared interval would
    /// space every replayed payload by the recorded gap plus fifteen seconds (B-007).
    /// </summary>
    [Fact]
    public void GivenBothChainsAreRegistered_WhenEachIsResolved_ThenTheReplayChainHasItsOwnCacheAndAnIntervalOfZero()
    {
        // Given
        var scheduler = new TestScheduler();
        using var recording = ReplayComposition.Recorded(ReplayRecordingCases.TwoPollsFifteenSecondsApart);
        using var composed = ReplayComposition.Over(recording, scheduler);

        // When
        var replayed = composed.GetRequiredKeyedService<SourceCache<AircraftSnapshot, string>>(OpenSkyReplayRegistration.Chain);
        var options = composed.GetRequiredKeyedService<IOptions<OpenSkyOptions>>(OpenSkyReplayRegistration.Chain);

        // Then
        replayed.Should().NotBeSameAs(
            composed.GetRequiredService<SourceCache<AircraftSnapshot, string>>(),
            "one cache per chain, so a swap cannot show two feeds at once");
        options.Value.PollInterval.Should().Be(TimeSpan.Zero, "the recorded spacing is the whole cadence");
        composed.GetRequiredService<IOptions<OpenSkyOptions>>()
            .Value.PollInterval.Should()
            .Be(OpenSkyOptions.DefaultPollInterval, "and the live chain's interval is untouched by that");
    }
}
