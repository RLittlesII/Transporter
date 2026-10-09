using LanguageExt;
using Transponder.Model;

namespace Transponder.Tracking;

/// <summary>A vehicle with what its last update moved, before the stale mark is derived (fleet-pipeline B-032, B-033, B-041).</summary>
internal sealed record MovedVehicle
{
    /// <summary>Gets the vehicle, exactly as the seam reported it.</summary>
    public required TransportVehicle Vehicle { get; init; }

    /// <summary>Gets the vehicle the last update replaced.</summary>
    public required Option<TransportVehicle> Replaced { get; init; }

    /// <summary>Gets the metres between the replaced vehicle's position and this one's.</summary>
    public required Option<double> Leg { get; init; }

    /// <summary>Gets the metres flown since the vehicle entered the fleet.</summary>
    public required double Travelled { get; init; }
}
