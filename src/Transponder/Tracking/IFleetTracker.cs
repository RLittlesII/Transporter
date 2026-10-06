using System;
using DynamicData;
using Transponder.Model;

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

    /// <summary>Shows only the vehicles the predicate matches (fleet-pipeline B-006, B-007).</summary>
    /// <param name="predicate">What a vehicle must satisfy to be visible; everything is until this is called.</param>
    void Filter(Func<TransportVehicle, bool> predicate);

    /// <summary>Sets how long a vehicle may be silent before it is marked stale (B-051).</summary>
    /// <param name="threshold">How long silence is tolerated; five minutes until this is called.</param>
    void StaleAfter(TimeSpan threshold);
}
