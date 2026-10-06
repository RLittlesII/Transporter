using System;
using Transponder.Model;

namespace Transponder.Features.Fleet;

/// <summary>One constraint the filter control offers: what it shows, and what it admits (B-009).</summary>
/// <remarks>The constraint is over the abstract vehicle, so a swap changes the choices and not the control (B-022).</remarks>
public sealed record FleetFilterChoice
{
    /// <summary>Gets what the dropdown shows.</summary>
    public required string Name { get; init; }

    /// <summary>Gets what a vehicle must satisfy to survive this choice.</summary>
    public required Func<TransportVehicle, bool> Matches { get; init; }
}
