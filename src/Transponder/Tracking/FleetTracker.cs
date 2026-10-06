using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using LanguageExt;
using Transponder.Model;
using Transponder.Tracking.Fleet;
using Unit = System.Reactive.Unit;

namespace Transponder.Tracking;

/// <summary>
/// The pipeline, assembled once over the seam, and the one component that holds the clock
/// (B-042, B-043).
/// </summary>
/// <remarks>
/// A source swap happens below <see cref="ITrackerSource"/>, so nothing here is rebuilt for one and
/// no consumer can see from the stream that it occurred (B-042).
/// </remarks>
internal sealed class FleetTracker : IFleetTracker
{
    /// <summary>Initializes a new instance of the <see cref="FleetTracker"/> class.</summary>
    /// <param name="source">The seam every strategy and the swap decorator adhere to (B-041).</param>
    /// <param name="clock">The observed instant staleness is measured against (B-043, ADR-0007).</param>
    /// <param name="ticks">Every advance of that instant, so silence alone can make a vehicle stale (fleet-pipeline B-018, ADR-0010).</param>
    /// <param name="description">What the live source offers a view, republished on a swap (fleet-pipeline B-020, B-021).</param>
    public FleetTracker(
        ITrackerSource source,
        IObservedClock clock,
        IObservedClockTicks ticks,
        IObservable<FleetSourceDescription> description)
    {
        _clock = clock;
        Fleet = source.Connect()
            .Filter(_predicate)
            .Transform(Mark, Reevaluate(ticks))
            .RefCount()
            .TakeUntil(_shutdown);
        Description = description
            .Replay(1)
            .RefCount()
            .TakeUntil(_shutdown);
        Order = _sortBy
            .CombineLatest(Description, static (chosen, offered) => chosen.IfNone(() => FirstSortable(offered)))
            .Select(static comparer => (IComparer<TrackedVehicle>) new FleetOrder(comparer))
            .DistinctUntilChanged()
            .TakeUntil(_shutdown);
    }

    /// <summary>How long a vehicle may be silent before it is marked, until a caller says otherwise (B-051).</summary>
    internal static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromMinutes(5);

    /// <summary>What is visible until a caller narrows it: everything (fleet-pipeline B-007).</summary>
    internal static readonly Func<TransportVehicle, bool> DefaultPredicate = static _ => true;

    /// <inheritdoc/>
    public IObservable<IChangeSet<TrackedVehicle, string>> Fleet { get; }

    /// <inheritdoc/>
    public IObservable<FleetSourceDescription> Description { get; }

    /// <inheritdoc/>
    public IObservable<IComparer<TrackedVehicle>> Order { get; }

    /// <inheritdoc/>
    public void Filter(Func<TransportVehicle, bool> predicate) => _predicate.OnNext(predicate);

    /// <inheritdoc/>
    public void SortBy(IComparer<TransportVehicle> comparer) => _sortBy.OnNext(Option<IComparer<TransportVehicle>>.Some(comparer));

    /// <inheritdoc/>
    public void StaleAfter(TimeSpan threshold) => _staleAfter.OnNext(threshold);

    /// <inheritdoc/>
    /// <remarks>Idempotent: a container disposing a singleton twice is not an error (fleet-pipeline B-004).</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.OnNext(Unit.Default);
        _shutdown.OnCompleted();
        _shutdown.Dispose();
        _staleAfter.Dispose();
        _predicate.Dispose();
        _sortBy.Dispose();
    }

    /// <summary>The order until a caller chooses one: the description's first sortable column (fleet-pipeline B-009).</summary>
    /// <param name="offered">What the live source offers.</param>
    /// <returns>That column's comparer, or the key's order when the source offers none.</returns>
    private static IComparer<TransportVehicle> FirstSortable(FleetSourceDescription offered) =>
        offered.Columns
            .Select(static column => column.Comparer)
            .Somes()
            .FirstOrDefault(ByKey);

    /// <summary>What makes the pipeline derive the mark again for vehicles nothing new arrived for.</summary>
    /// <param name="ticks">The observed instant's advances.</param>
    /// <returns>A trigger that fires on a new threshold and on every advance of the instant (fleet-pipeline B-017, B-018).</returns>
    /// <remarks>
    /// Re-deriving is the whole treatment: a vehicle goes stale because time moved, not because it
    /// was removed and re-added, which is why B-019 forbids <c>ExpireAfter</c> here.
    /// </remarks>
    private IObservable<Unit> Reevaluate(IObservedClockTicks ticks) =>
        _staleAfter.Select(static _ => Unit.Default)
            .Merge(ticks.Instant.Select(static _ => Unit.Default));

    /// <summary>Derives the stale mark, never storing it and never reading an ambient clock (B-043).</summary>
    /// <param name="vehicle">The vehicle the seam reported.</param>
    /// <returns>The vehicle, kept rather than removed, carrying the mark (B-051).</returns>
    private TrackedVehicle Mark(TransportVehicle vehicle) =>
        new() { Vehicle = vehicle, IsStale = vehicle.IsStale(_clock.Current, _staleAfter.Value) };

    /// <summary>The chosen comparer, read off the published element, with the key as the tie-break (fleet-pipeline B-011).</summary>
    /// <param name="comparer">The order the description offered, over the abstract vehicle (B-010).</param>
    /// <remarks>
    /// Total by construction: two vehicles a column cannot separate are separated by their keys, so
    /// two sorts of an unchanged fleet produce the same sequence and no row swaps places on a poll
    /// that changed nothing.
    /// </remarks>
    private sealed class FleetOrder(IComparer<TransportVehicle> comparer) : IComparer<TrackedVehicle>
    {
        /// <inheritdoc/>
        public int Compare(TrackedVehicle? left, TrackedVehicle? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            var byColumn = comparer.Compare(left.Vehicle, right.Vehicle);

            return byColumn != 0 ? byColumn : ByKey.Compare(left.Vehicle, right.Vehicle);
        }
    }

    /// <summary>The order every comparer falls back to, and the tie-break every one of them gets (fleet-pipeline B-011).</summary>
    private static readonly IComparer<TransportVehicle> ByKey =
        Comparer<TransportVehicle>.Create(static (left, right) => string.CompareOrdinal(left.Key, right.Key));

    private readonly IObservedClock _clock;
    private readonly BehaviorSubject<Func<TransportVehicle, bool>> _predicate = new(DefaultPredicate);
    private readonly BehaviorSubject<Option<IComparer<TransportVehicle>>> _sortBy = new(Option<IComparer<TransportVehicle>>.None);
    private readonly BehaviorSubject<TimeSpan> _staleAfter = new(DefaultStaleAfter);
    private readonly Subject<Unit> _shutdown = new();
    private bool _disposed;
}
