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
    /// <param name="strategies">Every source registered as <see cref="ITrackerSourceStrategy"/>.</param>
    /// <exception cref="InvalidOperationException">Nothing is registered as a strategy.</exception>
    public SwappingTrackerSource(IEnumerable<ITrackerSourceStrategy> strategies)
    {
        _strategies = strategies.ToArray();

        if (_strategies.Length == 0)
        {
            throw new InvalidOperationException(
                $"No {nameof(ITrackerSourceStrategy)} is registered, so the live source is nothing. A strategy "
                + $"registered as {nameof(ITrackerSource)} reaches consumers in place of this selector, and the "
                + "swap then silently does nothing (ADR-0011).");
        }

        _selected = new BehaviorSubject<ITrackerSource>(_strategies[0]);
    }

    /// <inheritdoc/>
    /// <remarks>DynamicData's <c>Switch</c>: the outgoing fleet leaves as removes rather than lingering.</remarks>
    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        _selected.Select(static source => source.Connect()).Switch();

    /// <summary>Makes the strategy behind <typeparamref name="TStrategy"/> the live one.</summary>
    /// <typeparam name="TStrategy">The per-type seam naming it.</typeparam>
    public void Select<TStrategy>()
        where TStrategy : ITrackerSource => Select(typeof(TStrategy));

    /// <summary>Makes the strategy behind <paramref name="strategy"/> the live one.</summary>
    /// <param name="strategy">The per-type seam naming it.</param>
    /// <exception cref="InvalidOperationException">No registered strategy adheres to that seam, or more than one does.</exception>
    /// <remarks>The overload a message carries, since a message cannot carry a type parameter.</remarks>
    public void Select(Type strategy) => _selected.OnNext(_strategies.Single(strategy.IsInstanceOfType));

    private readonly ITrackerSourceStrategy[] _strategies;
    private readonly BehaviorSubject<ITrackerSource> _selected;
}
