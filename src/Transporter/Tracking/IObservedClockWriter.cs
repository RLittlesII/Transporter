using System;

namespace Transporter.Tracking;

/// <summary>
/// The write side of the observed clock, named only by an integration. The pair is the
/// enforcement: a consumer holding <see cref="IObservedClock"/> cannot advance time, and the
/// component holding this cannot read it (ADR-0007 decision 1).
/// </summary>
internal interface IObservedClockWriter
{
    /// <summary>
    /// Reports the instant an envelope carried.
    /// </summary>
    /// <param name="instant">The instant the provider reported.</param>
    /// <remarks>
    /// Sets; it does not take the later of the two. A recording that restarts moves time backwards,
    /// and monotonicity is the live source's property rather than the clock's (ADR-0007 decision 2).
    /// </remarks>
    void Observe(DateTimeOffset instant);
}
