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
}
