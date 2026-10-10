using System;
using System.Collections.Generic;
using LanguageExt;
using Transporter.Model;

namespace Transporter.Tracking.Fleet;

/// <summary>
/// One column the live source offers: what a header shows, what a cell reads, and how the column
/// sorts (B-020).
/// </summary>
/// <remarks>
/// The cell is a selector over the abstract vehicle, which is what replaces the downcast ADR-0005
/// item 6 forbids: a value only one source reports reaches a sortable, grouped or filtered column as
/// a member on the base (B-010, B-022). A selector casts only under B-022's exception, inside the
/// per-source description a swap replaces — a card readout's cell is one (B-042).
/// </remarks>
public sealed record FleetColumn
{
    /// <summary>Gets what the header shows.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the cell, already display-formatted.</summary>
    /// <remarks>A string because the pipeline converts no unit and the view formats none (§ 11 row 3).</remarks>
    public required Func<TransportVehicle, string> Value { get; init; }

    /// <summary>Gets how this column sorts, absent when it does not.</summary>
    /// <remarks>Absent rather than null, because unsortable is a fact about the column (B-020).</remarks>
    public Option<IComparer<TransportVehicle>> Comparer { get; init; }
}
