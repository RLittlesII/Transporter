using LanguageExt;
using Transponder.Model;

namespace Transponder.Tracking;

/// <summary>
/// One vehicle as the pipeline publishes it: the vehicle, and whether it was silent past the
/// threshold when the mark was derived (B-051).
/// </summary>
/// <remarks>
/// The mark is not stored on the vehicle, because <c>domain-model</c> § "Never add" forbids a
/// derived member there and the clock moves under it.
/// </remarks>
public sealed record TrackedVehicle
{
    /// <summary>Gets the vehicle, exactly as the seam reported it.</summary>
    public required TransportVehicle Vehicle { get; init; }

    /// <summary>Gets a value indicating whether the vehicle was stale when the pipeline evaluated it.</summary>
    public required bool IsStale { get; init; }

    /// <summary>Gets the vehicle the last update replaced, none for a vehicle that has just entered the fleet (fleet-pipeline B-041).</summary>
    /// <remarks>The vehicle, never the element that carried it, so nothing older than one update is reachable.</remarks>
    public Option<TransportVehicle> Replaced { get; init; } = Option<TransportVehicle>.None;

    /// <summary>Gets the great-circle distance in metres from the replaced vehicle's position to this one's (fleet-pipeline B-032).</summary>
    /// <remarks>None, not zero, on an add and where either side has no position.</remarks>
    public Option<double> Leg { get; init; } = Option<double>.None;

    /// <summary>Gets the metres flown since the vehicle entered the fleet: every leg, summed (fleet-pipeline B-033).</summary>
    public double Travelled { get; init; }
}
