using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;
using Akka.Hosting;
using Akka.TestKit.Xunit2;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Transponder.Features.Fleet.ViewModels;
using Transponder.Messages;
using Transponder.Model;
using Transponder.Scheduling;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;
using Transponder.UnitTests.Scheduling;

namespace Transponder.UnitTests.Features.Fleet;

public class FleetRefreshTests : TestKit
{
    /// <summary>
    /// B-028. The gesture's whole job is the translation: one message told to the actor the registry
    /// holds, and an indicator that went up because the button was pressed rather than because a row
    /// changed. The fleet moving on its own leaves it alone, which is the clause ADR-0012 says an
    /// implementation is most likely to get wrong — a poll returning identical data changes no row,
    /// so an indicator driven by the collection would never clear and never have shown.
    /// </summary>
    [Fact]
    public void GivenTheRefreshCommand_WhenItIsInvoked_ThenTheActorIsToldAndTheIndicatorIsDrivenByTheGesture()
    {
        // Given
        var scheduler = new TestScheduler();
        var fleet = new SourceCache<TrackedVehicle, string>(static tracked => tracked.Vehicle.Key);
        var actor = CreateTestProbe();
        var sut = ViewModel(Tracker(fleet, Observed()), scheduler, Registry(actor));
        fleet.AddOrUpdate(Tracked("a1b2c3"));
        Flush(scheduler);
        sut.IsRefreshing.Should().BeFalse("a fleet that changed on its own is not a refresh anybody asked for");

        // When
        // Through ICommand, which is the path the button's binding takes; the reactive
        // Execute().Subscribe() the other tests use reaches the same command.
        ((ICommand) sut.RefreshCommand).Execute(null);
        Flush(scheduler);

        // Then
        actor.ExpectMsg<DemandPoll>().Should().BeSameAs(DemandPoll.Instance, "the message is told, and it carries no payload");
        actor.ExpectNoMsg(TimeSpan.FromMilliseconds(50));
        sut.IsRefreshing.Should().BeTrue("the press opened the window; nothing has reported a poll yet");
    }

    /// <summary>
    /// B-028 and `fleet-pipeline` B-031. The observed instant is what clears it, because every
    /// applied poll reports one — including the poll whose data was identical, which raises no notice
    /// (decisions/0001). The instant in force is skipped: the tracker publishes it on subscription,
    /// so a window that read it would close on the press that opened it.
    /// </summary>
    [Fact]
    public void GivenAPressWaitingOnAPoll_WhenAnObservedInstantArrives_ThenTheIndicatorClears()
    {
        // Given
        var scheduler = new TestScheduler();
        var observed = Observed();
        var sut = ViewModel(Tracker(Fleet(), observed), scheduler, Registry(CreateTestProbe()));
        using var press = sut.RefreshCommand.Execute().Subscribe();
        Flush(scheduler);
        sut.IsRefreshing.Should().BeTrue("the instant in force is not a poll that landed");

        // When
        observed.OnNext(Reported);
        Flush(scheduler);

        // Then
        sut.IsRefreshing.Should().BeFalse("a poll reported an instant, which is what a landed poll looks like");
    }

    /// <summary>
    /// B-028, the refused press. A press inside the polling interval produces no poll at all
    /// (`aircraft-source` B-053), so nothing will ever report an instant for it and the cap is the
    /// only thing that ends the window. The cap is timed on the injected scheduler, so this test
    /// advances three seconds rather than waiting them.
    /// </summary>
    [Fact]
    public void GivenAPressTheActorRefused_WhenTheCapElapses_ThenTheIndicatorClears()
    {
        // Given
        var scheduler = new TestScheduler();
        var sut = ViewModel(Tracker(Fleet(), Observed()), scheduler, Registry(CreateTestProbe()));
        using var press = sut.RefreshCommand.Execute().Subscribe();
        Flush(scheduler);
        sut.IsRefreshing.Should().BeTrue("the window is open, and no instant will ever arrive for this press");

        // When
        scheduler.AdvanceBy(TimeSpan.FromSeconds(3).Ticks);

        // Then
        sut.IsRefreshing.Should().BeFalse("no poll happened, and the indicator does not spin for ever over a refusal");
    }

    /// <summary>
    /// B-028. A second press replaces the window rather than queueing one, which is what `Switch`
    /// states. It matters for the refused press: two presses a second apart must not leave a cap from
    /// the first one landing while the second is still waiting, and must not raise the indicator again
    /// after a poll has cleared it.
    /// </summary>
    [Fact]
    public void GivenAnIndicatorAlreadyShowing_WhenASecondPressArrives_ThenTheWindowIsReplacedRatherThanQueued()
    {
        // Given
        var scheduler = new TestScheduler();
        var observed = Observed();
        var sut = ViewModel(Tracker(Fleet(), observed), scheduler, Registry(CreateTestProbe()));
        using var first = sut.RefreshCommand.Execute().Subscribe();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        // When
        using var second = sut.RefreshCommand.Execute().Subscribe();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        sut.IsRefreshing.Should().BeTrue("the first window's cap was replaced, not left running beside the second");
        observed.OnNext(Reported);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks);

