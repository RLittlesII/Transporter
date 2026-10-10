using System;
using Transporter.Model;

namespace Transporter.Tracking.Fleet;

/// <summary>One way the live source can be grouped: what the chooser shows, and how a group is read (B-020).</summary>
/// <remarks>The key is a selector over the abstract vehicle for the reason <see cref="FleetColumn.Value"/> is (B-022).</remarks>
public sealed record FleetGrouping
{
    /// <summary>Gets what the grouping chooser shows — "Origin country", "Category".</summary>
    public required string Name { get; init; }

    /// <summary>Gets how a vehicle answers this grouping.</summary>
    public required Func<TransportVehicle, string> Key { get; init; }
}
