using System.Collections.Generic;

namespace Transporter.Tracking.Fleet;

/// <summary>
/// What the live source offers a view: its columns in display order, and what it can be grouped by
/// (B-020).
/// </summary>
/// <remarks>
/// A swap replaces this value and edits no stage, comparer, predicate or key, which is the whole of
/// B-021. It lives under <c>Tracking/</c> rather than <c>Model/</c> because it describes how a
/// source is presented, which is not a domain fact (§ 11 row 4 concern 5).
/// </remarks>
public sealed record FleetSourceDescription
{
    /// <summary>Gets the columns, in display order.</summary>
    public required IReadOnlyList<FleetColumn> Columns { get; init; }

    /// <summary>Gets what this source can be grouped by.</summary>
    public required IReadOnlyList<FleetGrouping> Groupings { get; init; }

    /// <summary>Gets the filter choices this source offers, in the order the control shows them (B-029).</summary>
    /// <remarks>
    /// Offered whether or not any vehicle currently satisfies one: a choice is what the source
    /// admits, not what the data happens to hold. Empty is legal and means the control offers search
    /// alone. The choices the data holds are B-030's, derived from the fleet rather than declared
    /// here.
    /// </remarks>
    public IReadOnlyList<FleetFilterChoice> Filters { get; init; } = [];

    /// <summary>Gets which columns fill a card, every role empty when the source names none (B-036).</summary>
    /// <remarks>Here rather than on the tracker, so a swap replaces the card with the columns it names (B-021).</remarks>
    public FleetCard Card { get; init; } = new();

    /// <summary>Gets the detail pane's lines, in reading order, empty when the source names none (B-043).</summary>
    /// <remarks>
    /// Columns like the grid's, so the pane reads a line as the search reads a cell and names no
    /// subclass: a field only one kind of vehicle reports is read here, in the per-source file a
    /// swap replaces (B-022).
    /// </remarks>
    public IReadOnlyList<FleetColumn> Detail { get; init; } = [];
}
