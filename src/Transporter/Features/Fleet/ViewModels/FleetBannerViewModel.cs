using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveMarbles.Mvvm;
using ReactiveMarbles.PropertyChanged;
using Rocket.Surgery.Airframe;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;

namespace Transporter.Features.Fleet.ViewModels;

/// <summary>
/// The banner and the toast's notices: the latest notice and the window's totals for the banner, the
/// notices worth interrupting for for the toast, each paced by its own interval (B-023 – B-027, B-037).
/// </summary>
/// <remarks>
/// Two subscriptions to <see cref="IFleetTracker.Notices"/>, one per surface, so the banner and the
/// toast run at two cadences over the pipeline's one rate cap (B-026, `fleet-pipeline` § 4 row 10).
/// Nothing here counts, sums or keeps a list: the stage did both (B-023, B-037). No toast type is
/// named; the page renders one from <see cref="Interrupting"/> (B-027).
/// </remarks>
public sealed class FleetBannerViewModel : RxObject, IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="FleetBannerViewModel"/> class.</summary>
    /// <param name="tracker">Where the notices and the window are raised.</param>
    /// <param name="schedulers">Where values surface, by constructor so a test advances it (B-005).</param>
    public FleetBannerViewModel(IFleetTracker tracker, ISchedulerProvider schedulers)
    {
        _latest = tracker
            .Notices(this.WhenChanged(static model => model.BannerInterval))
            .Select(static notice => (FleetNotice?) notice)
            .AsValue(_ => RaisePropertyChanged(nameof(Latest)), schedulers.UserInterfaceThread, static () => null)
            .DisposeWith(_garbage);
        _window = tracker
            .Recent
            .AsValue(_ => RaisePropertyChanged(nameof(Window)), schedulers.UserInterfaceThread, static () => Empty)
            .DisposeWith(_garbage);
        Interrupting = tracker
            .Notices(this.WhenChanged(static model => model.ToastInterval))
            .Where(static notice => notice.Kind != FleetNoticeKind.Updated)
            .ObserveOn(schedulers.UserInterfaceThread);
    }

    /// <summary>Gets the most recent notice at the banner's cadence; null until the first arrives (B-023).</summary>
    /// <remarks>A nullable reference rather than an <c>Option</c>, because a binding cannot read one (`language-ext-usage` § "Where it stops").</remarks>
    public FleetNotice? Latest => _latest.Value;

    /// <summary>Gets the window of recent changes and its totals, counted before the filter (B-037, `fleet-pipeline` B-039).</summary>
    public FleetNoticeWindow Window => _window.Value;

    /// <summary>Gets or sets the least time between the banner's notices, edited on the page while running (B-026).</summary>
    public TimeSpan BannerInterval { get; set => RaiseAndSetIfChanged(ref field, value); } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the least time between the toast's notices, edited separately (B-026).</summary>
    public TimeSpan ToastInterval { get; set => RaiseAndSetIfChanged(ref field, value); } = TimeSpan.FromMinutes(1);

    /// <summary>Gets the notices worth interrupting for — quiet and resumed — at the toast's cadence (B-025).</summary>
    /// <remarks>Never an ordinary update, at any interval. The swap completing is <see cref="FleetViewModel.Swapped"/>.</remarks>
    public IObservable<FleetNotice> Interrupting { get; }

    /// <inheritdoc/>
    public void Dispose() => _garbage.Dispose();

    /// <summary>What the banner's totals read before the tracker publishes a window: no changes.</summary>
    private static readonly FleetNoticeWindow Empty = new();

    private readonly CompositeDisposable _garbage = [];
    private readonly IValueBinder<FleetNotice?> _latest;
    private readonly IValueBinder<FleetNoticeWindow> _window;
}
