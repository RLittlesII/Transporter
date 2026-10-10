using AwesomeAssertions;
using Transporter.Features.Fleet;
using Transporter.Tracking;
using Transporter.Tracking.Sources;

namespace Transporter.UnitTests.Features.Fleet;

public class FleetCardStatusTests
{
    /// <summary>
    /// B-032. The badge says stale, no fix, updated or fresh, read off the element through the
    /// card's own readouts. The hazard this item was reopened for is a card that never says updated,
    /// so a feed that changes every poll reads as one that changes nothing.
    /// </summary>
    /// <param name="hazard">What the case exists to catch.</param>
    /// <param name="element">The element the card is bound to.</param>
    /// <param name="expected">The mark the badge carries.</param>
    [Theory]
    [ClassData(typeof(FleetCardStatusCases))]
    public void GivenAnElement_WhenItsCardsMarkIsRead_ThenItIsTheFirstStatusThatHolds(string hazard, TrackedVehicle element, FleetCardMark expected)
    {
        // Given
        var card = AircraftFleetDescription.Offered.Card;

        // When
        var mark = FleetCardStatus.Of(card, element);

        // Then
        mark.Should().Be(expected, hazard);
    }
}
