using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Akka.Actor;
using Akka.Hosting;
using DynamicData;
using LanguageExt;
using ReactiveMarbles.Command;
using ReactiveMarbles.Mvvm;
using ReactiveMarbles.PropertyChanged;
using Rocket.Surgery.Airframe;
using Transporter.Messages;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using IScheduler = System.Reactive.Concurrency.IScheduler;
using Unit = System.Reactive.Unit;

namespace Transporter.Features.Fleet.ViewModels;

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
        _arrivals = tracker.Fleet.Publish().RefCount();
        var targets = registry.TryGet<SwapSource>(out var swapActor)
            ? Observable.FromAsync(token => swapActor.Ask<SwapTargets>(GetSwapTargets.Instance, TargetsWithin, token))
                .Catch(Observable.Return(new SwapTargets([])))
            : Observable.Return(new SwapTargets([]));
        _targets = targets
            .Do(answer => _answered = answer)
            .Select(static answer => (IReadOnlyList<string>) answer.Targets.Select(static target => target.Name).ToArray())
            .AsValue(
                _ =>
                {
                    RaisePropertyChanged(nameof(Targets));
                    RaisePropertyChanged(nameof(HasTargets));
                },
                schedulers.UserInterfaceThread,
                static () => Array.Empty<string>())
            .DisposeWith(_garbage);
        SwapCommand = RxCommand.Create(
                () => SelectedTarget,
                this.WhenChanged(static model => model.SelectedTarget).Select(ValidTarget),
                schedulers.UserInterfaceThread)
            .DisposeWith(_garbage);

        // Armed before the swap window below, which tells the actor: RxCommand notifies its
        // subscribers in order, so an addition the dispatch delivers synchronously is seen here too.
        var swapped = SwapCommand
            .Where(ValidTarget)
            .Select(_ => _arrivals.Where(static changes => changes.Adds != 0).Take(1).Select(static _ => Unit.Default))
            .Switch()
            .Publish();
        Swapped = swapped.ObserveOn(schedulers.UserInterfaceThread);
        swapped.Connect().DisposeWith(_garbage);
        _isSwapping = SwapCommand
            .Where(ValidTarget)
            .Select(position => SwapWindow(_answered.Targets[position], swapActor!, schedulers.UserInterfaceThread))
            .Switch()
            .AsValue(_ => RaisePropertyChanged(nameof(IsSwapping)), schedulers.UserInterfaceThread, static () => false)
            .DisposeWith(_garbage);
        this.WhenChanged(static model => model.SelectedTarget)
            .Where(ValidTarget)
            .Select(static _ => Unit.Default)
            .InvokeCommand(this, x => x.SwapCommand).DisposeWith(_garbage);

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
        _arrivals
            .ObserveOn(schedulers.UserInterfaceThread)
            .SortAndBind(out _fleet, tracker.Order)
            .Subscribe(Follow)
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

        // Skipped once: the first emission is the values at construction, which the description's arrival filters on.
        this.WhenChanged(static model => model.SearchText, static model => model.SelectedFilter, static (_, _) => Unit.Default)
            .Skip(1)
            .Subscribe(_ => Filter())
            .DisposeWith(_garbage);
        this.WhenChanged(static model => model.SelectedGrouping)
            .Where(static grouping => grouping is not null)
            .Subscribe(grouping => tracker.GroupBy(grouping!))
            .DisposeWith(_garbage);
        this.WhenChanged(static model => model.SortColumn)
            .Where(static column => column is not null)
            .Subscribe(column => ChooseColumn(column!))
            .DisposeWith(_garbage);
        ReverseCommand = RxCommand
            .Create(
                () => { SortedColumn.IfSome(sorted => ChooseColumn(sorted.Column)); },
                this.WhenChanged(static model => model.SortedColumn).Select(static sorted => sorted.IsSome),
                schedulers.UserInterfaceThread)
            .DisposeWith(_garbage);
        ClearFilterCommand = RxCommand
            .Create(() => { SelectedFilter = null; }, outputScheduler: schedulers.UserInterfaceThread)
            .DisposeWith(_garbage);
        Detail = new FleetDetailViewModel(
                this.WhenChanged(static model => model.Selected).Select(Prelude.Optional),
                tracker.Description,
                schedulers)
            .DisposeWith(_garbage);
        _observed = tracker
            .Observed
            .AsValue(
                _ => RaisePropertyChanged(nameof(Observed)),
                schedulers.UserInterfaceThread,
                static () => DateTimeOffset.MinValue)
            .DisposeWith(_garbage);
    }

    /// <summary>Gets the collection the grid binds, materialised here and in no other place (B-005).</summary>
    public ReadOnlyObservableCollection<TrackedVehicle> Fleet => _fleet;

    /// <summary>Gets the discovered target names.</summary>
    public IReadOnlyList<string> Targets => _targets.Value;

    /// <summary>Gets whether any target can be chosen.</summary>
    public bool HasTargets => Targets.Count != 0;

    /// <summary>Gets or sets the selected picker position.</summary>
    public int SelectedTarget { get; set => RaiseAndSetIfChanged(ref field, value); } = -1;

    /// <summary>Gets the command that requests the selected source.</summary>
    public RxCommand<Unit, int> SwapCommand { get; }

    /// <summary>Gets whether the swap presentation window is open.</summary>
    public bool IsSwapping => _isSwapping.Value;

    /// <summary>Gets a signal each time a swap chosen here completes: the new source's first addition after the choice (B-025).</summary>
    /// <remarks>
    /// The page turns it into a toast; this type names none (B-027). It waits for the addition with
    /// no deadline, because a swap that never delivers has not completed, and the next choice
    /// replaces the wait.
    /// </remarks>
    public IObservable<Unit> Swapped { get; }

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
    /// <remarks>
    /// Derived, never assigned: read from the tracker through <c>AsValue</c>, never from a wall clock,
    /// so a replay shows the ages it recorded. Reads the minimum until the first poll lands.
    /// </remarks>
    public DateTimeOffset Observed => _observed.Value;

    /// <summary>Gets the detail pane's view model, which the selection drives and nothing else does (B-013, B-014).</summary>
    /// <remarks>
    /// Built here rather than injected: its inputs are this view model's selection, observed rather
    /// than pushed from a setter, and the tracker's description, so a second owner could only
    /// disagree with this one. Disposed with this view model.
    /// </remarks>
    public FleetDetailViewModel Detail { get; }

    /// <summary>Gets or sets the element the grid has selected, null when nothing is (B-013, B-014).</summary>
    /// <remarks>
    /// A binding target, so a plain reference rather than an <c>Option</c> (`language-ext-usage`
    /// § "Where it stops"); the pane observes it through <c>WhenChanged</c> and receives an
    /// <c>Option</c>, so this setter sets one value and nothing else. The pipeline moves it too: a
    /// newer reading replaces it, and the vehicle leaving the collection clears it, so a selection
    /// never outlives what it selected.
    /// </remarks>
    public TrackedVehicle? Selected { get; set => RaiseAndSetIfChanged(ref field, value); }

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
        set => RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets the filter picker's choice, null when the user has cleared it (B-011).</summary>
    /// <remarks>A binding target, so a plain reference (`language-ext-usage` § "Where it stops"); the predicate reads it as an <c>Option</c>.</remarks>
    public FleetFilterChoice? SelectedFilter { get; set => RaiseAndSetIfChanged(ref field, value); }

    /// <summary>Gets the command that clears the filter picker's choice and leaves the search (B-011).</summary>
    public RxCommand<Unit, Unit> ClearFilterCommand { get; }

    /// <summary>Gets which header is active and in which direction, absent until one is chosen (B-012).</summary>
    public Option<(FleetColumn Column, bool Descending)> SortedColumn { get; private set => RaiseAndSetIfChanged(ref field, value); }

    /// <summary>Gets or sets the sort picker's choice, which reaches <see cref="ChooseColumn"/> (B-012).</summary>
    /// <remarks>A picker cannot re-choose the item it holds, so the reverse is <see cref="ReverseCommand"/>.</remarks>
    public FleetColumn? SortColumn { get; set => RaiseAndSetIfChanged(ref field, value); }

    /// <summary>Gets the command that hands the tracker the active column's reverse (B-012).</summary>
    public RxCommand<Unit, Unit> ReverseCommand { get; }

    /// <summary>Gets or sets the grouping picker's choice, null while the tracker keeps the default it seeded (B-012).</summary>
    public FleetGrouping? SelectedGrouping { get; set => RaiseAndSetIfChanged(ref field, value); }

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

    /// <summary>Keeps the selection on the element the collection now holds for it, or clears it (B-014).</summary>
    /// <param name="changes">The changes just bound, so the collection already reflects them.</param>
    /// <remarks>
    /// Read after the bind rather than before it, so a newer reading is selected only once it is
    /// the instance the grid holds. A filter that hides the vehicle removes it from the collection,
    /// and that clears the selection the same way its leaving the fleet does.
    /// </remarks>
    private void Follow(IChangeSet<TrackedVehicle, string> changes)
    {
        foreach (var change in changes)
        {
            if (Selected is not { } selected || !string.Equals(change.Key, selected.Vehicle.Key, StringComparison.Ordinal))
            {
                continue;
            }

            Selected = change.Reason == ChangeReason.Remove ? null : change.Current;
        }
    }

    /// <summary>Hands the tracker the one predicate the two inputs compose into (B-009).</summary>
    private void Filter() => _tracker.Filter(FleetSearch.Composed(SearchText, Columns, Prelude.Optional(SelectedFilter)));

    private bool ValidTarget(int position) => position >= 0 && position < _answered.Targets.Count;

    private IObservable<bool> SwapWindow(SwapTarget target, IActorRef actor, IScheduler scheduler) =>
        Observable.Create<bool>(observer =>
        {
            observer.OnNext(true);
            var window = _arrivals
                .Where(static changes => changes.Adds != 0)
                .Select(static _ => false)
                .Merge(Observable.Timer(SwapClearsAfter, scheduler).Select(static _ => false))
                .Take(1)
                .Subscribe(observer);
            actor.Tell(SwapSource.To(target));

            return window;
        });

    /// <summary>How long the indicator shows before it gives up waiting for a poll that was refused (decisions/0001).</summary>
    /// <remarks>
    /// This Feature's number, chosen by feel and checked by the first rehearsal. Not read from
    /// <c>OpenSkyOptions</c>: the throttle's window is a provider's configuration and this is a
    /// presentation choice, and a view model that named one would be a surface that knew what a poll
    /// costs.
    /// </remarks>
    private static readonly TimeSpan ClearsAfter = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan TargetsWithin = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SwapClearsAfter = TimeSpan.FromSeconds(10);
    private readonly CompositeDisposable _garbage = [];
    private readonly IValueBinder<bool> _isRefreshing;
    private readonly IValueBinder<bool> _isSwapping;
    private readonly IValueBinder<IReadOnlyList<string>> _targets;
    private readonly IObservable<IChangeSet<TrackedVehicle, string>> _arrivals;
    private readonly IValueBinder<DateTimeOffset> _observed;
    private readonly ReadOnlyObservableCollection<TrackedVehicle> _fleet;
    private readonly IFleetTracker _tracker;
    private SwapTargets _answered = new([]);
}
