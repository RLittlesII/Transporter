using System;
using DynamicData;

namespace Transponder.Tracking.Fleet;

/// <summary>
/// One group under the current grouping: its key, its counts, and its rows as a stream (B-014).
/// </summary>
/// <remarks>
/// The counts are derived from the same stream the fleet is, and the rows are a stream rather than
/// a collection — so a consumer rendering one group binds it and one showing only the counts binds
/// nothing. A group holding its own list of vehicles would be the second store of tracked items
/// B-002 forbids.
/// </remarks>
public sealed record FleetGroup
{
    /// <summary>Gets the value every vehicle in the group answers the grouping with.</summary>
    public required string Key { get; init; }

    /// <summary>Gets how many vehicles are in the group.</summary>
    public required int Count { get; init; }

    /// <summary>Gets how many of them were stale when the group was derived.</summary>
    public required int StaleCount { get; init; }

    /// <summary>Gets the group's rows, for a consumer that renders them.</summary>
    public required IObservable<IChangeSet<TrackedVehicle, string>> Vehicles { get; init; }
}
