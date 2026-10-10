using System;
using LanguageExt;

namespace Transporter.Model;

/// <summary>
/// One aircraft in the domain: the values OpenSky reported, in the units it reported them in, as the
/// grid, the filters and the aggregates see them.
/// </summary>
/// <remarks>
/// The one concrete subclass of <see cref="TransportVehicle"/> for this source, and the only place a
/// field OpenSky alone reports may live (ADR-0005 item 5). Values are stored unconverted — metres,
/// metres per second, degrees clockwise from north — and display units are the view's (B-035).
/// </remarks>
public sealed class Aircraft : TransportVehicle
{
    /// <summary>Initializes a new instance of the <see cref="Aircraft"/> class.</summary>
    /// <param name="key">The <c>icao24</c> in lowercase hex (B-037).</param>
    /// <param name="lastContact">The instant the source last heard from this aircraft.</param>
    public Aircraft(string key, DateTimeOffset lastContact)
        : base(key, lastContact)
    {
    }

    /// <summary>Gets or sets the callsign, absent when the wire reported only padding (B-019).</summary>
    public Option<string> Callsign { get; set; }

    /// <summary>Gets or sets the country the aircraft is registered in.</summary>
    public string OriginCountry { get; set; } = string.Empty;

    /// <summary>Gets or sets the instant the reported position was fixed at.</summary>
    public Option<DateTimeOffset> TimePosition { get; set; }

    /// <summary>Gets or sets the barometric altitude in metres.</summary>
    public Option<double> BarometricAltitude { get; set; }

    /// <summary>Gets or sets the geometric altitude in metres.</summary>
    /// <remarks>Separate from the barometric one, because B-036 requires both to survive the projection.</remarks>
    public Option<double> GeometricAltitude { get; set; }

    /// <summary>Gets or sets a value indicating whether the aircraft is on the ground.</summary>
    public bool OnGround { get; set; }

    /// <summary>Gets or sets the ground speed in metres per second.</summary>
    public Option<double> Velocity { get; set; }

    /// <summary>Gets or sets the heading in degrees clockwise from north.</summary>
    public Option<double> TrueTrack { get; set; }

    /// <summary>Gets or sets the climb rate in metres per second.</summary>
    public Option<double> VerticalRate { get; set; }

    /// <summary>Gets or sets the transponder code as four characters, so <c>"0021"</c> is never the number 21 (B-020).</summary>
    public Option<string> Squawk { get; set; }

    /// <summary>Gets or sets a value indicating whether the special position identification flag is set.</summary>
    public bool Spi { get; set; }

    /// <summary>Gets or sets how the position was fixed, absent when the wire sent a code this build does not name.</summary>
    public Option<PositionSource> PositionSource { get; set; }

    /// <summary>Gets or sets the aircraft category as the wire's integer, absent when the row did not carry one (B-018).</summary>
    /// <remarks>Left uninterpreted: README.md § "Response shape" publishes no value list, so naming the codes would invent a contract OpenSky did not.</remarks>
    public Option<int> Category { get; set; }

    /// <inheritdoc/>
    /// <remarks>Derived, which is why B-014 keeps it off the snapshot.</remarks>
    public override string Label => Callsign.IfNone(Key);

    /// <inheritdoc/>
    /// <remarks>The origin country, which is never absent: the wire's value defaults to empty.</remarks>
    public override string GroupKey => OriginCountry;
}
