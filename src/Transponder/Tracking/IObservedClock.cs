using System;

namespace Transponder.Tracking;

/// <summary>
/// The instant everything downstream treats as now. It is the instant the live source last
/// reported, never the wall clock: under replay the data's time comes from the recording, and a
/// replayed fleet aged against the wall clock is stale the moment it is loaded
/// (<c>aircraft-source</c> B-003, ADR-0007).
/// </summary>
public interface IObservedClock
{
    /// <summary>
    /// Gets the instant the live source last reported. <see cref="DateTimeOffset.MinValue"/> before
    /// any source has reported one — nothing is stale against it, and the collection is empty then.
    /// </summary>
    DateTimeOffset Current { get; }
}
