using AwesomeAssertions;
using DynamicData;
using LanguageExt;
using NSubstitute;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Model.Fixtures;
using Transporter.UnitTests.Tracking.Fixtures;

namespace Transporter.UnitTests.Tracking;

// RSA1010 asks for ObserveOn before every Bind. These bind without one deliberately: the
// pipeline marshals for nobody (fleet-pipeline B-005) and the user-interface scheduler belongs to
// the consumer (§ 5 row 9). The consumer here is a test, which has no user-interface thread.
#pragma warning disable RSA1010

public class FleetReadoutTests
{
    /// <summary>
    /// fleet-pipeline B-042. A delta is the change at the precision its cell shows, already
    /// formatted, and none where the cell did not change or either side has no value. The hazard
    /// each case names is a delta that reads plausibly and is wrong: a climb shown as a descent, or
    /// "▲ +0 ft" for a change no cell shows.
    /// </summary>
    /// <param name="readout">The readout's column name.</param>
    /// <param name="before">The value the replaced aircraft reported.</param>
    /// <param name="after">The value the current aircraft reports.</param>
    /// <param name="expected">The change, or null for none.</param>
    [Theory]
    [ClassData(typeof(FleetReadoutCases))]
    public void GivenTwoAircraft_WhenAReadoutsDeltaIsRead_ThenItIsTheChangeAtTheCellsPrecision(string readout, double? before, double? after, string? expected)
    {
        // Given
        var sut = AircraftReadouts.Named(readout);
        TransportVehicle replaced = AircraftReadouts.Reporting(readout, before);
        TrackedVehicle element = new TrackedVehicleFixture()
            .WithVehicle(AircraftReadouts.Reporting(readout, after))
            .WithReplaced(replaced);

        // When
        var change = sut.Change(element);

        // Then
        change.IfNoneUnsafe((string?) null).Should().Be(expected);
    }

    /// <summary>
    /// fleet-pipeline B-042. An element just entered replaced nothing (B-041), so no readout shows a
    /// change on it — the arrow appears on the first update, never on the first sighting.
    /// </summary>
    [Fact]
    public void GivenAnElementJustAdded_WhenEachReadoutsChangeIsRead_ThenThereIsNone()
    {
        // Given
        var readouts = AircraftFleetDescription.Offered.Card.Readouts;
        TrackedVehicle element = new TrackedVehicleFixture().WithVehicle(
            new AircraftFixture().WithBarometricAltitude(9_000).WithVelocity(200).WithTrueTrack(90).WithVerticalRate(5.08));

        // When
        var changes = readouts.Select(readout => readout.Change(element)).ToList();

        // Then
        changes.Should().HaveCount(4).And.OnlyContain(static change => change.IsNone);
    }

    /// <summary>
    /// fleet-pipeline B-042. The heading names no delta, because a subtraction reads 359° to 1° as a
    /// turn of 358°. The heading here did change, so the none is the readout's and not the data's.
    /// </summary>
    [Fact]
    public void GivenAReadoutNamingNoDelta_WhenItsChangeIsRead_ThenThereIsNone()
    {
        // Given
        var sut = AircraftReadouts.Named("Heading");
        TransportVehicle replaced = new AircraftFixture().WithTrueTrack(90);
        TrackedVehicle element = new TrackedVehicleFixture()
            .WithVehicle(new AircraftFixture().WithTrueTrack(180))
            .WithReplaced(replaced);

        // When
        var change = sut.Change(element);

        // Then
        change.IsNone.Should().BeTrue();
    }

    /// <summary>
    /// fleet-pipeline B-042. Through the tracker: the change a readout shows is read off the element
    /// the pipeline published, against the vehicle B-041 says the update replaced. A readout that
    /// read some other earlier value — the first sighting, or the vehicle itself — fails here and
    /// nowhere else.
    /// </summary>
    [Fact]
    public void GivenABoundFleet_WhenAnUpdateClimbs_ThenTheAltitudeReadoutReadsTheChangeOffTheElement()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        var altitude = AircraftReadouts.Named("Altitude");
        cache.AddOrUpdate(new AircraftFixture().WithBarometricAltitude(8_000));
        cache.AddOrUpdate(new AircraftFixture().WithBarometricAltitude(9_000));

        // When
        cache.AddOrUpdate(new AircraftFixture().WithBarometricAltitude(9_036.6));

        // Then
        altitude.Change(rows.Single()).IfNoneUnsafe((string?) null).Should().Be("▲ +120 ft");
    }

    /// <summary>
    /// fleet-pipeline B-042. Each readout's cell is in the unit a card shows. It is listed under the
    /// delta's claim because the delta's precision is defined as the cell's, so a cell in the wrong
    /// unit makes every delta wrong with it.
    /// </summary>
    /// <param name="readout">The readout's column name.</param>
    /// <param name="value">The value the aircraft reports, or null for none.</param>
    /// <param name="expected">The cell.</param>
    [Theory]
    [ClassData(typeof(ReadoutCellCases))]
    public void GivenAnAircraft_WhenItsReadoutCellsAreRead_ThenEachIsInItsDisplayUnit(string readout, double? value, string expected)
    {
        // Given
        var sut = AircraftReadouts.Named(readout);
        TransportVehicle vehicle = AircraftReadouts.Reporting(readout, value);

        // When
        var cell = sut.Column.Value(vehicle);

        // Then
        cell.Should().Be(expected);
    }
}
