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

    /// <summary>The instant the vehicle here was last heard from.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}
