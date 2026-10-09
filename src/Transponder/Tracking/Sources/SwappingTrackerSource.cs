using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using Transponder.Model;

namespace Transponder.Tracking.Sources;

/// <summary>Selects which strategy is live, as the only registration of <see cref="ITrackerSource"/> (ADR-0011).</summary>
internal sealed class SwappingTrackerSource : ITrackerSource
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

        _selected = new BehaviorSubject<TrackerSourceEntry>(Entries[0]);
    }

    /// <summary>Gets every registered strategy with its name, in registration order.</summary>
    public IReadOnlyList<TrackerSourceEntry> Entries { get; }

    /// <inheritdoc/>
    /// <remarks>DynamicData's <c>Switch</c>: the outgoing fleet leaves as removes rather than lingering.</remarks>
    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        _selected.Select(static entry => entry.Source.Connect()).Switch();

    /// <summary>Makes the strategy behind <paramref name="entry"/> the live one.</summary>
    /// <param name="entry">One of <see cref="Entries"/>.</param>
    /// <exception cref="InvalidOperationException">The entry is not one registration made.</exception>
    public void Select(TrackerSourceEntry entry) =>
        _selected.OnNext(Entries.Contains(entry)
            ? entry
            : throw new InvalidOperationException($"'{entry.Name}' is not a registered strategy, so it cannot be made live (B-057)."));

    private readonly BehaviorSubject<TrackerSourceEntry> _selected;
}
