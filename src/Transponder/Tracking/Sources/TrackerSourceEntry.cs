namespace Transponder.Tracking.Sources;

/// <summary>One registered strategy, paired with the name a swap control shows for it (B-057, ADR-0015).</summary>
/// <param name="Source">The strategy, by the seam it registered as.</param>
/// <param name="Name">The name a swap control shows; registration's, never the seam's (B-037).</param>
internal sealed record TrackerSourceEntry(ITrackerSourceStrategy Source, string Name);
