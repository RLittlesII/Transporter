using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Features.Fleet.ViewModels;
using Transponder.Model;
using Transponder.Scheduling;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;
using Transponder.UnitTests.Scheduling;

namespace Transponder.UnitTests.Features.Fleet;

public class FleetViewModelTests
{
    /// <summary>
    /// B-005. The tracker publishes changesets and owns no collection (ADR-0009), so the one the
    /// grid binds is materialised here or nowhere. The hazard this fails on is a view model that
    /// copies rows into a list of its own: the copy is populated the same way and diverges the
    /// first time a change arrives that the copy's author did not anticipate.
    /// </summary>
    [Fact]
    public void GivenATrackerPublishingAFleet_WhenTheViewModelIsConstructed_ThenItBindsTheStreamIntoItsOneCollection()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var fleet = new SourceCache<TrackedVehicle, string>(static tracked => tracked.Vehicle.Key);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(ByKey));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers);

        // When
        fleet.AddOrUpdate(Tracked("d4e5f6"));
        fleet.AddOrUpdate(Tracked("a1b2c3"));
        scheduler.Start();

        // Then
        sut.Fleet.Should().HaveCount(2, "every vehicle the stream published is in the one collection");
        sut.Fleet.Should().BeInAscendingOrder(static tracked => tracked.Vehicle.Key, "the comparer the tracker published is what the binding sorts by");
    }

    /// <summary>
    /// B-005. Disposal is the clause this Feature is most exposed on: the subscription is the only
    /// thing the view model owns, and the tracker is a container singleton two more view models will
    /// share. A view model that disposed the tracker would freeze the fleet for every other surface,
    /// and one that disposed nothing would keep binding into a collection nobody reads.
    /// </summary>
    [Fact]
    public void GivenABoundViewModel_WhenItIsDisposed_ThenItsSubscriptionGoesAndTheTrackerIsNotDisposed()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var fleet = new SourceCache<TrackedVehicle, string>(static tracked => tracked.Vehicle.Key);
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(ByKey));
        tracker.Description.Returns(Observable.Never<FleetSourceDescription>());
        FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers);
        fleet.AddOrUpdate(Tracked("a1b2c3"));
        scheduler.Start();

        // When
        sut.Dispose();
        fleet.AddOrUpdate(Tracked("d4e5f6"));
        scheduler.Start();

        // Then
        sut.Fleet.Should().ContainSingle("the subscription went with the view model");
        tracker.DidNotReceive().Dispose();
    }

    /// <summary>
    /// B-007. The columns are the live source's answer, read off the description it published, in
    /// its order. The hazard is a grid whose columns were compiled into the markup, which looks
    /// identical until the source swaps and the headers describe the source that left (B-021).
    /// </summary>
    [Fact]
    public void GivenADescription_WhenTheColumnsAreRead_ThenTheyAreTheDescriptionsInItsOrder()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var description = new FleetSourceDescription
        {
            Columns =
            [
                new FleetColumn { Name = "Callsign", Value = static vehicle => vehicle.Label },
                new FleetColumn { Name = "Origin country", Value = static vehicle => vehicle.GroupKey },
            ],
            Groupings = [new FleetGrouping { Name = "Origin country", Key = static vehicle => vehicle.GroupKey }],
        };
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(Observable.Never<IChangeSet<TrackedVehicle, string>>());
        tracker.Order.Returns(Observable.Return(ByKey));
        tracker.Description.Returns(Observable.Return(description));

        // When
        FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers);
        scheduler.Start();

        // Then
        sut.Columns.Should().Equal(description.Columns, "the columns are the description's, in the order it published them");
        sut.Groupings.Should().Equal(description.Groupings);
    }

    private static TrackedVehicle Tracked(string key) =>
        new() { Vehicle = new Aircraft(key, LastContact) { OriginCountry = "Belgium" }, IsStale = false };

    private static readonly IComparer<TrackedVehicle> ByKey =
        Comparer<TrackedVehicle>.Create(static (left, right) => string.CompareOrdinal(left.Vehicle.Key, right.Vehicle.Key));

    private static readonly DateTimeOffset LastContact = new(2026, 10, 6, 14, 32, 10, TimeSpan.Zero);
}

/// <summary>Builds the view model, so a constructor change edits this fixture rather than every test.</summary>
[AutoFixture(typeof(FleetViewModel))]
internal partial class FleetViewModelFixture
{
    /// <summary>Initializes a new instance of the <see cref="FleetViewModelFixture"/> class.</summary>
    /// <remarks>One scheduler in both positions, so a test advances time once; the tracker is each test's to arrange.</remarks>
    public FleetViewModelFixture()
    {
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(new TestScheduler());

        WithProvider(schedulers);
    }
}
