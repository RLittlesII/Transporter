using System.Reactive.Linq;
using System.Reactive.Subjects;
using AwesomeAssertions;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Features.Fleet.ViewModels;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.UnitTests.Scheduling;
using Transporter.UnitTests.Tracking.Fixtures;

namespace Transporter.UnitTests.Features.Fleet;

public class FleetPollViewModelTests
{
    /// <summary>
    /// B-036. A source that does not poll shows neither the countdown nor the badge. The hazard is a
    /// countdown shown for replay, counting toward a poll that never happens.
    /// </summary>
    [Fact]
    public void GivenNoPollStatus_WhenTheViewModelIsRead_ThenNothingIsShown()
    {
        // Given
        var scheduler = new TestScheduler();
        var tracker = Substitute.For<IFleetTracker>();
        tracker.PollStatus.Returns(Observable.Return(PollStatus.None));
        using FleetPollViewModel sut = new FleetPollViewModelFixture().WithTracker(tracker).WithProvider(Provider(scheduler));

        // When
        scheduler.Start();

        // Then
        sut.IsPolling.Should().BeFalse();
        sut.NextDue.Should().BeNull();
        sut.RefusedFor.Should().BeNull();
    }

    /// <summary>
    /// B-036. A refusal shows the interval the provider asked for and the due instant it moved, both
    /// as the status carried them. The hazard is a view model that recomputes the instant from the
    /// interval, which drifts from the poller's own schedule.
    /// </summary>
    [Fact]
    public void GivenARefusal_WhenItArrives_ThenRefusedForAndNextDueAreTheStatuses()
    {
        // Given
        var scheduler = new TestScheduler();
        var due = new DateTimeOffset(2026, 10, 8, 14, 33, 10, TimeSpan.Zero);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.PollStatus.Returns(Observable.Return(new PollStatus { NextDue = due, RefusedFor = TimeSpan.FromSeconds(60) }));
        using FleetPollViewModel sut = new FleetPollViewModelFixture().WithTracker(tracker).WithProvider(Provider(scheduler));

        // When
        scheduler.Start();

        // Then
        sut.IsPolling.Should().BeTrue();
        sut.NextDue.Should().Be(due);
        sut.RefusedFor.Should().Be(TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// B-036 and B-017. Time passing with no new status changes nothing. The hazard is a timer in the
    /// view model ticking the seconds down, which is the view's animation's job.
    /// </summary>
    [Fact]
    public void GivenAStatus_WhenTheSchedulerAdvances_ThenNoPropertyChanges()
    {
        // Given
        var scheduler = new TestScheduler();
        var status = new BehaviorSubject<PollStatus>(new PollStatus { NextDue = new DateTimeOffset(2026, 10, 8, 14, 32, 25, TimeSpan.Zero) });
        var tracker = Substitute.For<IFleetTracker>();
        tracker.PollStatus.Returns(status);
        using FleetPollViewModel sut = new FleetPollViewModelFixture().WithTracker(tracker).WithProvider(Provider(scheduler));
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        // When
        scheduler.AdvanceBy(TimeSpan.FromMinutes(5).Ticks);

        // Then
        changed.Should().BeEmpty("the countdown is the view's animation, not a tick here");
    }

    /// <summary>B-036: one status notification carries matching poll/due instants, refusal and absence.</summary>
    [Fact]
    public void GivenSuccessRefusalAndNone_WhenStatusesArrive_ThenEachNotificationReadsTheCompleteReport()
    {
        // Given
        var scheduler = new TestScheduler();
        var tracker = Substitute.For<IFleetTracker>();
        using var statuses = new BehaviorSubject<PollStatus>(PollStatus.None);
        tracker.PollStatus.Returns(statuses);
        using FleetPollViewModel sut = new FleetPollViewModelFixture().WithTracker(tracker).WithProvider(Provider(scheduler));
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);
        var start = new DateTimeOffset(2026, 10, 15, 14, 32, 10, TimeSpan.Zero);
        PollStatus applied = new PollStatusFixture().WithPolledAt(start).WithNextDue(start.AddSeconds(15));
        var refused = new PollStatus
        {
            PolledAt = start.AddSeconds(15),
            NextDue = start.AddSeconds(57),
            RefusedFor = TimeSpan.FromSeconds(42),
        };
        var observed = new List<PollStatus>();
        sut.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(FleetPollViewModel.Status))
            {
                observed.Add(sut.Status);
            }
        };

        // When
        statuses.OnNext(applied);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);
        statuses.OnNext(refused);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);
        statuses.OnNext(PollStatus.None);
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);

        // Then
        observed.Should().Equal(applied, refused, PollStatus.None);
        sut.Status.Should().Be(PollStatus.None);
        sut.IsPolling.Should().BeFalse();
    }

    /// <summary>One scheduler in both positions.</summary>
    /// <param name="scheduler">The scheduler the test advances.</param>
    /// <returns>The provider.</returns>
    private static SchedulerProvider Provider(TestScheduler scheduler) =>
        new SchedulerProviderFixture().WithTestScheduler(scheduler);
}

/// <summary>Builds the poll view model, so a constructor change edits this fixture rather than every test.</summary>
[AutoFixture(typeof(FleetPollViewModel))]
internal partial class FleetPollViewModelFixture;
