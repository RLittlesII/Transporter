using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using LanguageExt;
using ReactiveMarbles.Mvvm;
using Rocket.Surgery.Airframe;
using Transponder.Model;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;

namespace Transponder.Features.Fleet.ViewModels;

/// <summary>The detail pane: the selected vehicle, read through the lines the description names (B-013, B-014).</summary>
/// <remarks>
/// Thin by construction: every value is derived from two streams through <c>AsValue</c>, so no setter
/// raises another property and nothing here is assigned. Which fields a vehicle has, what they are
/// called and which unit each reads in are the description's (`fleet-pipeline` B-043), so this type
/// names no subclass and converts nothing (B-019, B-022); it pairs a line's name with its cell.
/// </remarks>
public sealed class FleetDetailViewModel : RxObject, IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="FleetDetailViewModel"/> class.</summary>
    /// <param name="selected">The element the fleet has selected, absent when nothing is (B-014).</param>
    /// <param name="description">The live source's description, whose detail lines the pane reads (`fleet-pipeline` B-043).</param>
    /// <param name="schedulers">Where the values are marshalled, by constructor so a test advances it.</param>
    public FleetDetailViewModel(
        IObservable<Option<TrackedVehicle>> selected,
        IObservable<FleetSourceDescription> description,
        ISchedulerProvider schedulers)
    {
        var shown = selected.Select(static element => element.Map(static tracked => tracked.Vehicle));

        _isEmpty = shown
            .Select(static vehicle => vehicle.IsNone)
            .AsValue(_ => RaisePropertyChanged(nameof(IsEmpty)), schedulers.UserInterfaceThread, static () => true)
            .DisposeWith(_garbage);
        _title = shown
            .Select(static vehicle => vehicle.Match(static some => some.Label, static () => string.Empty))
            .AsValue(_ => RaisePropertyChanged(nameof(Title)), schedulers.UserInterfaceThread, static () => string.Empty)
            .DisposeWith(_garbage);
        _rows = shown
            .CombineLatest(
                description.Select(static described => described.Detail),
                static (vehicle, lines) => vehicle.Match(some => Lines(some, lines), static () => []))
            .AsValue(_ => RaisePropertyChanged(nameof(Rows)), schedulers.UserInterfaceThread, static () => [])
            .DisposeWith(_garbage);
    }

    /// <summary>Gets a value indicating whether the pane has nothing to show (B-014).</summary>
    public bool IsEmpty => _isEmpty.Value;

    /// <summary>Gets the pane's heading: the vehicle's label, empty when there is none.</summary>
    public string Title => _title.Value;

    /// <summary>Gets the pane's lines, one per detail line the description names, empty when nothing is selected (B-013).</summary>
    public IReadOnlyList<FleetDetailRow> Rows => _rows.Value;

    /// <inheritdoc/>
    public void Dispose() => _garbage.Dispose();

    /// <summary>Each line's name beside its cell for one vehicle; the cell is the description's, already formatted.</summary>
    /// <param name="vehicle">The vehicle shown.</param>
    /// <param name="lines">The description's detail lines.</param>
    /// <returns>The pane's rows, in the description's order.</returns>
    private static IReadOnlyList<FleetDetailRow> Lines(TransportVehicle vehicle, IReadOnlyList<FleetColumn> lines) =>
        [.. lines.Select(line => new FleetDetailRow(line.Name, line.Value(vehicle)))];

    private readonly CompositeDisposable _garbage = [];
    private readonly IValueBinder<bool> _isEmpty;
    private readonly IValueBinder<string> _title;
    private readonly IValueBinder<IReadOnlyList<FleetDetailRow>> _rows;
}
