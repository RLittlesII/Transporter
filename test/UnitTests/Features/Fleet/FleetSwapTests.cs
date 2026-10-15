using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;
using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit.Xunit2;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Reactive.Testing;
using NSubstitute;
using ReactiveMarbles.PropertyChanged;
using Transporter.Features.Fleet.ViewModels;
using Transporter.Messages;
using Transporter.Model;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using Transporter.UnitTests.Model.Fixtures;
using Transporter.UnitTests.Scheduling;
using Transporter.UnitTests.Tracking.Fixtures;

namespace Transporter.UnitTests.Features.Fleet;

public class FleetSwapTests : TestKit
{
    /// <summary>B-040: discovery preserves registration order and is not a swap.</summary>
    [Fact]
    public void GivenTheActorAnswersTargets_WhenTheDashboardLoads_ThenTheirOrderAndHandlesArePreservedWithoutSelection()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            answer,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);

        // When
        Flush(scheduler);

        // Then
        sut.Targets.Should().Equal("Zeta feed", "Alpha feed");
        sut.SelectedTarget.Should().Be(-1);
        sut.HasTargets.Should().BeTrue();
        sut.SwapCommand.CanCommandExecute.Should().NotBeNull();
        ((ICommand) sut.SwapCommand).CanExecute(null).Should().BeFalse();
        actor.UnderlyingActor.Discoveries.Should().Be(1);
        actor.UnderlyingActor.Swaps.Should().BeEmpty();
    }

    /// <summary>B-040: unanswered discovery leaves no default or command target.</summary>
    [Fact]
    public void GivenPendingDiscovery_WhenThePickerIsRead_ThenItIsEmptyAndNoSwapCanExecute()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            null,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);

        // When
        Flush(scheduler);

        // Then
        sut.Targets.Should().BeEmpty();
        sut.HasTargets.Should().BeFalse();
        ((ICommand) sut.SwapCommand).CanExecute(null).Should().BeFalse();
        actor.UnderlyingActor.Discoveries.Should().Be(1);
        actor.UnderlyingActor.Swaps.Should().BeEmpty();
    }

    /// <summary>B-040: an injected failure exercises the timeout failure path without a real delay.</summary>
    [Fact]
    public void GivenDiscoveryFails_WhenTimeAdvances_ThenThereIsNoRetryOrChoice()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            new Status.Failure(new InvalidOperationException("synthetic discovery failure")),
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);

        // When
        scheduler.AdvanceBy(TimeSpan.FromSeconds(30).Ticks);

        // Then
        sut.Targets.Should().BeEmpty();
        actor.UnderlyingActor.Discoveries.Should().Be(1);
        actor.UnderlyingActor.Swaps.Should().BeEmpty();
    }

    /// <summary>B-016 and B-040: choosing carries the answered target, never its position as a reconstructed handle.</summary>
    [Fact]
    public void GivenAnAnsweredTarget_WhenChosen_ThenExactlyThatOpaqueTargetIsTold()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            answer,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);

        // When
        Flush(scheduler);
        sut.SelectedTarget = 1;
        Flush(scheduler);

        // Then
        actor.UnderlyingActor.Swaps.Should().ContainSingle().Which.Target.Should().BeSameAs(answer.Targets[1]);
        sut.IsSwapping.Should().BeTrue();
        actor.UnderlyingActor.Discoveries.Should().Be(1);
    }

    /// <summary>B-040: queued output preserves each gesture even before the UI scheduler drains.</summary>
    [Fact]
    public void GivenTwoRapidChoices_WhenOutputDrains_ThenBothOriginalTargetsAreToldOnce()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            answer,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);

        // When
        Flush(scheduler);
        sut.SelectedTarget = 1;
        sut.SelectedTarget = 0;
        Flush(scheduler);

        // Then
        actor.UnderlyingActor.Swaps.Select(message => message.Target).Should().Equal(answer.Targets[1], answer.Targets[0]);
        sut.IsSwapping.Should().BeTrue();
        actor.UnderlyingActor.Discoveries.Should().Be(1);
    }

    /// <summary>B-040: a later invalid selection cannot erase an already executed valid gesture.</summary>
    [Fact]
    public void GivenAValidThenInvalidChoice_WhenOutputDrains_ThenTheValidGestureSurvives()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            answer,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);

        // When
        Flush(scheduler);
        sut.SelectedTarget = 1;
        sut.SelectedTarget = -1;
        Flush(scheduler);

        // Then
        actor.UnderlyingActor.Swaps.Should().ContainSingle().Which.Target.Should().BeSameAs(answer.Targets[1]);
        sut.IsSwapping.Should().BeTrue();
        actor.UnderlyingActor.Discoveries.Should().Be(1);
    }

    /// <summary>B-040: an unavailable position sends no fabricated target.</summary>
    [Fact]
    public void GivenAnInvalidPosition_WhenChosen_ThenTheCommandRemainsDisabled()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            answer,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);

        // When
        Flush(scheduler);
        sut.SelectedTarget = 99;
        Flush(scheduler);

        // Then
        ((ICommand) sut.SwapCommand).CanExecute(null).Should().BeFalse();
        actor.UnderlyingActor.Swaps.Should().BeEmpty();
    }

    /// <summary>B-016: the bound command path sends one message per execution without an acknowledgement.</summary>
    [Fact]
    public void GivenASelectedTarget_WhenTheICommandPathRuns_ThenItTellsWithoutAwaitingAReply()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            answer,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);
        Flush(scheduler);
        sut.SelectedTarget = 1;
        Flush(scheduler);
        var before = actor.UnderlyingActor.Swaps.Count;

        // When
        ((ICommand) sut.SwapCommand).Execute(null);
        Flush(scheduler);

        // Then
        actor.UnderlyingActor.Swaps.Should().HaveCount(before + 1);
        actor.UnderlyingActor.Swaps[^1].Target.Should().BeSameAs(answer.Targets[1]);
        sut.IsSwapping.Should().BeTrue();
    }

    /// <summary>B-016: cached initial rows and outgoing removes do not close a new window.</summary>
    [Fact]
    public void GivenASwapWindow_WhenOnlyRemovalsArrive_ThenItWaitsForAVisibleAddition()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            answer,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);
        TrackedVehicle existing = new TrackedVehicleFixture();
        fleet.AddOrUpdate(existing);
        Flush(scheduler);
        sut.SelectedTarget = 1;
        Flush(scheduler);
        sut.IsSwapping.Should().BeTrue("a replay of rows already bound is not a post-gesture addition");

        // When
        fleet.RemoveKey(existing.Vehicle.Key);
        Flush(scheduler);
        sut.IsSwapping.Should().BeTrue();
        fleet.AddOrUpdate(new TrackedVehicleFixture().WithVehicle(new AircraftFixture().WithKey("d4e5f6")));
        Flush(scheduler);

        // Then
        sut.IsSwapping.Should().BeFalse();
    }

    /// <summary>B-016: the cap ends presentation state while preserving recovery.</summary>
    [Fact]
    public void GivenASilentSwap_WhenTenSecondsElapse_ThenOnlyItsIndicatorEndsAndAnotherChoiceWorks()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            answer,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);
        Flush(scheduler);
        var opened = 0L;
        using var openings = sut.WhenChanged(static model => model.IsSwapping)
            .Where(static shown => shown)
            .Subscribe(_ => opened = scheduler.Clock);
        sut.SelectedTarget = 1;
        Flush(scheduler);

        // When
        scheduler.AdvanceTo(opened + TimeSpan.FromSeconds(10).Ticks - 1);
        sut.IsSwapping.Should().BeTrue();
        scheduler.AdvanceBy(2);

        // Then
        sut.IsSwapping.Should().BeFalse();
        sut.HasTargets.Should().BeTrue();
        sut.SelectedTarget = 0;
        Flush(scheduler);
        actor.UnderlyingActor.Swaps.Should().HaveCount(2);
        sut.IsSwapping.Should().BeTrue();
    }

    /// <summary>B-016: the latest gesture owns the one deadline.</summary>
    [Fact]
    public void GivenASwapAlreadyWaiting_WhenASecondChoiceArrives_ThenItsDeadlineReplacesTheFirst()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            answer,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);
        Flush(scheduler);
        sut.SelectedTarget = 1;
        scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks);

        // When
        sut.SelectedTarget = 0;
        scheduler.AdvanceBy(TimeSpan.FromSeconds(6).Ticks);

        // Then
        sut.IsSwapping.Should().BeTrue();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(4).Ticks + 10);
        sut.IsSwapping.Should().BeFalse();
        actor.UnderlyingActor.Swaps.Should().HaveCount(2);
    }

    /// <summary>B-016: response observation precedes dispatch, including a synchronous response.</summary>
    [Fact]
    public void GivenAnImmediateAddition_WhenTheSwapIsDispatched_ThenTheArmedWindowObservesIt()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            answer,
            probe.Ref,
            () => fleet.AddOrUpdate(new TrackedVehicleFixture()))));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);

        // When
        Flush(scheduler);
        sut.SelectedTarget = 1;
        Flush(scheduler);

        // Then
        actor.UnderlyingActor.Swaps.Should().ContainSingle();
        sut.Fleet.Should().ContainSingle();
        sut.IsSwapping.Should().BeFalse();
    }

    /// <summary>B-016: the documented outgoing-add race is an observable presentation signal.</summary>
    [Fact]
    public void GivenAVisiblePostGestureAddition_WhenItArrives_ThenNoSourceAcknowledgementIsRequired()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            answer,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);
        Flush(scheduler);
        sut.SelectedTarget = 1;
        Flush(scheduler);

        // When
        fleet.AddOrUpdate(new TrackedVehicleFixture());
        Flush(scheduler);

        // Then
        sut.IsSwapping.Should().BeFalse();
    }

    /// <summary>B-040 and B-005: disposal cancels outstanding discovery and freezes projected bindings.</summary>
    [Fact]
    public void GivenPendingDiscovery_WhenTheViewModelIsDisposed_ThenALateAnswerCannotReachBindings()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var fleet = new SourceCache<TrackedVehicle, string>(static item => item.Vehicle.Key);
        var probe = CreateTestProbe();
        var answer = new SwapTargets([new SwapTarget("Zeta feed", 17), new SwapTarget("Alpha feed", 3)]);
        var actor = ActorOfAsTestActorRef<SelectorActor>(Props.Create(() => new SelectorActor(
            null,
            probe.Ref,
            null)));
        var registry = new ActorRegistry();
        registry.Register<SwapSource>(actor);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(Comparer<TrackedVehicle>.Create(static (a, b) => string.CompareOrdinal(a.Vehicle.Key, b.Vehicle.Key))));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        tracker.Observed.Returns(Observable.Return(DateTimeOffset.MinValue));
        using FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers).WithRegistry(registry);

        // When
        sut.Dispose();
        actor.UnderlyingActor.Answer(answer);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(30).Ticks);

        // Then
        sut.Targets.Should().BeEmpty();
        actor.UnderlyingActor.Discoveries.Should().Be(1);
        actor.UnderlyingActor.Swaps.Should().BeEmpty();
    }

    private static void Flush(TestScheduler scheduler) => scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);
}

/// <summary>An actor running on TestActorRef's calling-thread dispatcher makes discovery and immediate responses deterministic.</summary>
internal sealed class SelectorActor : ReceiveActor
{
    public SelectorActor(object? answer, IActorRef observer, Action? onSwap)
    {
        Receive<GetSwapTargets>(_ =>
        {
            Discoveries++;
            _pending = Sender;
            if (answer is not null)
            {
                Sender.Tell(answer);
            }
        });
        Receive<SwapSource>(message =>
        {
            Swaps.Add(message);
            observer.Tell(message);
            onSwap?.Invoke();
        });
    }

    public int Discoveries { get; private set; }

    public List<SwapSource> Swaps { get; } = [];

    public void Answer(object answer) => _pending.Tell(answer);

    private IActorRef _pending = ActorRefs.Nobody;
}
