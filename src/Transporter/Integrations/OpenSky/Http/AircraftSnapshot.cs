using LanguageExt;

namespace Transporter.Integrations.OpenSky;

/// <summary>
/// One aircraft as OpenSky reported it: the wire's values, in the wire's units, with names on them.
/// Nothing here is converted, derived or interpreted — that is the projection's work, one layer up.
/// </summary>
internal sealed record AircraftSnapshot
{
    public required string Icao24 { get; init; }

    public required Option<string> Callsign { get; init; }

    public required string OriginCountry { get; init; }

    public required Option<long> TimePosition { get; init; }

    public required long LastContact { get; init; }

    public required Option<double> Longitude { get; init; }

    public required Option<double> Latitude { get; init; }

    public required Option<double> BarometricAltitude { get; init; }

    public required bool OnGround { get; init; }

    public required Option<double> Velocity { get; init; }

    public required Option<double> TrueTrack { get; init; }

    public required Option<double> VerticalRate { get; init; }

    public required Option<double> GeometricAltitude { get; init; }

    public required Option<string> Squawk { get; init; }

    public required bool Spi { get; init; }

    public required int PositionSource { get; init; }

    public required Option<int> Category { get; init; }
}
