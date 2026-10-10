using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit.Xunit2;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Airframe;
using Transporter.Container;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.Tracking.Container;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Integrations.OpenSky.Replay;

namespace Transporter.UnitTests.Tracking;

// RSA1010 asks for ObserveOn before every Bind; the consumer here is a test, which has no
// user-interface thread to marshal to (ReplaySwapTests gives the same reason).
#pragma warning disable RSA1010

public class SourceSwapActorTests : TestKit
{
    /// <summary>
    /// `fleet-dashboard` B-016's half of the swap: the control tells, and this is what is told. One
    /// message, and the live source is the one it named — through the decorator, so a consumer
    /// already subscribed keeps the subscription it had.
    /// </summary>
    [Fact]
    public void GivenASubscribedConsumer_WhenTheActorIsToldToSwap_ThenTheLiveSourceIsTheOneNamed()
    {
        // Given
        var strategies = new SwappingTrackerSourceFixture();
        SwappingTrackerSource source = strategies;
        var sut = Sys.ActorOf(Starts(source));
        var observed = new List<IChangeSet<TransportVehicle, string>>();
        using var subscription = source.Connect().Subscribe(observed.Add);
        strategies.Aircraft.Report("a1b2c3");
        var second = TargetsOf(sut)[1];

        // When
        sut.Tell(SwapSource.To(second));
        AwaitAssert(() => strategies.Aircraft.Stopped.Should().Be(1));
        strategies.Vessels.Report("imo9074729");

        // Then
        observed.Should().HaveCount(3, "the add, the outgoing fleet leaving, and the incoming add");
        strategies.Vessels.Started.Should().Be(1);
    }

    /// <summary>
    /// Told, never asked: the actor answers nothing to a swap, so no caller can learn which source is
    /// live from it any more than from the decorator it tells (B-038).
    /// </summary>
    [Fact]
    public void GivenTheActor_WhenItIsToldToSwap_ThenItRepliesWithNothing()
    {
        // Given
        var sut = Sys.ActorOf(Starts(new SwappingTrackerSourceFixture()));
        var second = TargetsOf(sut)[1];

        // When
        sut.Tell(SwapSource.To(second), TestActor);

        // Then
        ExpectNoMsg(TimeSpan.FromMilliseconds(250));
    }

    /// <summary>
    /// B-057. The targets are registration's: one per strategy the application registers, in the
    /// order it registers them, each carrying the name its registration gave it and a handle — so a
    /// picker offers them and names no strategy or strategy type.
    /// </summary>
    [Fact]
    public void GivenTheApplicationsRegistrations_WhenTheTargetsAreAsked_ThenOnePerStrategyArrivesInOrderWithItsName()
    {
        // Given
        using var composed = BothSources(new TestScheduler());
        var sut = Sys.ActorOf(Starts(composed.GetRequiredService<SwappingTrackerSource>()));

        // When
        var targets = TargetsOf(sut);

        // Then
        targets.Should().Equal(
            [
                new SwapTarget(OpenSkyRegistration.LiveAircraft, 0),
                new SwapTarget(OpenSkyReplayRegistration.RecordedAircraft, 1),
            ],
            "the live source is registered first and the recording beside it");
        composed.GetServices<ITrackerSourceStrategy>().Should().HaveCount(targets.Count, "every registered strategy is offered, and nothing else is");
    }

    /// <summary>
    /// B-057's handle carries back: telling the actor to swap to the target it answered makes that
    /// strategy live, over the application's own registrations rather than doubles.
    /// </summary>
    [Fact]
    public void GivenTheApplicationsTargets_WhenTheActorIsToldToSwapToTheSecond_ThenTheRecordingIsLive()
    {
        // Given
        var scheduler = new TestScheduler();
        using var composed = BothSources(scheduler);
        var live = (LiveSnapshots) composed.GetRequiredService<IAircraftSnapshotClient>();
        var sut = Sys.ActorOf(Starts(composed.GetRequiredService<SwappingTrackerSource>()));
        using var subscription = composed.GetRequiredService<ITrackerSource>().Connect().Bind(out var rows).Subscribe();
        var recorded = TargetsOf(sut).Single(static target => target.Name == OpenSkyReplayRegistration.RecordedAircraft);

        // When
        sut.Tell(SwapSource.To(recorded));
        AwaitAssert(() => live.Stops.Should().Be(1, "the swap disposes the live poll"));
        scheduler.AdvanceBy(1);

        // Then
        rows.Select(static vehicle => vehicle.Key)
            .Should()
            .BeEquivalentTo(["a1b2c3", "d4e5f6", "070809"], "the recording's aircraft are what the seam now carries");
    }

    /// <summary>
    /// B-057 with `replay-source` B-024: the application's own composition, run with no recording
    /// named, registers no replay strategy — so the actor offers the live source alone, and a picker
    /// cannot offer a recording nobody chose.
    /// </summary>
    [Fact]
    public void GivenTheApplicationsCompositionNamingNoRecording_WhenTheTargetsAreAsked_ThenOnlyTheLiveSourceIsOffered()
    {
        // Given
        using var host = new HostBuilder()
            .ConfigureServices(static services => services
                .AddTransporter(ReplayComposition.Settings(), new TestScheduler())
                .AddAkka("transporter", static _ => { }))
            .Build();
        var sut = Sys.ActorOf(Starts(host.Services.GetRequiredService<SwappingTrackerSource>()));

        // When
        var targets = TargetsOf(sut);

        // Then
        targets.Should().Equal(
            [new SwapTarget(OpenSkyRegistration.LiveAircraft, 0)],
            "a run that names no recording registers no replay source to offer");
    }

    /// <summary>What the system starts the actor from.</summary>
    /// <param name="source">The decorator the actor tells, from <see cref="SwappingTrackerSourceFixture"/>.</param>
    /// <returns>The <see cref="Props"/>.</returns>
    /// <remarks>
    /// Not an <c>AutoFixture</c>: Akka reads this expression and requires a <c>new</c> of the actor,
    /// so a fixture's implicit conversion to an instance is one it rejects. The arrangement that
    /// varies is the decorator's, and that is a generated fixture.
    /// </remarks>
    private static Props Starts(SwappingTrackerSource source) =>
        Props.Create(() => new SourceSwapActor(source));

    /// <summary>
    /// The live source and the aircraft replay, registered the way the application registers them,
    /// with the live poll replaced so nothing reaches the provider.
    /// </summary>
    /// <param name="scheduler">The scheduler every wait in the chain is scheduled on.</param>
    /// <returns>The provider, which the caller disposes.</returns>
    private static ServiceProvider BothSources(TestScheduler scheduler)
    {
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

        return services.BuildServiceProvider();
    }

    /// <summary>Asks the actor for the targets, the way a view model does.</summary>
    /// <param name="sut">The actor.</param>
    /// <returns>The targets it answered.</returns>
    private IReadOnlyList<SwapTarget> TargetsOf(IActorRef sut)
    {
        sut.Tell(GetSwapTargets.Instance, TestActor);

        return ExpectMsg<SwapTargets>().Targets;
    }

    /// <summary>The recording a rehearsal left behind, named the way ADR-0004 names one.</summary>
    private const string Rehearsal = "aircraft-2026-10-04T14-32-11Z.ndjson";
}
