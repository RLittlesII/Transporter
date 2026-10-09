using AwesomeAssertions;
using LanguageExt;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Features.Fleet.ViewModels;
using Transponder.Model;
using Transponder.UnitTests.Model.Fixtures;

namespace Transponder.UnitTests.Features.Fleet;

public class FleetDetailViewModelTests
{
    /// <summary>
    /// B-013. The pane is the one surface that learns it has an aircraft, so it is where a field only
    /// an aircraft reports appears. The hazard is a pane built from the description's columns alone,
    /// which reads plausibly and shows nothing a card did not already show.
    /// </summary>
    [Fact]
    public void GivenASelectedAircraft_WhenItsRowsAreRead_ThenTheyIncludeFieldsOnlyAnAircraftReports()
    {
        // Given
        FleetDetailViewModel sut = new FleetDetailViewModelFixture();
        TransportVehicle aircraft = new AircraftFixture().WithSquawk("0021").WithCategory(3).WithGeometricAltitude(10_668);

        // When
        sut.Vehicle = Option<TransportVehicle>.Some(aircraft);

        // Then
        sut.Rows.Should().Contain(new FleetDetailRow("Squawk", "0021"))
            .And.Contain(new FleetDetailRow("Category", "3"))
            .And.Contain(new FleetDetailRow("GPS altitude", "35,000 ft"));
        sut.IsEmpty.Should().BeFalse();
    }

    /// <summary>
    /// B-014. Absent is the pane's empty state: no rows, no heading, and the prompt showing. The
    /// hazard is a pane that keeps the last vehicle's rows when the selection clears, which reads as a
    /// vehicle the grid no longer has.
    /// </summary>
    [Fact]
    public void GivenAPaneShowingAVehicle_WhenTheVehicleIsCleared_ThenThePaneIsEmpty()
    {
        // Given
        TransportVehicle shown = new AircraftFixture();
        FleetDetailViewModel sut = new FleetDetailViewModelFixture();
        var selection = Option<TransportVehicle>.Some(shown);
        sut.Vehicle = selection;

        // When
        sut.Vehicle = Option<TransportVehicle>.None;

        // Then
        sut.IsEmpty.Should().BeTrue();
        sut.Rows.Should().BeEmpty();
        sut.Title.Should().BeEmpty();
    }

    /// <summary>
    /// B-019. Each canonical unit is converted for display by the pane and the vehicle still holds
    /// the canonical value afterwards. The hazards are a conversion done in place on the domain value,
    /// and a unit the person does not read — metres for an altitude a pilot reads in feet.
    /// </summary>
    /// <param name="label">The row's label.</param>
    /// <param name="value">The canonical value the aircraft reports, or null for none.</param>
    /// <param name="expected">What the row reads.</param>
    [Theory]
    [ClassData(typeof(FleetDetailUnitCases))]
    public void GivenACanonicalValue_WhenThePaneProjectsIt_ThenItReadsInTheDisplayUnitAndTheVehicleIsUnchanged(string label, double? value, string expected)
    {
        // Given
        FleetDetailViewModel sut = new FleetDetailViewModelFixture();
        var aircraft = DetailAircraft.Reporting(label, value);
        var canonical = DetailAircraft.Canonical(label, aircraft);

        // When
        sut.Vehicle = Option<TransportVehicle>.Some(aircraft);

        // Then
        sut.Rows.Should().Contain(new FleetDetailRow(label, expected));
        (DetailAircraft.Canonical(label, aircraft) == canonical).Should().BeTrue("the pane converts for display and writes nothing back");
    }
}

/// <summary>Builds the detail view model, so a constructor change edits this fixture rather than every test.</summary>
/// <remarks>
/// Hand-written over <see cref="AutoFixtureBase{TFixture}"/>: the view model has a parameterless
/// constructor, and the generator emits no conversion for a type with nothing to inject.
/// </remarks>
internal sealed class FleetDetailViewModelFixture : AutoFixtureBase<FleetDetailViewModelFixture>
{
    /// <summary>Takes the subject, as every fixture here is taken.</summary>
    /// <param name="fixture">The fixture to build.</param>
    public static implicit operator FleetDetailViewModel(FleetDetailViewModelFixture fixture) => new();
}
