namespace Transporter.Model;

/// <summary>A position fix in degrees, as the source reported it.</summary>
/// <param name="Latitude">Degrees north of the equator.</param>
/// <param name="Longitude">Degrees east of the prime meridian.</param>
/// <remarks>
/// One optional position rather than two optional coordinates, because no source reports half a fix
/// (ADR-0005 § "The members").
/// </remarks>
public readonly record struct GeoPosition(double Latitude, double Longitude);
