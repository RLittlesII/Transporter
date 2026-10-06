namespace Transponder.Integrations.OpenSky.Model;

/// <summary>
/// The box a poll asks about. Four degrees of latitude and longitude, in the provider's own units
/// and its own order, configured per decisions/0001 with no default compiled in (B-050).
/// </summary>
/// <remarks>
/// This type never reaches the contract: B-006 and B-024 both forbid a box on it, so
/// <see cref="Contracts.IOpenSkyApi.GetStates"/> takes the four coordinates loose and the box stays
/// here, on the options the client is configured with.
/// </remarks>
internal sealed record BoundingBox
{
    /// <summary>Gets the box's lower latitude bound, in degrees.</summary>
    public required double LatitudeMinimum { get; init; }

    /// <summary>Gets the box's lower longitude bound, in degrees.</summary>
    public required double LongitudeMinimum { get; init; }

    /// <summary>Gets the box's upper latitude bound, in degrees.</summary>
    public required double LatitudeMaximum { get; init; }

    /// <summary>Gets the box's upper longitude bound, in degrees.</summary>
    public required double LongitudeMaximum { get; init; }
}
