using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Akka.Actor;
using Akka.Hosting;
using DynamicData;
using LanguageExt;
using ReactiveMarbles.Command;
using ReactiveMarbles.Mvvm;
using Rocket.Surgery.Airframe;
using Transponder.Messages;
using Transponder.Model;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;
using Unit = System.Reactive.Unit;

namespace Transponder.Features.Fleet.ViewModels;

/// <summary>
/// The grid's view model: the one collection, the live source's columns, the four inputs the user
/// changes, and the gesture that asks for a poll (B-005, B-007, B-009 - B-012, B-028).
/// </summary>
/// <remarks>
/// The tracker publishes changesets and owns no collection, so the <c>ObserveOn</c>, the
/// <c>SortAndBind</c> and the disposal of that subscription are this type's (ADR-0009). The inputs
/// go the other way, as calls on the tracker: it owns the subject behind each method and the default
/// it starts at, so this type holds no subject and remembers no default.
/// </remarks>
public sealed class FleetViewModel : RxObject, IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="FleetViewModel"/> class.</summary>
    /// <param name="tracker">The pipeline over the tracker seam, and all this view model depends on (B-020).</param>
    /// <param name="schedulers">Where the binding is marshalled, by constructor so a test advances it (B-005).</param>
    /// <param name="registry">Where the actor a demanded poll is told to is resolved from (B-028, ADR-0012).</param>
    public FleetViewModel(IFleetTracker tracker, ISchedulerProvider schedulers, IActorRegistry registry)
    {
        _tracker = tracker;

        // The scheduler is passed rather than left to default: an unset one is resolved from
        // ReactiveMarbles' service locator, which is the ambient read B-005 forbids.
        RefreshCommand = RxCommand
            .Create(
                () => registry.Get<DemandPoll>().Tell(DemandPoll.Instance),
                outputScheduler: schedulers.UserInterfaceThread)
            .DisposeWith(_garbage);

        _isRefreshing = RefreshCommand
            .Select(_ => tracker
                .Observed
                .Skip(1)
                .Select(static _ => false)
                .Merge(Observable.Timer(ClearsAfter, schedulers.UserInterfaceThread).Select(static _ => false))
                .Take(1)
                .StartWith(true))
            .Switch()
            .AsValue(
                _ => RaisePropertyChanged(nameof(IsRefreshing)),
                schedulers.UserInterfaceThread,
                static () => false)
            .DisposeWith(_garbage);
        tracker
            .Fleet
            .ObserveOn(schedulers.UserInterfaceThread)
            .SortAndBind(out _fleet, tracker.Order)
            .Subscribe()
            .DisposeWith(_garbage);
        tracker
            .Description
            .ObserveOn(schedulers.UserInterfaceThread)
            .Subscribe(description =>
            {
                Columns = description.Columns;
                Groupings = description.Groupings;
                Filters = description.Filters;
                Card = description.Card;
                Filter();
            })
            .DisposeWith(_garbage);
        tracker
            .Observed
            .ObserveOn(schedulers.UserInterfaceThread)
            .Subscribe(instant => Observed = instant)
            .DisposeWith(_garbage);
    }

    /// <summary>Gets the collection the grid binds, materialised here and in no other place (B-005).</summary>
    public ReadOnlyObservableCollection<TrackedVehicle> Fleet => _fleet;

    /// <summary>Gets the command that says a poll is wanted: one <c>Tell</c>, and no answer waited for (B-028).</summary>
    /// <remarks>
    /// Whether a poll happens is the actor's decision — a press inside the polling interval is
    /// refused there (`aircraft-source` B-053) — so this command names no client, awaits nothing and
    /// never <c>Ask</c>s (B-017, ADR-0012). Typed as the command rather than as
    /// <c>ICommand</c>, which it implements for the binding, because it is also the stream of
    /// its own presses and <see cref="IsRefreshing"/> is read from it.
    /// </remarks>
    public RxCommand<Unit, Unit> RefreshCommand { get; }

    /// <summary>Gets a value indicating whether a demanded poll is outstanding (B-028).</summary>
    /// <remarks>
    /// Derived, never assigned: the press opens the window and the next observed instant closes it,
    /// with <see cref="ClearsAfter"/> as the cap for the press that was refused and produced no poll
    /// to report (decisions/0001, `fleet-pipeline` B-031). Driven by the gesture rather than by the
    /// fleet changing, because a poll returning identical data changes nothing — the clause ADR-0012
    /// says an implementation is most likely to get wrong.
    /// </remarks>
    public bool IsRefreshing => _isRefreshing.Value;

    /// <summary>Gets the live source's columns, in the order it published them (B-007).</summary>
    public IReadOnlyList<FleetColumn> Columns { get; private set => RaiseAndSetIfChanged(ref field, value); } = [];

    /// <summary>Gets which of the live source's columns fill a card's roles (B-029, `fleet-pipeline` B-036).</summary>
    /// <remarks>Replaced with the description, so a swap re-lays every card and no markup names a column.</remarks>
    public FleetCard Card { get; private set => RaiseAndSetIfChanged(ref field, value); } = new();

    /// <summary>Gets the instant the provider last reported, which a card's age is measured from (B-031).</summary>
    /// <remarks>Re-published from the tracker, never read from a wall clock, so a replay shows the ages it recorded.</remarks>
    public DateTimeOffset Observed { get; private set => RaiseAndSetIfChanged(ref field, value); } = DateTimeOffset.MinValue;

    /// <summary>Gets what the live source can be grouped by (B-007).</summary>
    public IReadOnlyList<FleetGrouping> Groupings { get; private set => RaiseAndSetIfChanged(ref field, value); } = [];

    /// <summary>Gets what the filter control offers, as the description published it (B-009).</summary>
    /// <remarks>Not composed here: a choice this view model invented would be one a swap could not replace (`fleet-pipeline` B-029).</remarks>
    public IReadOnlyList<FleetFilterChoice> Filters { get; private set => RaiseAndSetIfChanged(ref field, value); } = [];

    /// <summary>Gets or sets what the user typed, which reaches the tracker as half of one predicate (B-009, B-010).</summary>
    /// <remarks>Never null to a binding: an unset box reads as empty, which is also the text that matches everything.</remarks>
    public string SearchText
    {
        get => field ?? string.Empty;
        set
        {
            RaiseAndSetIfChanged(ref field, value);
            Filter();
        }
    }

    /// <summary>Gets or sets the control's choice, absent when the user has cleared it (B-011).</summary>
    /// <remarks>Absent is a value rather than a null, so clearing the choice is a state the predicate reads rather than a case it guards.</remarks>
    public Option<FleetFilterChoice> SelectedFilter
    {
        get;
        set
        {
            RaiseAndSetIfChanged(ref field, value);
            Filter();
        }
    }

    /// <summary>Gets which header is active and in which direction, absent until one is chosen (B-012).</summary>
    public Option<(FleetColumn Column, bool Descending)> SortedColumn { get; private set => RaiseAndSetIfChanged(ref field, value); }

    /// <summary>Gets or sets which grouping the chooser has, absent while the tracker keeps the default it seeded (B-012).</summary>
    public Option<FleetGrouping> SelectedGrouping
    {
        get;
        set
        {
            RaiseAndSetIfChanged(ref field, value);
            value.IfSome(_tracker.GroupBy);
        }
    }

    /// <summary>Hands the tracker a column's comparer, or its reverse when the same column is chosen again (B-012).</summary>
    /// <param name="column">The header the user chose.</param>
    /// <remarks>
    /// A column the description marks unsortable carries no comparer, so choosing it changes
    /// nothing rather than clearing the order the user already has.
    /// </remarks>
    public void ChooseColumn(FleetColumn column)
    {
        if (column is null)
        {
            return;
        }

        column.Comparer.IfSome(comparer =>
        {
            var descending = SortedColumn.Match(
                sorted => ReferenceEquals(sorted.Column, column) && !sorted.Descending,
                static () => false);

            SortedColumn = (column, descending);
            _tracker.SortBy(descending ? Reversed(comparer) : comparer);
        });
    }

    /// <inheritdoc/>
    public void Dispose() => _garbage.Dispose();

    /// <summary>The same order, read the other way (B-012).</summary>
    /// <param name="comparer">The description's comparer.</param>
    /// <returns>Its reverse.</returns>
    private static IComparer<TransportVehicle> Reversed(IComparer<TransportVehicle> comparer) =>
        Comparer<TransportVehicle>.Create((left, right) => comparer.Compare(right, left));

    /// <summary>Hands the tracker the one predicate the two inputs compose into (B-009).</summary>
    private void Filter() => _tracker.Filter(FleetSearch.Composed(SearchText, Columns, SelectedFilter));

    /// <summary>How long the indicator shows before it gives up waiting for a poll that was refused (decisions/0001).</summary>
    /// <remarks>
    /// This Feature's number, chosen by feel and checked by the first rehearsal. Not read from
    /// <c>OpenSkyOptions</c>: the throttle's window is a provider's configuration and this is a
    /// presentation choice, and a view model that named one would be a surface that knew what a poll
    /// costs.
    /// </remarks>
    private static readonly TimeSpan ClearsAfter = TimeSpan.FromSeconds(3);

    private readonly CompositeDisposable _garbage = [];
    private readonly IValueBinder<bool> _isRefreshing;
    private readonly ReadOnlyObservableCollection<TrackedVehicle> _fleet;
    private readonly IFleetTracker _tracker;
}
