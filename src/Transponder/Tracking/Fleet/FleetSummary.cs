namespace Transponder.Tracking.Fleet;

/// <summary>
/// The fleet-wide counts, derived from the same stream as the fleet and changing with it (B-015).
/// </summary>
/// <remarks>
/// A value rather than three streams: every consumer of one of these counts wants the set of them
/// as of one moment, and three streams would let a view show a tracked count from one changeset
/// beside a stale count from the next.
/// </remarks>
public sealed record FleetSummary
{
    /// <summary>Gets how many vehicles are in the fleet.</summary>
    public required int Tracked { get; init; }

    /// <summary>Gets how many of them were stale when the summary was derived.</summary>
    public required int Stale { get; init; }

    /// <summary>Gets how many groups the current grouping produces.</summary>
    public required int Groups { get; init; }
}
