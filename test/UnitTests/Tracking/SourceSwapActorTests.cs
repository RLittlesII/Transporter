using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using AwesomeAssertions;
using DynamicData;
using Transponder.Model;
using Transponder.Tracking.Sources;

namespace Transponder.UnitTests.Tracking;

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

        // When
        sut.Tell(SwapSource.To<ISecondTrackerSource>());
        AwaitAssert(() => strategies.Aircraft.Stopped.Should().Be(1));
        strategies.Vessels.Report("imo9074729");

        // Then
        observed.Should().HaveCount(3, "the add, the outgoing fleet leaving, and the incoming add");
        strategies.Vessels.Started.Should().Be(1);
    }

    /// <summary>
    /// Told, never asked: the actor answers nothing, so no caller can learn which source is live from
    /// it any more than from the decorator it tells (B-038).
    /// </summary>
    [Fact]
    public void GivenTheActor_WhenItIsToldToSwap_ThenItRepliesWithNothing()
    {
        // Given
        var sut = Sys.ActorOf(Starts(new SwappingTrackerSourceFixture()));

        // When
        sut.Tell(SwapSource.To<ISecondTrackerSource>(), TestActor);

        // Then
        ExpectNoMsg(TimeSpan.FromMilliseconds(250));
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
}
