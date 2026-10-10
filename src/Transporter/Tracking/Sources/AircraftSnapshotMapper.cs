using System;
using LanguageExt;
using Riok.Mapperly.Abstractions;
using Transporter.Integrations.OpenSky;
using Transporter.Model;

namespace Transporter.Tracking.Sources;

/// <summary>
/// Projects an <see cref="AircraftSnapshot"/> into an <see cref="Aircraft"/>. This is the first place
/// a domain object exists and the only place one is built (B-034).
/// </summary>
/// <remarks>
/// <see cref="RequiredMappingStrategy.Both"/>, so a member added on either side and forgotten on the
/// other is a build error rather than a silent default. The conversions are named methods a test can
/// call, never buried in a generated member mapping (<c>mapping</c> § "Conversions are explicit").
/// </remarks>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Both)]
internal sealed partial class AircraftSnapshotMapper
{
    /// <summary>The cache key: the snapshot's <c>icao24</c> in lowercase hex (B-037).</summary>
    /// <param name="icao24">The identifier as the wire sent it.</param>
    /// <returns>The key.</returns>
    /// <remarks>
    /// Uppercase hex would split one aircraft into two cache entries, and no type system catches
    /// that. Not a default mapping: as one it was applied to every string member, and the generated
    /// projection lowercased the origin country too.
    /// </remarks>
    [UserMapping(Default = false)]
    internal static string ToKey(string icao24) => icao24.ToLowerInvariant();

    /// <summary>An instant from the wire's Unix seconds.</summary>
    /// <param name="unixSeconds">Seconds since the epoch.</param>
    /// <returns>The instant.</returns>
    /// <remarks>Not a default mapping: the only non-optional instant is the one the factory sets.</remarks>
    [UserMapping(Default = false)]
    internal static DateTimeOffset ToInstant(long unixSeconds) => DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

    /// <summary>An instant from the wire's Unix seconds, absent when the wire reported none.</summary>
    /// <param name="unixSeconds">Seconds since the epoch, or absent.</param>
    /// <returns>The instant, or absent.</returns>
    internal static Option<DateTimeOffset> ToInstant(Option<long> unixSeconds) => unixSeconds.Map(ToInstant);

    /// <summary>The position fix, absent unless the snapshot reported both coordinates.</summary>
    /// <param name="snapshot">What the provider reported.</param>
    /// <returns>The fix, or absent.</returns>
    /// <remarks>
    /// Combining the two coordinates here is what makes "no position, not <c>0,0</c>" something the
    /// compiler enforces rather than something a test has to catch (B-036).
    /// </remarks>
    internal static Option<GeoPosition> ToPosition(AircraftSnapshot snapshot) =>
        snapshot.Latitude.Bind(latitude => snapshot.Longitude.Map(longitude => new GeoPosition(latitude, longitude)));

    /// <summary>How the position was fixed, absent when the wire sent a code this build does not name.</summary>
    /// <param name="code">The wire's integer, index 16.</param>
    /// <returns>The named source, or absent.</returns>
    internal static Option<PositionSource> ToPositionSource(int code) =>
        Enum.IsDefined(typeof(PositionSource), code) ? (PositionSource) code : Option<PositionSource>.None;

    /// <summary>Projects one snapshot.</summary>
    /// <param name="snapshot">What the provider reported.</param>
    /// <returns>The domain aircraft.</returns>
    /// <remarks>
    /// Indices 5 and 6 are ignored at the source because <see cref="ToPosition"/> consumes them, and
    /// <c>icao24</c> and the last contact because the factory below sets the two members ADR-0005
    /// requires a constructor to set. Nothing here converts a unit (B-035).
    /// </remarks>
    [MapperIgnoreSource(nameof(AircraftSnapshot.Icao24))]
    [MapperIgnoreSource(nameof(AircraftSnapshot.LastContact))]
    [MapperIgnoreSource(nameof(AircraftSnapshot.Longitude))]
    [MapperIgnoreSource(nameof(AircraftSnapshot.Latitude))]
    [MapperIgnoreTarget(nameof(Aircraft.Key))]
    [MapperIgnoreTarget(nameof(Aircraft.LastContact))]
    [MapPropertyFromSource(nameof(Aircraft.Position), Use = nameof(ToPosition))]
    internal partial Aircraft Project(AircraftSnapshot snapshot);

    /// <summary>Builds the aircraft, so the key and the last contact are set on construction (ADR-0005 item 3).</summary>
    /// <param name="snapshot">What the provider reported.</param>
    /// <returns>The aircraft the generated mapping then fills.</returns>
    [ObjectFactory]
    private static Aircraft Create(AircraftSnapshot snapshot) =>
        new(ToKey(snapshot.Icao24), ToInstant(snapshot.LastContact));
}
