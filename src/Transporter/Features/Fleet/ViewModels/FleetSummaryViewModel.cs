using System;
using System.Reactive.Disposables;
using ReactiveMarbles.Mvvm;
using Rocket.Surgery.Airframe;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;

namespace Transporter.Features.Fleet.ViewModels;

/// <summary>The summary strip: the tracker's counts, projected and never recounted (B-015).</summary>
/// <remarks>
/// One binder over the one <see cref="FleetSummary"/> value, so the three counts a view shows are
/// always from the same changeset — the reason the tracker publishes them as one value
/// (`fleet-pipeline` B-015). Nothing here reads the bound collection.
/// </remarks>
public sealed class FleetSummaryViewModel : RxObject, IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="FleetSummaryViewModel"/> class.</summary>
    /// <param name="tracker">Where the counts are derived.</param>
    /// <param name="schedulers">Where the binder surfaces values, by constructor so a test advances it (B-005).</param>
    public FleetSummaryViewModel(IFleetTracker tracker, ISchedulerProvider schedulers) =>
        _summary = tracker
            .Summary
            .AsValue(
                _ =>
                {
                    RaisePropertyChanged(nameof(Tracked));
                    RaisePropertyChanged(nameof(Stale));
                    RaisePropertyChanged(nameof(Groups));
                },
                schedulers.UserInterfaceThread,
                static () => Nothing)
            .DisposeWith(_garbage);

    /// <summary>Gets how many vehicles the tracker holds.</summary>
    public int Tracked => _summary.Value.Tracked;

    /// <summary>Gets how many of them the tracker marked stale.</summary>
    public int Stale => _summary.Value.Stale;

    /// <summary>Gets how many groups the current grouping produces.</summary>
    public int Groups => _summary.Value.Groups;

    /// <inheritdoc/>
    public void Dispose() => _garbage.Dispose();

    /// <summary>What the strip reads before the first summary: an empty fleet, which is what there is.</summary>
    private static readonly FleetSummary Nothing = new() { Tracked = 0, Stale = 0, Groups = 0 };

    private readonly CompositeDisposable _garbage = [];
    private readonly IValueBinder<FleetSummary> _summary;
}
