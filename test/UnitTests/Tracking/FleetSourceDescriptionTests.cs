using System.Linq;
using AwesomeAssertions;
using Transporter.Model;
using Transporter.Tracking.Fleet;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Model.Fixtures;
using Transporter.UnitTests.Tracking.Fixtures;

namespace Transporter.UnitTests.Tracking;

public class FleetSourceDescriptionTests
{
    /// <summary>
    /// fleet-pipeline B-020. The description carries the columns and the groupings, each column a
    /// display name beside a selector that returns a formatted cell for any vehicle. A column whose
    /// name or selector were missing is a header with nothing under it, which is the swap failing
    /// quietly rather than loudly.
    /// </summary>
    [Fact]
    public void GivenTheAircraftDescription_WhenItsColumnsAreRead_ThenEachCarriesANameAndASelector()
    {
        // Given
        var offered = AircraftFleetDescription.Offered;
        TransportVehicle vehicle = new AircraftFixture().WithCallsign("ZULU").WithOriginCountry("Mexico");

        // When
        var cells = offered.Columns.Select(column => column.Value(vehicle)).ToList();
        var groups = offered.Groupings.Select(grouping => grouping.Key(vehicle)).ToList();

        // Then
        offered.Columns.Should().NotBeEmpty().And.OnlyContain(static column => column.Name.Length > 0);
        cells.Should().OnlyContain(static cell => cell.Length > 0, "a column with no cell is a header with nothing under it");
        cells.Should().Contain("ZULU").And.Contain("Mexico").And.Contain("no fix", "the position column reports an absent fix as a fact rather than as 0,0");
        offered.Groupings.Should().NotBeEmpty().And.OnlyContain(static grouping => grouping.Name.Length > 0);
        groups.Should().Equal(["Mexico"]);
    }

    /// <summary>
    /// fleet-pipeline B-029. The curated choices ship with the description, each a name beside a
    /// predicate that admits one vehicle and rejects another — a choice that admits everything is a
    /// dropdown entry that appears to do nothing, which reads as the filter being broken. They are
    /// read off a static description with no fleet in the arrangement, which is the other half of
    /// the claim: a choice is what the source admits, not what the data happens to hold.
    /// </summary>
    [Fact]
    public void GivenTheAircraftDescription_WhenItsFiltersAreRead_ThenEachCarriesANameAndAPredicateThatAdmitsAndRejects()
    {
        // Given
        var offered = AircraftFleetDescription.Offered;
        TransportVehicle grounded = new AircraftFixture().WithCallsign("ZULU").WithOnGround(true);
        TransportVehicle airborne = new AircraftFixture().WithCallsign("ALFA").WithOnGround(false);
        TransportVehicle located = new AircraftFixture().WithCallsign("BRAVO").WithPosition(new GeoPosition(19.4, -99.1));

        // When
        var vehicles = new[] { grounded, airborne, located };

        // Then
        offered.Filters.Should().NotBeEmpty();
        offered.Filters.Should().OnlyContain(static choice => choice.Name.Length > 0, "a choice with no name is an empty dropdown entry");
        offered.Filters.Should().OnlyContain(
            choice => vehicles.Any(vehicle => choice.Matches(vehicle)) && vehicles.Any(vehicle => !choice.Matches(vehicle)),
            "a choice that admits every vehicle or rejects every vehicle constrains nothing");
        var onTheGround = offered.Filters.Single(static choice => choice.Name == "On the ground");
        var reporting = offered.Filters.Single(static choice => choice.Name == "Reporting a position");
        onTheGround.Matches(grounded).Should().BeTrue();
        onTheGround.Matches(airborne).Should().BeFalse();
        reporting.Matches(grounded).Should().BeFalse("the aircraft reports no fix, and absent is a fact rather than 0,0");
    }

    /// <summary>
    /// fleet-pipeline B-036. Each filled role is one of the description's own columns — the same
    /// instance, not one sharing its name — so a role and the column it names cannot disagree on a
    /// selector. A role built from a fresh column passes a name check and fails here. The place is
    /// the role this description leaves empty until it offers a place column.
    /// </summary>
    [Fact]
    public void GivenTheAircraftDescription_WhenItsCardIsRead_ThenEachFilledRoleIsOneOfItsColumns()
    {
        // Given
        var offered = AircraftFleetDescription.Offered;

        // When
        var card = offered.Card;

        // Then
        var title = card.Title.IfNoneUnsafe((FleetColumn?) null);
        var subtitle = card.Subtitle.IfNoneUnsafe((FleetColumn?) null);
        var readouts = card.Readouts.Select(static readout => readout.Column).ToList();
        title.Should().BeSameAs(offered.Columns.Single(static column => column.Name == "Callsign"));
        subtitle.Should().BeSameAs(offered.Columns.Single(static column => column.Name == "Origin country"));
        readouts.Select(static column => column.Name).Should().Equal(["Altitude", "Ground speed", "Heading", "Vertical rate"]);
        readouts.Should().OnlyContain(
            column => offered.Columns.Any(held => ReferenceEquals(held, column)),
            "a readout is a column the description holds");
        card.Place.IsNone.Should().BeTrue("this description offers no place column yet");
    }

    /// <summary>
    /// fleet-pipeline B-036. A description that names no card has every role empty and no readouts,
    /// rather than a role a consumer has to guess at — which holds for any source.
    /// </summary>
    [Fact]
    public void GivenADescriptionNamingNoCard_WhenItsRolesAreRead_ThenEveryRoleIsEmpty()
    {
        // Given
        FleetSourceDescription offered = new FleetSourceDescriptionFixture();

        // When
        var card = offered.Card;

        // Then
        card.Title.IsNone.Should().BeTrue();
        card.Subtitle.IsNone.Should().BeTrue();
        card.Place.IsNone.Should().BeTrue();
        card.Readouts.Should().BeEmpty();
    }

    /// <summary>
    /// fleet-pipeline B-043. The aircraft description names the fields only an aircraft reports as
    /// detail lines, each read in the unit a person reads. The hazard is a pane that can show them only
    /// by naming <see cref="Aircraft"/> itself, which is the cast the description exists to hold.
    /// 10,972.8 m is exactly 36,000 ft.
    /// </summary>
    [Fact]
    public void GivenTheAircraftDescription_WhenItsDetailIsRead_ThenItNamesTheFieldsOnlyAnAircraftReports()
    {
        // Given
        var offered = AircraftFleetDescription.Offered;
        TransportVehicle aircraft = new AircraftFixture().WithSquawk("0021").WithCategory(3).WithGeometricAltitude(10_972.8).WithOnGround(true);

        // When
        var lines = offered.Detail.Select(line => (line.Name, Cell: line.Value(aircraft)));

        // Then
        lines.Should().Contain(("Squawk", "0021"))
            .And.Contain(("Category", "3"))
            .And.Contain(("GPS altitude", "36,000 ft"))
            .And.Contain(("On the ground", "Yes"));
    }

    /// <summary>
    /// fleet-pipeline B-043. A description that names no detail lines has none, so a source that
    /// offers no pane is legal and a pane shows nothing for it rather than lines it guessed at.
    /// </summary>
    [Fact]
    public void GivenADescriptionNamingNoDetail_WhenItsDetailIsRead_ThenItIsEmpty()
    {
        // Given
        FleetSourceDescription offered = new FleetSourceDescriptionFixture();

        // When
        var lines = offered.Detail;

        // Then
        lines.Should().BeEmpty();
    }
}
