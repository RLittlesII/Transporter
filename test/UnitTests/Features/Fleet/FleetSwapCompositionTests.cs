using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit.Xunit2;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Airframe;
using Transporter.Container;
using Transporter.Features.Fleet.ViewModels;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Messages;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Integrations.OpenSky.Replay;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Features.Fleet;

public class FleetSwapCompositionTests : TestKit
{
    /// <summary>B-016, B-020 and B-040, with aircraft-source B-041: the composed view model reaches both strategies through the actor.</summary>
    [Fact]
    public void GivenTheApplicationsGraph_WhenThePickerSwapsToReplayAndBack_ThenTheSameTrackerAndCollectionRemain()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenSky:BaseUrl"] = "https://synthetic.invalid",
            ["OpenSky:Box:LatitudeMinimum"] = "28.8",
            ["OpenSky:Box:LongitudeMinimum"] = "-96.0",
            ["OpenSky:Box:LatitudeMaximum"] = "30.4",
            ["OpenSky:Box:LongitudeMaximum"] = "-94.2",
        }).Build();
        using var recording = ReplayComposition.Recorded(ReplayRecordingCases.SpanningSixMinutes);
        using var host = new HostBuilder().ConfigureServices(services => services
                .AddTransporter(settings, scheduler)
                .AddSingleton<ISchedulerProvider>(schedulers)
                .AddSingleton<IAircraftSnapshotClient>(static provider =>
                    new LiveSnapshots(provider.GetRequiredService<SourceCache<AircraftSnapshot, string>>()))
                .AddAircraftReplay(recording)
                .AddAkka("selector-composition", static _ => { }))
            .Build();
        var source = host.Services.GetRequiredService<SwappingTrackerSource>();
        var actor = ActorOfAsTestActorRef<SourceSwapActor>(Props.Create(() => new SourceSwapActor(source)));
        host.Services.GetRequiredService<IActorRegistry>().Register<SwapSource>(actor);
        using var sut = host.Services.GetRequiredService<FleetViewModel>();
        var tracker = host.Services.GetRequiredService<IFleetTracker>();
        var collection = sut.Fleet;
        var live = (LiveSnapshots) host.Services.GetRequiredService<IAircraftSnapshotClient>();
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);
        sut.Targets.Should().Equal(OpenSkyRegistration.LiveAircraft, OpenSkyReplayRegistration.RecordedAircraft);
        live.Polls.Should().Be(1);

        // When
        sut.SelectedTarget = 1;
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);
        live.Stops.Should().Be(1, "the actor switched the decorator rather than a second view-model-owned collection");
        sut.SelectedTarget = 0;
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);

        // Then
        live.Polls.Should().Be(2);
        sut.Fleet.Should().BeSameAs(collection);
        host.Services.GetRequiredService<IFleetTracker>().Should().BeSameAs(tracker);
        sut.Fleet.Should().ContainSingle().Which.Vehicle.Key.Should().Be(LiveSnapshots.Key);
    }
}
