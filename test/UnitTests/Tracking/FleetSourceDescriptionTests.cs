using AwesomeAssertions;
using Transponder.Model;
using Transponder.Tracking.Sources;

namespace Transponder.UnitTests.Tracking;

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
        TransportVehicle vehicle = new Aircraft("a1b2c3", LastContact) { Callsign = "ZULU", OriginCountry = "Mexico" };

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
        TransportVehicle grounded = new Aircraft("a1b2c3", LastContact) { Callsign = "ZULU", OriginCountry = "Mexico", OnGround = true };
        TransportVehicle airborne = new Aircraft("d4e5f6", LastContact) { Callsign = "ALFA", OriginCountry = "Mexico", OnGround = false };
        TransportVehicle located = new Aircraft("f7a8b9", LastContact) { Callsign = "BRAVO", Position = new GeoPosition(19.4, -99.1) };

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

    /// <summary>The instant the vehicle here was last heard from.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}
