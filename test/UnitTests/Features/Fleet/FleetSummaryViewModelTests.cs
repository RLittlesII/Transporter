using System.Reactive.Linq;
using AwesomeAssertions;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Features.Fleet.ViewModels;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Features.Fleet;

public class FleetSummaryViewModelTests
{
    /// <summary>
    /// B-015. The strip shows the tracker's counts as one value, and counts nothing itself. The
    /// hazard is a view model that counts the bound collection: it agrees with the tracker until a
    /// filter hides a stale vehicle, and then the two disagree with nothing reporting it.
    /// </summary>
    [Fact]
    public void GivenTheTrackersSummary_WhenItChanges_ThenTheProjectedCountsFollowItAndNothingIsRecomputed()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Summary.Returns(Observable.Return(new FleetSummary { Tracked = 4, Stale = 1, Groups = 2 }));
        FleetSummaryViewModel sut = new FleetSummaryViewModelFixture().WithTracker(tracker).WithProvider(schedulers);

        // When
        scheduler.Start();

        // Then
        (sut.Tracked, sut.Stale, sut.Groups).Should().Be((4, 1, 2));
        _ = tracker.DidNotReceive().Fleet;
    }

    /// <summary>
    /// B-015. Before the tracker has derived anything the strip reads an empty fleet, which is what
    /// there is, rather than a count left over from a source it no longer reads.
    /// </summary>
    [Fact]
    public void GivenNoSummaryYet_WhenTheCountsAreRead_ThenTheyAreZero()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Summary.Returns(Observable.Never<FleetSummary>());

        // When
        FleetSummaryViewModel sut = new FleetSummaryViewModelFixture().WithTracker(tracker).WithProvider(schedulers);
        scheduler.Start();

        // Then
        (sut.Tracked, sut.Stale, sut.Groups).Should().Be((0, 0, 0));
    }
}

/// <summary>Builds the summary view model, so a constructor change edits this fixture rather than every test.</summary>
[AutoFixture(typeof(FleetSummaryViewModel))]
internal partial class FleetSummaryViewModelFixture;
