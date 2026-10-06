using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using Transponder.Model;

namespace Transponder.Tracking;

/// <summary>
/// The pipeline, assembled once over the seam, and the one component that holds the clock
/// (B-042, B-043).
/// </summary>
/// <remarks>
/// A source swap happens below <see cref="ITrackerSource"/>, so nothing here is rebuilt for one and
/// no consumer can see from the stream that it occurred (B-042).
/// </remarks>
internal sealed class FleetTracker : IFleetTracker
{
    /// <summary>Initializes a new instance of the <see cref="FleetTracker"/> class.</summary>
    /// <param name="source">The seam every strategy and the swap decorator adhere to (B-041).</param>
    /// <param name="clock">The observed instant staleness is measured against (B-043, ADR-0007).</param>
    public FleetTracker(ITrackerSource source, IObservedClock clock)
    {
        _clock = clock;
        Fleet = source.Connect()
            .Transform(Mark, _staleAfter.Select(static _ => Unit.Default))
            .RefCount()
            .TakeUntil(_shutdown);
    }

    /// <summary>How long a vehicle may be silent before it is marked, until a caller says otherwise (B-051).</summary>
    internal static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public IObservable<IChangeSet<TrackedVehicle, string>> Fleet { get; }

    /// <inheritdoc/>
    public void StaleAfter(TimeSpan threshold) => _staleAfter.OnNext(threshold);

    /// <inheritdoc/>
    public void Dispose()
    {
        _shutdown.OnNext(Unit.Default);
        _shutdown.OnCompleted();
        _shutdown.Dispose();
        _staleAfter.Dispose();
    }

    /// <summary>Derives the stale mark, never storing it and never reading an ambient clock (B-043).</summary>
    /// <param name="vehicle">The vehicle the seam reported.</param>
    /// <returns>The vehicle, kept rather than removed, carrying the mark (B-051).</returns>
    private TrackedVehicle Mark(TransportVehicle vehicle) =>
        new() { Vehicle = vehicle, IsStale = vehicle.IsStale(_clock.Current, _staleAfter.Value) };

    private readonly IObservedClock _clock;
    private readonly BehaviorSubject<TimeSpan> _staleAfter = new(DefaultStaleAfter);
    private readonly Subject<Unit> _shutdown = new();
}
