using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using Transponder.Model;

namespace Transponder.Tracking.Sources;

/// <summary>
/// The decorator registered as <see cref="ITrackerSource"/>: it picks which strategy is live and
/// nothing downstream can tell from the stream that the pick changed (B-038, B-039, ADR-0011).
/// </summary>
/// <remarks>
/// Takes the strategies as the container's own enumerable, so adding a source is a registration and
/// never an edit here. It is told what is live and answers nobody who asks, which is what keeps
/// B-038's second clause true.
/// </remarks>
internal sealed class SwappingTrackerSource : ITrackerSource
{
    /// <summary>Initializes a new instance of the <see cref="SwappingTrackerSource"/> class.</summary>
    /// <param name="strategies">Every source registered as <see cref="ITrackerSourceStrategy"/>.</param>
    /// <exception cref="InvalidOperationException">Nothing is registered as a strategy, so there is nothing to be live.</exception>
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
    /// <remarks>
    /// One subscription across every swap: <c>Switch</c> unsubscribes the outgoing strategy's inner
    /// sequence and subscribes the incoming one, so the outgoing poller stops with the subscription
    /// that owned it (B-040) and the pipeline downstream is untouched (B-039, B-042).
    /// </remarks>
    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        _selected.Select(static source => source.Connect()).Switch();

    /// <summary>Makes the strategy behind <typeparamref name="TStrategy"/> the live one.</summary>
    /// <typeparam name="TStrategy">The per-type seam naming the strategy, rather than a kind or a name carried on the seam (B-037).</typeparam>
    public void Select<TStrategy>()
        where TStrategy : ITrackerSource => Select(typeof(TStrategy));

    /// <summary>Makes the strategy behind <paramref name="strategy"/> the live one.</summary>
    /// <param name="strategy">The per-type seam naming the strategy.</param>
    /// <exception cref="InvalidOperationException">No registered strategy adheres to that seam, or more than one does.</exception>
    /// <remarks>
    /// The overload a message carries, because a message cannot carry a type parameter: the actor
    /// that performs the swap is told a seam and tells this.
    /// </remarks>
    public void Select(Type strategy) => _selected.OnNext(_strategies.Single(strategy.IsInstanceOfType));

    private readonly ITrackerSourceStrategy[] _strategies;
    private readonly BehaviorSubject<ITrackerSource> _selected;
}