        // Then
        sut.IsRefreshing.Should().BeFalse("one poll ended the one window, and no earlier cap re-opened it");
    }

    /// <summary>
    /// B-028, with B-017. The view model translates and waits for nothing: the command returns before
    /// any poll could have happened, and the actor is told rather than asked. An `Ask` here would
    /// block the gesture on a network round trip, which is the failure the whole shape avoids.
    /// </summary>
    [Fact]
    public void GivenAnActorThatNeverReplies_WhenTheCommandIsInvoked_ThenTheGestureReturnsAndTheIndicatorStillShows()
    {
        // Given
        var scheduler = new TestScheduler();
        var actor = CreateTestProbe();
        var sut = ViewModel(Tracker(Fleet(), Observed()), scheduler, Registry(actor));

        // When
        using var press = sut.RefreshCommand.Execute().Subscribe();
        Flush(scheduler);

        // Then
        actor.ExpectMsg<DemandPoll>();
        actor.Reply("a poll happened");
        Flush(scheduler);
        sut.IsRefreshing.Should().BeTrue("an actor's reply is not what clears it, because nothing asked for one");
    }

    /// <summary>Delivers what the view model marshalled, without running the timers scheduled ahead of it.</summary>
    /// <param name="scheduler">The one scheduler in the arrangement.</param>
    /// <remarks>
    /// <c>Start()</c> would drain the queue to the end, the three-second cap included, so every
    /// press would read as cleared and the tests that matter would pass for the wrong reason.
    /// A single tick is not enough either: <c>ObserveOn</c> schedules one work item per value, so
    /// the press's value arrives a tick after the one already queued. A tenth of a second drains
    /// what is pending and is well short of the cap.
    /// </remarks>
    private static void Flush(TestScheduler scheduler) => scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);

    /// <summary>The view model under test, built with the one scheduler in both positions.</summary>
    /// <param name="tracker">The seam, substituted.</param>
    /// <param name="scheduler">The clock every operator in the indicator's stream is timed on.</param>
    /// <param name="registry">Where the poll actor is resolved from.</param>
    /// <returns>The view model.</returns>
    private static FleetViewModel ViewModel(IFleetTracker tracker, TestScheduler scheduler, IActorRegistry registry)
    {
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);

        return new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);
    }

    /// <summary>A registry holding the probe under the key the integration registers its actor under.</summary>
    /// <param name="actor">The probe standing in for the poll actor.</param>
    /// <returns>The registry the view model resolves from.</returns>
    private static IActorRegistry Registry(Akka.TestKit.TestProbe actor)
    {
        var registry = new ActorRegistry();
        registry.Register<DemandPoll>(actor.Ref);

        return registry;
    }

    /// <summary>The tracker as the dashboard sees it: a fleet, an order, no description, and the instants.</summary>
    /// <param name="fleet">The cache the fleet stream reports from.</param>
    /// <param name="observed">The instants, seeded the way the clock seeds them.</param>
    /// <returns>The substituted seam.</returns>
    private static IFleetTracker Tracker(SourceCache<TrackedVehicle, string> fleet, IObservable<DateTimeOffset> observed)
    {
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(ByKey));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(observed);

        return tracker;
    }

    /// <summary>The instants, behind the subject the clock publishes them with — so the in-force value arrives on subscription (`fleet-pipeline` § 10).</summary>
    /// <returns>The stream a test pushes a reported instant into.</returns>
    private static BehaviorSubject<DateTimeOffset> Observed() => new(DateTimeOffset.MinValue);

    /// <summary>An empty fleet, keyed the way the pipeline keys it.</summary>
    /// <returns>The cache behind the fleet stream.</returns>
    private static SourceCache<TrackedVehicle, string> Fleet() => new(static tracked => tracked.Vehicle.Key);

    /// <summary>One vehicle as the pipeline publishes it.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The element the fleet stream carries.</returns>
    private static TrackedVehicle Tracked(string key) =>
        new() { Vehicle = new Aircraft(key, Reported), IsStale = false };

    /// <summary>The instant a provider reported, which is the only clock these tests read.</summary>
    private static readonly DateTimeOffset Reported = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly IComparer<TrackedVehicle> ByKey =
        Comparer<TrackedVehicle>.Create(static (left, right) => string.CompareOrdinal(left.Vehicle.Key, right.Vehicle.Key));
}
