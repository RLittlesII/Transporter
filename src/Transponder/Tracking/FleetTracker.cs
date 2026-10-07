using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using LanguageExt;
using Rocket.Surgery.Airframe;
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
    /// <param name="schedulers">Where the notices' rate cap is timed, which is the one operator here that needs a scheduler at all (fleet-pipeline B-005, B-026).</param>
    /// <param name="description">What the live source offers a view, republished on a swap (fleet-pipeline B-020, B-021).</param>
    public FleetTracker(
        ITrackerSource source,
        IObservedClock clock,
        IObservedClockTicks ticks,
        ISchedulerProvider schedulers,
        IObservable<FleetSourceDescription> description)
    {
        _clock = clock;
        _ticks = ticks;
        _schedulers = schedulers;
        _arrivals = source.Connect().Filter(_predicate).RefCount();
        Fleet = _arrivals
            .Transform(Mark, Reevaluate(ticks))
            .RefCount()
            .TakeUntil(_shutdown);
        Groups = Fleet
            .GroupWithImmutableState(Grouped, _groupBy.Select(static _ => Unit.Default))
            .Transform(Group)
            .RefCount()
            .TakeUntil(_shutdown);
        Summary = Groups
            .QueryWhenChanged(static groups => new FleetSummary
            {
                Tracked = groups.Items.Sum(static group => group.Count),
                Stale = groups.Items.Sum(static group => group.StaleCount),
                Groups = groups.Count,
            })
            .DistinctUntilChanged()
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

    /// <summary>The least time between notices until a caller paces them (fleet-pipeline B-026).</summary>
    internal static readonly TimeSpan DefaultNoticeInterval = TimeSpan.FromSeconds(1);

    /// <summary>What is visible until a caller narrows it: everything (fleet-pipeline B-007).</summary>
    internal static readonly Func<TransportVehicle, bool> DefaultPredicate = static _ => true;

    /// <summary>
    /// How the fleet is grouped until a caller chooses a grouping: by the answer the vehicle itself
    /// gives (fleet-pipeline B-013).
    /// </summary>
    /// <remarks>
    /// The tracker holds this rather than waiting for the description, for the reason it holds the
    /// default predicate: a grouping that arrived only with a description would leave the groups
    /// empty until one did, and no test of the pipeline could tell that from a fleet with no groups.
    /// </remarks>
    internal static readonly FleetGrouping DefaultGrouping = new()
    {
        Name = "Group",
        Key = static vehicle => vehicle.GroupKey,
    };

    /// <inheritdoc/>
    public IObservable<IChangeSet<TrackedVehicle, string>> Fleet { get; }

    /// <inheritdoc/>
    public IObservable<IChangeSet<FleetGroup, string>> Groups { get; }

    /// <inheritdoc/>
    public IObservable<FleetSummary> Summary { get; }

    /// <inheritdoc/>
    public IObservable<FleetSourceDescription> Description { get; }

    /// <inheritdoc/>
    public IObservable<IComparer<TrackedVehicle>> Order { get; }

    /// <inheritdoc/>
    public void Filter(Func<TransportVehicle, bool> predicate) => _predicate.OnNext(predicate);

    /// <inheritdoc/>
    public void SortBy(IComparer<TransportVehicle> comparer) => _sortBy.OnNext(Option<IComparer<TransportVehicle>>.Some(comparer));

    /// <inheritdoc/>
    public void GroupBy(FleetGrouping grouping) => _groupBy.OnNext(grouping);

    /// <inheritdoc/>
    public void StaleAfter(TimeSpan threshold) => _staleAfter.OnNext(threshold);

    /// <inheritdoc/>
    /// <remarks>
    /// Built per subscription and held in no field, which is what keeps an idle tracker idle: a
    /// tracker-held arrival timer would keep the shared chain's reference count above zero for ever
    /// and defeat the teardown B-028 claims (§ 12 finding 1 of the specification). Silence is
    /// therefore reported while somebody is listening and not otherwise, which is the whole of what
    /// that costs.
    /// </remarks>
    public IObservable<FleetNotice> Notices(IObservable<TimeSpan> minimumInterval) =>
        Observable.Defer(Raised)
            .Sample(Pace(minimumInterval))
            .TakeUntil(_shutdown);

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
        _groupBy.Dispose();
    }

    /// <summary>The order until a caller chooses one: the description's first sortable column (fleet-pipeline B-009).</summary>
    /// <param name="offered">What the live source offers.</param>
    /// <returns>That column's comparer, or the key's order when the source offers none.</returns>
    private static IComparer<TransportVehicle> FirstSortable(FleetSourceDescription offered) =>
        offered.Columns
            .Select(static column => column.Comparer)
            .Somes()
            .FirstOrDefault(ByKey);

    /// <summary>Counts what a changeset from the seam did, which is what a notice reports (fleet-pipeline B-025).</summary>
    /// <param name="state">What the previous trigger left behind.</param>
    /// <param name="changeset">The changeset that arrived.</param>
    /// <param name="instant">The observed instant it arrived at.</param>
    /// <returns>The state, carrying a notice when the changeset changed something.</returns>
    /// <remarks>
    /// A changeset carrying no change raises none, which is what makes silence mean "nothing moved"
    /// rather than "a poll happened". The first changeset after a quiet spell is a
    /// <see cref="FleetNoticeKind.Resumed"/> rather than an <see cref="FleetNoticeKind.Updated"/>,
    /// so a consumer that announced the silence can say it is over (B-027).
    /// </remarks>
    private static NoticeState Arrived(NoticeState state, IChangeSet<TransportVehicle, string> changeset, DateTimeOffset instant)
    {
        if (changeset.Adds + changeset.Updates + changeset.Removes == 0)
        {
            return state with { Arrived = instant, Notice = Option<FleetNotice>.None };
        }

        var tracked = state.Tracked + changeset.Adds - changeset.Removes;

        return new NoticeState
        {
            Tracked = tracked,
            Arrived = instant,
            Quiet = false,
            Notice = new FleetNotice
            {
                Kind = state.Quiet ? FleetNoticeKind.Resumed : FleetNoticeKind.Updated,
                Instant = instant,
                Tracked = tracked,
                Added = changeset.Adds,
                Updated = changeset.Updates,
                Removed = changeset.Removes,
            },
        };
    }

    /// <summary>Reports silence once, when the observed instant has run past the threshold (fleet-pipeline B-027).</summary>
    /// <param name="state">What the previous trigger left behind.</param>
    /// <param name="instant">The observed instant the clock has reached.</param>
    /// <param name="threshold">How long silence is tolerated.</param>
    /// <returns>The state, carrying a quiet notice the first time the gap exceeds the threshold.</returns>
    /// <remarks>
    /// Once, because the flag is what a further advance reads: a notice per tick would make silence
    /// a stream of its own and tell a consumer nothing it did not know after the first.
    /// </remarks>
    private static NoticeState Advanced(NoticeState state, DateTimeOffset instant, TimeSpan threshold)
    {
        if (state.Quiet || instant - state.Arrived <= threshold)
        {
            return state with { Notice = Option<FleetNotice>.None };
        }

        return state with
        {
            Quiet = true,
            Notice = new FleetNotice
            {
                Kind = FleetNoticeKind.Quiet,
                Instant = instant,
                Tracked = state.Tracked,
                Added = 0,
                Updated = 0,
                Removed = 0,
            },
        };
    }

    /// <summary>Builds one group's value: its key, its counts, and its rows as a stream (fleet-pipeline B-014).</summary>
    /// <param name="grouped">The group as the operator formed it.</param>
    /// <returns>The group.</returns>
    /// <remarks>
    /// Both counts are read off the grouping the operator produced, which is the same stream the
    /// fleet is derived from — never by enumerating a collection a consumer bound.
    /// </remarks>
    private FleetGroup Group(IGrouping<TrackedVehicle, string, string> grouped) =>
        new()
        {
            Key = grouped.Key,
            Count = grouped.Count,
            StaleCount = grouped.Items.Count(static tracked => tracked.IsStale),
            Vehicles = Fleet.Filter(tracked => string.Equals(Grouped(tracked), grouped.Key, StringComparison.Ordinal)),
        };

    /// <summary>How a vehicle answers the grouping in force (fleet-pipeline B-012).</summary>
    /// <param name="tracked">The published element.</param>
    /// <returns>The group key.</returns>
    /// <remarks>
    /// Read from the subject on every evaluation rather than captured, so a new grouping is a value
    /// arriving at the stage: the operator's regrouper re-evaluates membership and nothing is
    /// rebuilt or re-subscribed.
    /// </remarks>
    private string Grouped(TrackedVehicle tracked) => _groupBy.Value.Key(tracked.Vehicle);

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

    /// <summary>The notices before they are paced, with the state a quiet spell needs (fleet-pipeline B-025 – B-027).</summary>
    /// <returns>One notice per changeset that changed something, plus the quiet and resumed ones.</returns>
    /// <remarks>
    /// Counted from the stage above the stale mark, because that stage carries what the seam
    /// reported and the mark's re-derivation is not an arrival: a clock tick re-marks silent
    /// vehicles, and counting those as changes would announce the passage of time as new data —
    /// and would mean a quiet spell could never be detected, since the thing that detects it would
    /// itself be producing changesets.
    /// </remarks>
    private IObservable<FleetNotice> Raised() =>
        Observable.Defer(() =>
        {
            var state = NoticeState.Idle(_clock.Current);

            return _arrivals
                .Select(changeset => (Changeset: Option<IChangeSet<TransportVehicle, string>>.Some(changeset), Instant: _clock.Current))
                .Merge(_ticks.Instant.Select(static instant => (Changeset: Option<IChangeSet<TransportVehicle, string>>.None, Instant: instant)))
                .Scan(state, (carried, trigger) => trigger.Changeset.Match(
                    changeset => Arrived(carried, changeset, trigger.Instant),
                    () => Advanced(carried, trigger.Instant, _staleAfter.Value)))
                .SelectMany(static carried => carried.Notice.ToArray());
        });

    /// <summary>The cap a caller paces the notices with, defaulted until one arrives (fleet-pipeline B-026).</summary>
    /// <param name="minimumInterval">The least time between notices.</param>
    /// <returns>A tick per window, switching when the caller changes the interval.</returns>
    /// <remarks>
    /// The sampler switches and the notices do not, which is what "without rebuilding anything"
    /// requires: switching the source instead would tear down the state a quiet spell is tracked
    /// in every time a consumer changed its cadence.
    /// </remarks>
    private IObservable<long> Pace(IObservable<TimeSpan> minimumInterval) =>
        minimumInterval
            .StartWith(DefaultNoticeInterval)
            .DistinctUntilChanged()
            .Select(interval => Observable.Interval(interval, _schedulers.BackgroundThread))
            .Switch();

    /// <summary>What a notice is derived from: the counts so far, when the last changeset arrived, and whether silence has been reported.</summary>
    /// <remarks>
    /// Per subscription, carried through a <c>Scan</c> rather than held in a field — a field would
    /// be shared between two consumers pacing the notices differently, and the second to subscribe
    /// would read the first's quiet spell.
    /// </remarks>
    private sealed record NoticeState
    {
        /// <summary>Gets how many vehicles the changesets so far leave in the fleet.</summary>
        public required int Tracked { get; init; }

        /// <summary>Gets the observed instant the last changeset arrived at.</summary>
        public required DateTimeOffset Arrived { get; init; }

        /// <summary>Gets a value indicating whether silence has already been reported.</summary>
        public required bool Quiet { get; init; }

        /// <summary>Gets the notice this trigger raised, if it raised one.</summary>
        public required Option<FleetNotice> Notice { get; init; }

        /// <summary>The state a subscription starts in: nothing counted, and the clock as it stands.</summary>
        /// <param name="instant">The observed instant when the subscription was made.</param>
        /// <returns>The seed.</returns>
        public static NoticeState Idle(DateTimeOffset instant) =>
            new() { Tracked = 0, Arrived = instant, Quiet = false, Notice = Option<FleetNotice>.None };
    }

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
    private readonly IObservedClockTicks _ticks;
    private readonly ISchedulerProvider _schedulers;
    private readonly IObservable<IChangeSet<TransportVehicle, string>> _arrivals;
    private readonly BehaviorSubject<Func<TransportVehicle, bool>> _predicate = new(DefaultPredicate);
    private readonly BehaviorSubject<Option<IComparer<TransportVehicle>>> _sortBy = new(Option<IComparer<TransportVehicle>>.None);
    private readonly BehaviorSubject<FleetGrouping> _groupBy = new(DefaultGrouping);
    private readonly BehaviorSubject<TimeSpan> _staleAfter = new(DefaultStaleAfter);
    private readonly Subject<Unit> _shutdown = new();
    private bool _disposed;
}
