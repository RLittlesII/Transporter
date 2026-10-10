using AwesomeAssertions;
using DynamicData;
using Flurl.Http.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Integrations.OpenSky.Contracts;
using Transporter.Integrations.OpenSky.Replay;
using Transporter.Tracking;
using Transporter.Tracking.Container;
using Transporter.Tracking.Sources;

namespace Transporter.UnitTests.Integrations.OpenSky.Replay;

// RSA1010 asks for ObserveOn before every Bind, for the reason StalenessTests gives: the pipeline
// marshals for nobody and the consumer here is a test, which has no user-interface thread.
#pragma warning disable RSA1010

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
    /// B-015. Replay is one more strategy and is selected by the same decorator that selects any
    /// live source: one registration answers the seam a consumer holds, both sources answer the one
    /// the selector reads, and selecting replay is the same call selecting the live source is. There
    /// is no offline-mode flag and no replay-only selector, which is the point of the Feature for
    /// the audience and the thing most likely to be added under pressure on stage.
    /// </summary>
    [Fact]
    public void GivenBothSourcesRegistered_WhenReplayIsSelected_ThenItIsSelectedThroughTheOneDecoratorAndNothingElseSelectsIt()
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
        using var subscription = composed.GetRequiredService<ITrackerSource>().Connect().Bind(out var rows).Subscribe();

        // When
        selector.Select(selector.Entries.Single(static entry => entry.Source is IAircraftReplayTrackerSource));
        scheduler.AdvanceBy(1);

        // Then
        composed.GetServices<ITrackerSource>()
            .Should()
            .ContainSingle("one registration answers the seam a consumer holds")
            .Which.Should()
            .BeOfType<SwappingTrackerSource>("and it is the selector, not a source");
        composed.GetServices<ITrackerSourceStrategy>()
            .Should()
            .HaveCount(2, "both sources are strategies the one selector chooses between");
        rows.Select(static vehicle => vehicle.Key)
            .Should()
            .BeEquivalentTo(["a1b2c3", "d4e5f6", "070809"], "selecting replay through the decorator is what put the recording's aircraft in the fleet");
    }

    /// <summary>
    /// B-011, which `0010` carries and this item builds: a composition with no credential
    /// configured and no provider to reach starts, and the fleet fills from a recording. That is the
    /// venue with no network and the laptop with no secrets, which is the situation this Feature
    /// exists for — and the registration split § 11 row 4 decided is what makes the host start at
    /// all, since credential validation now belongs to the live transport rather than to the
    /// integration.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenNoCredentialConfiguredAndNoReachableProvider_WhenTheHostStartsAndReplayRuns_ThenTheFleetFillsAndNoRequestIsMade()
    {
        // Given
        using var http = new HttpTest();
        var scheduler = new TestScheduler();
        var recordings = new RecordedFiles().Holding(Rehearsal, ReplayRecordingCases.SpanningSixMinutes);
        var settings = ReplayComposition.Settings(aircraft: Rehearsal);
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<ISchedulerProvider>(ReplayComposition.Schedulers(scheduler));
                services.AddAircraftReplay(settings, recordings);
                services.AddFleetTracking();
            })
            .Build();

        // When
        await host.StartAsync(CancellationToken.None);
        using var subscription = host.Services.GetRequiredService<IFleetTracker>()
            .Fleet.Bind(out var rows)
            .Subscribe();
        scheduler.AdvanceBy(1);

        // Then
        rows.Select(static row => row.Vehicle.Key)
            .Should()
            .BeEquivalentTo(["a1b2c3", "d4e5f6", "070809"], "the recording filled the fleet with no credential and no network");
        http.CallLog.Should().BeEmpty("nothing in a replay composition reaches the provider, the token endpoint included");

        await host.StopAsync(CancellationToken.None);
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

    /// <summary>The recording a rehearsal left behind, named the way ADR-0004 names one.</summary>
    private const string Rehearsal = "aircraft-2026-10-04T14-32-11Z.ndjson";
}
