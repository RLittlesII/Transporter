using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using Transporter.Model;
using Transporter.Tracking.Fleet;

namespace Transporter.Tracking.Sources;

/// <summary>
/// Selects which strategy is live, as the only registration of <see cref="ITrackerSource"/>
/// (ADR-0011), and the live strategy's poll status with it (B-056).
/// </summary>
internal sealed class SwappingTrackerSource : ITrackerSource, IPollStatus
{
    /// <summary>Initializes a new instance of the <see cref="SwappingTrackerSource"/> class.</summary>
    /// <param name="entries">Every strategy registration paired with a name, in registration order (ADR-0015).</param>
    /// <exception cref="InvalidOperationException">Nothing is registered as a strategy.</exception>
    public SwappingTrackerSource(IEnumerable<TrackerSourceEntry> entries)
    {
        Entries = entries.ToArray();

        if (Entries.Count == 0)
        {
            throw new InvalidOperationException(
                $"No {nameof(TrackerSourceEntry)} is registered, so the live source is nothing. A strategy "
                + $"registered as {nameof(ITrackerSource)} reaches consumers in place of this selector, and the "
                + "swap then silently does nothing (ADR-0011).");
        }

        _selected = new BehaviorSubject<Selection>(new Selection(Entries[0], Connected: true));
    }

    /// <summary>Gets every registered strategy with its name, in registration order.</summary>
    public IReadOnlyList<TrackerSourceEntry> Entries { get; }

    /// <summary>Gets the selected source's description, including its current value for a late subscriber.</summary>
    public IObservable<FleetSourceDescription> Description =>
        _selected.Select(static selection => selection.Entry.Description).DistinctUntilChanged();

    /// <inheritdoc/>
    /// <remarks>
    /// Selected the way <see cref="Connect"/> is. Each entry's seam starts with the status in force, so a
    /// <c>Switch</c> to it reads that status at once and nothing is buffered here; once the outgoing
    /// status is dropped nothing it writes is published (B-056).
    /// </remarks>
    public IObservable<PollStatus> Status =>
        _selected.Select(static selection => selection.Connected
            ? selection.Entry.Status.Status
            : Observable.Never<PollStatus>()).Switch();

    /// <inheritdoc/>
    public IObservable<bool> CanDemandPoll =>
        _selected.Select(static selection => selection.Connected
            ? selection.Entry.Status.CanDemandPoll
            : Observable.Never<bool>()).Switch();

    /// <inheritdoc/>
    /// <remarks>DynamicData's <c>Switch</c>: the outgoing fleet leaves as removes rather than lingering.</remarks>
    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        _selected.Select(static selection => selection.Connected
            ? selection.Entry.Source.Connect()
            : Observable.Never<IChangeSet<TransportVehicle, string>>()).Switch();

    /// <summary>Makes the strategy behind <paramref name="entry"/> the live one.</summary>
    /// <param name="entry">One of <see cref="Entries"/>.</param>
    /// <exception cref="InvalidOperationException">The entry is not one registration made.</exception>
    public void Select(TrackerSourceEntry entry)
    {
        if (!Entries.Contains(entry))
        {
            throw new InvalidOperationException($"'{entry.Name}' is not a registered strategy, so it cannot be made live (B-057).");
        }

        // One selection controls both streams. Disconnect first so outgoing removes are read
        // through the old description; publish the new description before connecting its cache.
        _selected.OnNext(_selected.Value with { Connected = false });
        _selected.OnNext(new Selection(entry, Connected: false));
        _selected.OnNext(new Selection(entry, Connected: true));
    }

    private sealed record Selection(TrackerSourceEntry Entry, bool Connected);

    private readonly BehaviorSubject<Selection> _selected;
}
