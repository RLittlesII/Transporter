using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Contracts;
using Transporter.Integrations.OpenSky.Model;
using Transporter.Integrations.OpenSky.Polling;
using Transporter.Messages;
using Transporter.Scheduling;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Integrations.OpenSky;

public class AircraftPollActorTests : TestKit
{
    /// <summary>
    /// B-053, the refusal. A press five seconds after a poll spends nothing: the provider is not
    /// called, the actor answers nothing, and the changeset stream neither faults nor completes —
    /// a throttle implemented as an exception would surface on stage as a broken grid.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAPollPerformedInsideTheInterval_WhenAPollIsDemanded_ThenItIsRefusedSilentlyAndNothingFaults()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var options = Options.Create(new OpenSkyOptions { Box = Houston, PollInterval = TimeSpan.FromSeconds(15) });
        var api = Substitute.For<IOpenSkyApi>();
        api.GetStates(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(static _ => Task.FromResult(OpenSkyPayloads.ThreeRows.Response));
        AircraftSnapshotClient client = new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithProvider(schedulers)
            .WithOptions(options);
        var completed = false;
        Exception? errored = null;
        using var subscription = client.Snapshots.Subscribe(static _ => { }, failure => errored = failure, () => completed = true);
        await client.PollNow(CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks);
        var sut = Sys.ActorOf(Starts(client, options, schedulers));

        // When
        sut.Tell(DemandPoll.Instance, TestActor);

        // Then
        ExpectNoMsg(TimeSpan.FromMilliseconds(250));
        api.ReceivedCalls().Should().HaveCount(1, "the demanded poll fell inside the interval and was refused");
        errored.Should().BeNull("a refusal is not a failure");
        completed.Should().BeFalse();
    }

    /// <summary>
    /// B-053, the poll. Outside the interval the demand is performed, and the snapshot reaches the
    /// cache by the path every scheduled poll takes — the gesture buys new data rather than a
    /// second code path to keep in step.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenTheIntervalHasElapsedSinceTheLastPoll_WhenAPollIsDemanded_ThenOneCallIsMadeAndItsSnapshotsReachTheCache()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var options = Options.Create(new OpenSkyOptions { Box = Houston, PollInterval = TimeSpan.FromSeconds(15) });
        var api = Substitute.For<IOpenSkyApi>();
        api.GetStates(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(static _ => Task.FromResult(OpenSkyPayloads.ThreeRows.Response));
        var cache = new SourceCache<AircraftSnapshot, string>(static snapshot => snapshot.Icao24);
        AircraftSnapshotClient client = new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithCache(cache)
            .WithProvider(schedulers)
            .WithOptions(options);
        await client.PollNow(CancellationToken.None);
        cache.Clear();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(15).Ticks);
        var sut = Sys.ActorOf(Starts(client, options, schedulers));

        // When
        sut.Tell(DemandPoll.Instance);

        // Then
        AwaitAssert(() => api.ReceivedCalls().Should().HaveCount(2, "the window had elapsed, so the demand was performed"));
        AwaitAssert(() => cache.Items.Should().HaveCount(3));
    }

    /// <summary>What the system starts the actor from.</summary>
    /// <param name="poll">The poller, which is the live client rather than a double of it.</param>
    /// <param name="options">The interval the refusal window is.</param>
    /// <param name="schedulers">The clock both the actor and the client read.</param>
    /// <returns>The <see cref="Props"/>.</returns>
    /// <remarks>
    /// Not an <c>AutoFixture</c>, for the reason <c>SourceSwapActorTests.Starts</c> gives: Akka reads
    /// this expression and requires a <c>new</c> of the actor.
    /// </remarks>
    private static Props Starts(IDemandedPoll poll, IOptions<OpenSkyOptions> options, ISchedulerProvider schedulers) =>
        Props.Create(() => new AircraftPollActor(poll, options, schedulers, new RecordingLogger<AircraftPollActor>()));

    /// <summary>The box decisions/0001 chose, so a test asserting a poll asserts a real box.</summary>
    private static readonly BoundingBox Houston = new()
    {
        LatitudeMinimum = 28.8,
        LongitudeMinimum = -96.0,
        LatitudeMaximum = 30.4,
        LongitudeMaximum = -94.2,
    };
}
