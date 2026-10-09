using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AwesomeAssertions;
using LanguageExt;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Features.Fleet.ViewModels;
using Transponder.Model;
using Transponder.Scheduling;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;
using Transponder.Tracking.Sources;
using Transponder.UnitTests.Model.Fixtures;
using Transponder.UnitTests.Scheduling;
using Transponder.UnitTests.Tracking.Fixtures;

namespace Transponder.UnitTests.Features.Fleet;

public class FleetDetailViewModelTests
{
    /// <summary>
    /// B-013. The pane's lines are the description's, in its order, each a name beside the cell it
    /// reads. The hazard is a pane that lays out its own fields: it reads plausibly for an aircraft,
    /// and it is a second place naming the subclass, which a swap would have to edit.
    /// </summary>
    [Fact]
    public void GivenASelectedVehicle_WhenItsRowsAreRead_ThenEachIsADetailLineTheDescriptionNames()
    {
        // Given
        var scheduler = new TestScheduler();
        FleetSourceDescription description = new FleetSourceDescriptionFixture().WithDetail(
        [
            new FleetColumn { Name = "Key", Value = static vehicle => vehicle.Key },
            new FleetColumn { Name = "Label", Value = static vehicle => vehicle.Label },
        ]);
        TrackedVehicle selected = new TrackedVehicleFixture().WithVehicle(new AircraftFixture().WithKey("a1b2c3").WithCallsign("ZULU"));
        FleetDetailViewModel sut = new FleetDetailViewModelFixture()
            .WithSelected(Observable.Return(Option<TrackedVehicle>.Some(selected)))
            .WithDescription(Observable.Return(description))
            .WithScheduler(scheduler);

        // When
        scheduler.Start();

        // Then
        sut.Rows.Should().Equal(new FleetDetailRow("Key", "a1b2c3"), new FleetDetailRow("Label", "ZULU"));
        sut.Title.Should().Be("ZULU");
        sut.IsEmpty.Should().BeFalse();
    }

    /// <summary>
    /// B-014. Absent is the pane's empty state: no rows, no heading, and the prompt showing. The
    /// hazard is a pane that keeps the last vehicle's rows when the selection clears, which reads as a
    /// vehicle the grid no longer has.
    /// </summary>
    [Fact]
    public void GivenAPaneShowingAVehicle_WhenTheSelectionClears_ThenThePaneIsEmpty()
    {
        // Given
        var scheduler = new TestScheduler();
        using var selection = new BehaviorSubject<Option<TrackedVehicle>>(Option<TrackedVehicle>.Some(new TrackedVehicleFixture()));
        FleetDetailViewModel sut = new FleetDetailViewModelFixture()
            .WithSelected(selection)
            .WithDescription(Observable.Return(AircraftFleetDescription.Offered))
            .WithScheduler(scheduler);
        scheduler.Start();

        // When
        selection.OnNext(Option<TrackedVehicle>.None);
        scheduler.Start();

        // Then
        sut.IsEmpty.Should().BeTrue();
        sut.Rows.Should().BeEmpty();
        sut.Title.Should().BeEmpty();
    }

    /// <summary>
    /// B-019. The pane transforms nothing: a canonical altitude reaches it already in feet, because the
    /// description's line converted it, and the vehicle still holds metres afterwards. The hazards are
    /// a conversion done again in the view model, which is how the pane and the card come to round one
    /// value two ways, and a conversion done in place on the domain value. 10,668 m is exactly 35,000 ft.
    /// </summary>
    [Fact]
    public void GivenACanonicalAltitude_WhenThePaneProjectsIt_ThenTheRowIsTheDescriptionsCellAndTheVehicleIsUnchanged()
    {
        // Given
        var scheduler = new TestScheduler();
        Aircraft aircraft = new AircraftFixture().WithBarometricAltitude(10_668);
        TrackedVehicle selected = new TrackedVehicleFixture().WithVehicle(aircraft);
        FleetDetailViewModel sut = new FleetDetailViewModelFixture()
            .WithSelected(Observable.Return(Option<TrackedVehicle>.Some(selected)))
            .WithDescription(Observable.Return(AircraftFleetDescription.Offered))
            .WithScheduler(scheduler);

        // When
        scheduler.Start();

        // Then
        sut.Rows.Should().Contain(new FleetDetailRow("Altitude", "35,000 ft"));
        (aircraft.BarometricAltitude == Option<double>.Some(10_668)).Should().BeTrue("the pane converts for display and writes nothing back");
    }
}

/// <summary>Builds the detail view model, so a constructor change edits this fixture rather than every test.</summary>
/// <remarks>
/// Hand-written over <see cref="AutoFixtureBase{TFixture}"/>: the generator names a member for each
/// parameter's type, and two of these are observables, so it would emit two <c>WithObservable</c>.
/// Nothing selected, no description and one scheduler in both positions are the defaults.
/// </remarks>
internal sealed class FleetDetailViewModelFixture : AutoFixtureBase<FleetDetailViewModelFixture>
{
    /// <summary>Sets the selection the pane follows.</summary>
    /// <param name="selected">The selection stream.</param>
    /// <returns>The fixture, so building chains.</returns>
    public FleetDetailViewModelFixture WithSelected(IObservable<Option<TrackedVehicle>> selected) => With(ref _selected, selected);

    /// <summary>Sets the description whose detail lines the pane reads.</summary>
    /// <param name="description">The description stream.</param>
    /// <returns>The fixture, so building chains.</returns>
    public FleetDetailViewModelFixture WithDescription(IObservable<FleetSourceDescription> description) => With(ref _description, description);

    /// <summary>Puts one scheduler in both positions, so a test advances time once.</summary>
    /// <param name="scheduler">The test's scheduler.</param>
    /// <returns>The fixture, so building chains.</returns>
    public FleetDetailViewModelFixture WithScheduler(TestScheduler scheduler) => With(ref _scheduler, scheduler);

    /// <summary>Takes the subject, as every fixture here is taken.</summary>
    /// <param name="fixture">The fixture to build.</param>
    public static implicit operator FleetDetailViewModel(FleetDetailViewModelFixture fixture)
    {
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(fixture._scheduler);
        return new FleetDetailViewModel(fixture._selected, fixture._description, schedulers);
    }

    private IObservable<Option<TrackedVehicle>> _selected = Observable.Return(Option<TrackedVehicle>.None);
    private IObservable<FleetSourceDescription> _description = Observable.Never<FleetSourceDescription>();
    private TestScheduler _scheduler = new();
}
