using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Transponder.Tracking;

/// <summary>
/// The one clock object, behind both of its interfaces. Registered once and aliased to each, so
/// which side a constructor names is what decides whether it can write time (ADR-0007).
/// </summary>
internal sealed class ObservedClock : IObservedClock, IObservedClockWriter, IObservedClockTicks
{
    /// <inheritdoc/>
    public DateTimeOffset Current { get; private set; } = DateTimeOffset.MinValue;

    /// <inheritdoc/>
    public IObservable<DateTimeOffset> Instant => _instant.AsObservable();

    /// <inheritdoc/>
    void IObservedClockWriter.Observe(DateTimeOffset instant)
    {
        Current = instant;
        _instant.OnNext(instant);
    }

    /// <summary>The instant in force, so a stage subscribing later reads it rather than waiting for the next envelope.</summary>
    private readonly BehaviorSubject<DateTimeOffset> _instant = new(DateTimeOffset.MinValue);
}
