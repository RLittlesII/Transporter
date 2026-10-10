using AwesomeAssertions;
using Transporter.Model;
using Transporter.Tracking;

namespace Transporter.UnitTests.Tracking;

public class GreatCircleTests
{
    /// <summary>
    /// fleet-pipeline B-032. The leg is the great-circle distance in metres, to within a metre, and
    /// the same whichever way it is measured: a formula with its arguments swapped in one term
    /// passes a check made in one direction only.
    /// </summary>
    /// <param name="fromLatitude">The latitude the vehicle left.</param>
    /// <param name="fromLongitude">The longitude the vehicle left.</param>
    /// <param name="toLatitude">The latitude it reached.</param>
    /// <param name="toLongitude">The longitude it reached.</param>
    /// <param name="metres">The distance between them.</param>
    [Theory]
    [ClassData(typeof(GreatCircleCases))]
    public void GivenTwoPositions_WhenMeasured_ThenTheDistanceIsTheGreatCircleInMetres(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude,
        double metres)
    {
        // Given
        var from = new GeoPosition(fromLatitude, fromLongitude);
        var to = new GeoPosition(toLatitude, toLongitude);

        // When
        var outbound = GreatCircle.Metres(from, to);
        var inbound = GreatCircle.Metres(to, from);

        // Then
        outbound.Should().BeApproximately(metres, 1);
        inbound.Should().BeApproximately(metres, 1, "the distance does not depend on the direction it is measured in");
    }
}
