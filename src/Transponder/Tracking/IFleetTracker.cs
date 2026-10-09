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

    /// <summary>Gets the groups the current grouping forms, each carrying its counts (fleet-pipeline B-012, B-014).</summary>
    IObservable<IChangeSet<FleetGroup, string>> Groups { get; }

    /// <summary>Gets the fleet-wide counts, derived from the same stream as the fleet (fleet-pipeline B-015).</summary>
    IObservable<FleetSummary> Summary { get; }

    /// <summary>Gets the live source's columns and groupings (fleet-pipeline B-020).</summary>
    IObservable<FleetSourceDescription> Description { get; }

    /// <summary>Gets the order to bind by: the chosen comparer over the published element, ties broken on the key (fleet-pipeline B-009 - B-011).</summary>
    /// <remarks>
    /// The sort happens in the consumer's <c>SortAndBind</c>, not in a stage here: a sorted changeset
    /// does not survive the transform that derives the mark, so a stage would sort where nobody could
    /// see it (ADR-0009 decision 2, amended 2026-10-06).
    /// </remarks>
    IObservable<IComparer<TrackedVehicle>> Order { get; }

    /// <summary>Gets the observed instant, and every advance of it — one per applied poll, identical data included (fleet-pipeline B-031).</summary>
    /// <remarks>
    /// The clock's own stream, re-published rather than re-derived: the instant is the one the
    /// provider reported, so under replay it comes from the recording and never from a wall clock
    /// (<c>aircraft-source</c> B-003, ADR-0007). It is here because a consumer depends on this seam
    /// and nothing below it (<c>aircraft-source</c> B-041), and it is what tells a consumer a poll
    /// applied a response when <see cref="Notices"/> cannot — a poll whose data was identical
    /// changes nothing and so raises no notice (fleet-pipeline B-025), but it still reports an
    /// instant. Nothing is deduplicated for the same reason: two identical instants are two polls.
    /// </remarks>
    IObservable<DateTimeOffset> Observed { get; }

    /// <summary>Gets the live source's poll status, starting with the one in force (fleet-pipeline B-040).</summary>
    /// <remarks>
    /// Re-published from <see cref="IPollStatus"/>, never produced here: the tracker reads no clock
    /// and runs no timer for it, so a countdown is the view's to draw (ADR-0013). A swap to a source
    /// that does not poll is <see cref="Tracking.PollStatus.None"/> as the next value.
    /// </remarks>
    IObservable<PollStatus> PollStatus { get; }

    /// <summary>Notices, paced by the caller (fleet-pipeline B-025 - B-027).</summary>
    /// <param name="minimumInterval">
    /// The least time between notices; a new value takes effect without rebuilding anything, and
    /// one second applies until a value arrives.
    /// </param>
    /// <returns>
    /// One notice per changeset that changed something, a quiet notice when nothing has arrived for
    /// longer than the staleness threshold, and a resumed notice on the next changeset after one.
    /// </returns>
    /// <remarks>
    /// A method rather than a property, because the pacing is an operator with behaviour to test
    /// and the cap is per consumer: the banner and the toast run at two cadences over one
    /// implementation of the rate cap (§ 4 row 10).
    /// </remarks>
    IObservable<FleetNotice> Notices(IObservable<TimeSpan> minimumInterval);

    /// <summary>Shows only the vehicles the predicate matches (fleet-pipeline B-006, B-007).</summary>
    /// <param name="predicate">What a vehicle must satisfy to be visible; everything is until this is called.</param>
    void Filter(Func<TransportVehicle, bool> predicate);

    /// <summary>Orders the fleet by a comparer the description offers (fleet-pipeline B-009, B-010).</summary>
    /// <param name="comparer">How to order the fleet; the description's first sortable column until this is called.</param>
    void SortBy(IComparer<TransportVehicle> comparer);

    /// <summary>Regroups the fleet under one of the description's groupings (fleet-pipeline B-012).</summary>
    /// <param name="grouping">How to group the fleet; the vehicle's own grouping answer until this is called.</param>
    void GroupBy(FleetGrouping grouping);

    /// <summary>Sets how long a vehicle may be silent before it is marked stale (B-051).</summary>
    /// <param name="threshold">How long silence is tolerated; five minutes until this is called.</param>
    void StaleAfter(TimeSpan threshold);
}
