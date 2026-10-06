using System;
using System.Collections.Generic;
using DynamicData;
using Transponder.Model;
using Transponder.Tracking.Fleet;

namespace Transponder.Tracking;

/// <summary>
/// The pipeline over the tracker seam, and the only thing a view model depends on (B-041).
/// </summary>
/// <remarks>
/// It publishes changesets and binds nothing: the one collection is materialised by whoever binds
/// the stream (ADR-0009).
/// </remarks>
public interface IFleetTracker : IDisposable
{
    /// <summary>Gets the fleet, each vehicle carrying its stale mark, shared between subscribers.</summary>
    IObservable<IChangeSet<TrackedVehicle, string>> Fleet { get; }

    /// <summary>Gets the live source's columns and groupings (fleet-pipeline B-020).</summary>
    IObservable<FleetSourceDescription> Description { get; }

    /// <summary>Gets the order to bind by: the chosen comparer over the published element, ties broken on the key (fleet-pipeline B-009 - B-011).</summary>
    /// <remarks>
    /// The sort happens in the consumer's <c>SortAndBind</c>, not in a stage here: a sorted changeset
    /// does not survive the transform that derives the mark, so a stage would sort where nobody could
    /// see it (ADR-0009 decision 2, amended 2026-10-06).
    /// </remarks>
    IObservable<IComparer<TrackedVehicle>> Order { get; }

    /// <summary>Shows only the vehicles the predicate matches (fleet-pipeline B-006, B-007).</summary>
    /// <param name="predicate">What a vehicle must satisfy to be visible; everything is until this is called.</param>
    void Filter(Func<TransportVehicle, bool> predicate);

    /// <summary>Orders the fleet by a comparer the description offers (fleet-pipeline B-009, B-010).</summary>
    /// <param name="comparer">How to order the fleet; the description's first sortable column until this is called.</param>
    void SortBy(IComparer<TransportVehicle> comparer);

    /// <summary>Sets how long a vehicle may be silent before it is marked stale (B-051).</summary>
    /// <param name="threshold">How long silence is tolerated; five minutes until this is called.</param>
    void StaleAfter(TimeSpan threshold);
}
