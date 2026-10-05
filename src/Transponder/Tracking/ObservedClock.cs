using System;

namespace Transponder.Tracking;

/// <summary>
/// The one clock object, behind both of its interfaces. Registered once and aliased to each, so
/// which side a constructor names is what decides whether it can write time (ADR-0007).
/// </summary>
internal sealed class ObservedClock : IObservedClock, IObservedClockWriter
{
    /// <inheritdoc/>
    public DateTimeOffset Current { get; private set; } = DateTimeOffset.MinValue;

    /// <inheritdoc/>
    void IObservedClockWriter.Observe(DateTimeOffset instant) => Current = instant;
}
