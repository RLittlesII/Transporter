using System;

namespace Transporter.Tracking;

/// <summary>
/// The read side that notices time moving: the observed instant, and every advance of it
/// (fleet-pipeline B-018, ADR-0010).
/// </summary>
/// <remarks>
/// A third seam beside <see cref="IObservedClock"/> and the write side rather than a member on
/// either, so ADR-0007's read/write split stands unamended and a consumer holding this still cannot
/// advance time. <see cref="IObservedClock.Current"/> alone cannot satisfy B-018: nothing can notice
/// a value being read.
/// </remarks>
public interface IObservedClockTicks
{
    /// <summary>Gets the observed instant, starting with the one in force and then every advance.</summary>
    IObservable<DateTimeOffset> Instant { get; }
}
