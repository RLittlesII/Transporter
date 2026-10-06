using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveMarbles.Mvvm;
using Rocket.Surgery.Airframe;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;

namespace Transponder.Features.Fleet.ViewModels;

/// <summary>
/// The grid's view model: the one collection, and the live source's columns (B-005, B-007).
/// </summary>
/// <remarks>
/// The tracker publishes changesets and owns no collection, so the <c>ObserveOn</c>, the
/// <c>SortAndBind</c> and the disposal of that subscription are this type's (ADR-0009).
/// </remarks>
public sealed class FleetViewModel : RxObject, IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="FleetViewModel"/> class.</summary>
    /// <param name="tracker">The pipeline over the tracker seam, and all this view model depends on (B-020).</param>
    /// <param name="schedulers">Where the binding is marshalled, by constructor so a test advances it (B-005).</param>
    public FleetViewModel(IFleetTracker tracker, ISchedulerProvider schedulers)
    {
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
            })
            .DisposeWith(_garbage);
    }

    /// <summary>Gets the collection the grid binds, materialised here and in no other place (B-005).</summary>
    public ReadOnlyObservableCollection<TrackedVehicle> Fleet => _fleet;

    /// <summary>Gets the live source's columns, in the order it published them (B-007).</summary>
    public IReadOnlyList<FleetColumn> Columns { get; private set => RaiseAndSetIfChanged(ref field, value); } = [];

    /// <summary>Gets what the live source can be grouped by (B-007).</summary>
    public IReadOnlyList<FleetGrouping> Groupings { get; private set => RaiseAndSetIfChanged(ref field, value); } = [];

    /// <summary>Gets what the filter control offers; item 0037 fills it (B-009).</summary>
    public IReadOnlyList<FleetFilterChoice> Filters { get; private set => RaiseAndSetIfChanged(ref field, value); } = [];

    /// <summary>Gets or sets what the user typed; what it composes into is item 0037's (B-009).</summary>
    public string SearchText { get; set => RaiseAndSetIfChanged(ref field, value); } = string.Empty;

    /// <inheritdoc/>
    public void Dispose() => _garbage.Dispose();

    private readonly CompositeDisposable _garbage = [];
    private readonly ReadOnlyObservableCollection<TrackedVehicle> _fleet;
}
