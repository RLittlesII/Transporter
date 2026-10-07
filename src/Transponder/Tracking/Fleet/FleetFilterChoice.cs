using System;
using Transponder.Model;

namespace Transponder.Tracking.Fleet;

/// <summary>One choice the filter control offers: what it shows, and what it admits (B-029).</summary>
/// <remarks>
/// The predicate reads <see cref="TransportVehicle"/>, so the control is written once and a swap
/// replaces the choices rather than the control (B-021). The source's own description is the one
/// place a choice may be built from a cast to the concrete vehicle, which is B-022's single
/// exception: a cast there dies with the source that needed it.
/// </remarks>
public sealed record FleetFilterChoice
{
    /// <summary>Gets what the control shows.</summary>
    public required string Name { get; init; }

    /// <summary>Gets what a vehicle must satisfy to survive this choice.</summary>
    public required Func<TransportVehicle, bool> Matches { get; init; }
}
