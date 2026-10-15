using System;
using System.Reactive.Disposables;
using LanguageExt;
using ReactiveMarbles.Mvvm;
using Rocket.Surgery.Airframe;
using Transporter.Tracking;

namespace Transporter.Features.Fleet.ViewModels;

/// <summary>When the next poll is due, and how long the provider is refusing, as the tracker published them (B-036).</summary>
/// <remarks>
/// One value per status and never a tick: the countdown is the view's animation toward
/// <see cref="NextDue"/> (B-017). No timer, no clock and no arithmetic over instants here (B-031).
/// </remarks>
public sealed class FleetPollViewModel : RxObject, IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="FleetPollViewModel"/> class.</summary>
    /// <param name="tracker">Where the poll status is re-published (`fleet-pipeline` B-040).</param>
    /// <param name="schedulers">Where the binder surfaces values, by constructor so a test advances it (B-005).</param>
    public FleetPollViewModel(IFleetTracker tracker, ISchedulerProvider schedulers) =>
        _status = tracker
            .PollStatus
            .AsValue(
                _ =>
                {
                    RaisePropertyChanged(nameof(NextDue));
                    RaisePropertyChanged(nameof(RefusedFor));
                    RaisePropertyChanged(nameof(IsPolling));
                },
                schedulers.UserInterfaceThread,
                static () => PollStatus.None)
            .DisposeWith(_garbage);

    /// <summary>Gets the instant the next poll is due; null for a source that does not poll.</summary>
    /// <remarks>Nullable rather than an <c>Option</c>, because a binding cannot read one (`language-ext-usage` § "Where it stops").</remarks>
    public DateTimeOffset? NextDue => _status.Value.NextDue.ToNullable();

    /// <summary>Gets the interval the provider asked for while it refuses; null otherwise.</summary>
    public TimeSpan? RefusedFor => _status.Value.RefusedFor.ToNullable();

    /// <summary>Gets a value indicating whether the source has said anything about polling; false hides the countdown and the badge together.</summary>
    public bool IsPolling => !_status.Value.Equals(PollStatus.None);

    /// <inheritdoc/>
    public void Dispose() => _garbage.Dispose();

    private readonly CompositeDisposable _garbage = [];
    private readonly IValueBinder<PollStatus> _status;
}
