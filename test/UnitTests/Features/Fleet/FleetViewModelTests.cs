using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using Akka.Hosting;
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
using Transponder.UnitTests.Model.Fixtures;
using Transponder.UnitTests.Scheduling;
using Transponder.UnitTests.Tracking.Fixtures;

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
        FleetSourceDescription description = new FleetSourceDescriptionFixture();
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

    /// <summary>
    /// B-009. The two inputs leave as one predicate handed to the tracker, and the view model touches
    /// no collection to do it. The hazard is the pattern this demo exists to replace: a view model
    /// that filters its own copy, which looks correct on screen and re-queries the source on every
    /// keystroke. So the assertion is on what the tracker received and on the bound collection being
    /// untouched — the two things a filtering view model would get wrong.
    /// </summary>
    [Fact]
    public void GivenSearchTextAndAFilter_WhenTheyChange_ThenOnePredicateReachesTheTrackerAndNoCollectionIsEnumerated()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var onTheGround = new FleetFilterChoice { Name = "On the ground", Matches = static vehicle => vehicle is Aircraft { OnGround: true } };
        FleetSourceDescription description = new FleetSourceDescriptionFixture().WithFilters([onTheGround]);
        var fleet = new SourceCache<TrackedVehicle, string>(static tracked => tracked.Vehicle.Key);
        var predicates = new List<Func<TransportVehicle, bool>>();
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(fleet.Connect());
        tracker.Order.Returns(Observable.Return(ByKey));
        tracker.Description.Returns(Observable.Return(description));
        tracker.When(static substitute => substitute.Filter(Arg.Any<Func<TransportVehicle, bool>>()))
            .Do(call => predicates.Add(call.Arg<Func<TransportVehicle, bool>>()));
        FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers);
        fleet.AddOrUpdate(Tracked("a1b2c3"));
        scheduler.Start();
        var bound = sut.Fleet.Count;

        // When
        sut.SearchText = "a1b";
        sut.SelectedFilter = onTheGround;
        scheduler.Start();

        // Then
        sut.Filters.Should().Equal(description.Filters, "the choices are the description's, not ones the view model invented");
        predicates.Should().NotBeEmpty("every change to an input hands the tracker a predicate");
        var composed = predicates[^1];
        composed(new AircraftFixture().WithKey("a1b2c3").WithOnGround(true)).Should().BeTrue("the last predicate carries the search and the choice together");
        composed(new AircraftFixture().WithKey("a1b2c3").WithOnGround(false)).Should().BeFalse("the choice is part of the same predicate");
        composed(new AircraftFixture().WithKey("zzz999").WithOnGround(true)).Should().BeFalse("the search is part of the same predicate");
        sut.Fleet.Should().HaveCount(bound, "the view model filters nothing itself — the pipeline is what re-evaluates");
    }

    /// <summary>
    /// B-012. A header hands the description's comparer over, the same header again hands its
    /// reverse, and the grouping chooser hands one of the description's groupings. The failure this
    /// catches is a second tap that re-publishes the same ascending comparer: the rows do not move,
    /// and nothing errors, so it reads as the grid being broken rather than the comparer being
    /// wrong.
    /// </summary>
    [Fact]
    public void GivenAColumnChosenTwiceAndAGroupingChosen_WhenTheTrackerIsRecorded_ThenItReceivedTheComparerItsReverseAndTheGrouping()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var byLabel = Comparer<TransportVehicle>.Create(static (left, right) => string.CompareOrdinal(left.Label, right.Label));
        var sortable = new FleetColumn { Name = "Callsign", Value = static vehicle => vehicle.Label, Comparer = byLabel };
        var unsortable = new FleetColumn { Name = "Position", Value = static vehicle => "no fix" };
        var category = new FleetGrouping { Name = "Category", Key = static vehicle => vehicle.GroupKey };
        FleetSourceDescription description = new FleetSourceDescriptionFixture().WithColumns([sortable, unsortable]).WithGroupings([category]);
        var comparers = new List<IComparer<TransportVehicle>>();
        var groupings = new List<FleetGrouping>();
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Fleet.Returns(Observable.Never<IChangeSet<TrackedVehicle, string>>());
        tracker.Order.Returns(Observable.Return(ByKey));
        tracker.Description.Returns(Observable.Return(description));
        tracker.When(static substitute => substitute.SortBy(Arg.Any<IComparer<TransportVehicle>>()))
            .Do(call => comparers.Add(call.Arg<IComparer<TransportVehicle>>()));
        tracker.When(static substitute => substitute.GroupBy(Arg.Any<FleetGrouping>()))
            .Do(call => groupings.Add(call.Arg<FleetGrouping>()));
        FleetViewModel sut = new FleetViewModelFixture().WithTracker(tracker).WithProvider(schedulers);
        scheduler.Start();
        TransportVehicle first = new AircraftFixture().WithKey("a1b2c3").WithCallsign("AAA");
        TransportVehicle second = new AircraftFixture().WithKey("d4e5f6").WithCallsign("ZZZ");

        // When
        sut.ChooseColumn(sortable);
        sut.ChooseColumn(sortable);
        sut.ChooseColumn(unsortable);
        sut.SelectedGrouping = category;

        // Then
        comparers.Should().HaveCount(2, "the sortable column was chosen twice and the unsortable one changed nothing");
        comparers[0].Compare(first, second).Should().BeNegative("the first choice is the description's own comparer");
        comparers[1].Compare(first, second).Should().BePositive("the second choice is its reverse");
        sut.SortedColumn.IfNone(() => (unsortable, true)).Should().Be((sortable, true), "the active header and its direction are what the grid reads");
        groupings.Should().Equal([category], "the grouping handed over is the description's");
    }

    /// <summary>One vehicle as the pipeline publishes it, keyed so the bound order is readable.</summary>
    /// <param name="key">The key, which is also the label while the callsign is absent.</param>
    /// <returns>The element the fleet stream carries.</returns>
    private static TrackedVehicle Tracked(string key) =>
        new TrackedVehicleFixture().WithVehicle(new AircraftFixture().WithKey(key).WithOriginCountry("Belgium"));

    private static readonly IComparer<TrackedVehicle> ByKey =
        Comparer<TrackedVehicle>.Create(static (left, right) => string.CompareOrdinal(left.Vehicle.Key, right.Vehicle.Key));
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

        // Empty: a test that presses the refresh control registers the probe it expects the
        // message at (B-028), and every other test never resolves from it.
        WithRegistry(new ActorRegistry());
    }
}
