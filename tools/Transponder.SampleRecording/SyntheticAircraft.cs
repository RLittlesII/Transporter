namespace Transponder.SampleRecording;

/// <summary>
/// One invented aircraft a generated recording reports, and the hazard its row carries.
/// </summary>
/// <remarks>
/// Mutable, because a recording is the same aircraft reported forty times with its position moved
/// each poll — a record per poll would be forty objects to say one aircraft flew.
/// </remarks>
internal sealed class SyntheticAircraft
{
    /// <summary>Gets the invented ICAO 24-bit address, which is the cache key.</summary>
    public required string Icao24 { get; init; }

    /// <summary>Gets the callsign as the wire carries it, padded to eight characters.</summary>
    public required string Callsign { get; init; }

    /// <summary>Gets the origin country, which is what a grouped view groups by.</summary>
    public required string Country { get; init; }

    /// <summary>Gets the squawk as four characters, so a leading zero survives.</summary>
    public required string Squawk { get; init; }

    /// <summary>Gets a value indicating whether the row is eighteen elements rather than seventeen.</summary>
    public bool Extended { get; init; } = true;

    /// <summary>Gets the aircraft category, absent on a seventeen-element row and zero where the provider reports no information.</summary>
    public int? Category { get; init; }

    /// <summary>Gets a value indicating whether it is on the ground.</summary>
    public bool OnGround { get; init; }

    /// <summary>Gets a value indicating whether it goes quiet partway through the recording.</summary>
    /// <remarks>
    /// Quiet is the provider still listing the aircraft with its last contact frozen, which is how
    /// a vehicle goes stale and is kept rather than removed. A row that vanished would be removed
    /// by the differential write instead, and prove nothing about staleness.
    /// </remarks>
    public bool Quietens { get; init; }

    /// <summary>Gets a value indicating whether its row is one the reader cannot read.</summary>
    public bool Unreadable { get; init; }

    /// <summary>Gets or sets the longitude in degrees.</summary>
    public double Longitude { get; set; }

    /// <summary>Gets or sets the latitude in degrees.</summary>
    public double Latitude { get; set; }

    /// <summary>Gets or sets the track in degrees clockwise from north.</summary>
    public double TrueTrack { get; set; }

    /// <summary>Gets or sets the ground speed in metres per second, absent where the provider reports none.</summary>
    public double? Velocity { get; set; }

    /// <summary>Gets or sets the barometric altitude in metres.</summary>
    public double BarometricAltitude { get; set; }

    /// <summary>Gets or sets the vertical rate in metres per second.</summary>
    public double VerticalRate { get; set; }
}
