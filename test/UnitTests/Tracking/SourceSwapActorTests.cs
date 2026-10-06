using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using AwesomeAssertions;
using DynamicData;
using Transponder.Model;
using Transponder.Tracking;
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
        var aircraft = new FirstTrackerSource();
        var vessels = new SecondTrackerSource();
        var source = new SwappingTrackerSource([aircraft, vessels]);
        var sut = Sys.ActorOf(Props.Create(() => new SourceSwapActor(source)));
        var observed = new List<IChangeSet<TransportVehicle, string>>();
        using var subscription = source.Connect().Subscribe(observed.Add);
        aircraft.Report("a1b2c3");

        // When
        sut.Tell(SwapSource.To<ISecondTrackerSource>());
        AwaitAssert(() => aircraft.Stopped.Should().Be(1));
        vessels.Report("imo9074729");

        // Then
        observed.Should().HaveCount(3, "the add, the outgoing fleet leaving, and the incoming add");
        vessels.Started.Should().Be(1);
    }

    /// <summary>
    /// Told, never asked: the actor answers nothing, so no caller can learn which source is live from
    /// it any more than from the decorator it tells (B-038).
    /// </summary>
    [Fact]
    public void GivenTheActor_WhenItIsToldToSwap_ThenItRepliesWithNothing()
    {
        // Given
        var source = new SwappingTrackerSource([new FirstTrackerSource(), new SecondTrackerSource()]);
        var sut = Sys.ActorOf(Props.Create(() => new SourceSwapActor(source)));

        // When
        sut.Tell(SwapSource.To<ISecondTrackerSource>(), TestActor);

        // Then
        ExpectNoMsg(TimeSpan.FromMilliseconds(250));
    }
}
