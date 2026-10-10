using System;
using Transporter.Model;

namespace Transporter.Tracking;

/// <summary>The distance between two positions on the Earth's surface (fleet-pipeline B-032).</summary>
/// <remarks>
/// A sphere of the mean radius, which is within half a percent of the ellipsoid and invisible at a
/// tenth of a kilometre (fleet-pipeline § 7).
/// </remarks>
internal static class GreatCircle
{
    /// <summary>Measures the great-circle distance between two positions, by the haversine formula.</summary>
    /// <param name="from">Where the vehicle was.</param>
    /// <param name="to">Where it is.</param>
    /// <returns>The distance in metres.</returns>
    internal static double Metres(GeoPosition from, GeoPosition to)
    {
        var fromLatitude = Radians(from.Latitude);
        var toLatitude = Radians(to.Latitude);
        var northing = Math.Sin((toLatitude - fromLatitude) / 2);
        var easting = Math.Sin(Radians(to.Longitude - from.Longitude) / 2);
        var haversine = (northing * northing) + (Math.Cos(fromLatitude) * Math.Cos(toLatitude) * easting * easting);

        return 2 * MeanRadius * Math.Asin(Math.Sqrt(haversine));
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    /// <summary>The Earth's mean radius in metres.</summary>
    private const double MeanRadius = 6_371_008.8;
}
