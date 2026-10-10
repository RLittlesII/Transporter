using AwesomeAssertions;
using Transporter.Model;

namespace Transporter.UnitTests.Model;

public class AircraftTests
{
    /// <summary>
    /// fleet-pipeline B-013. An aircraft answers the grouping key with its origin country, and the
    /// answer is never absent because the wire's value defaults to empty. The member is
    /// <see langword="abstract" /> on <c>TransportVehicle</c>, so a source that answers none does not
    /// compile — which is the compile error <c>Barge</c> in <c>FleetSortTests</c> had to satisfy to
    /// exist at all.
    /// </summary>
    [Fact]
    public void GivenAnAircraft_WhenItsGroupKeyIsRead_ThenItIsTheOriginCountry()
    {
        // Given
        var aircraft = new Aircraft("a1b2c3", LastContact) { OriginCountry = "Mexico" };

        // When
        var grouped = aircraft.GroupKey;

        // Then
        grouped.Should().Be("Mexico");
        new Aircraft("d4e5f6", LastContact).GroupKey.Should().BeEmpty("a country the wire did not report is empty, not absent");
    }

    /// <summary>The instant every aircraft here was last heard from.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}
