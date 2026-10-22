using Transporter.Tracking.Fleet;

namespace Transporter.Tracking.Sources;

/// <summary>
/// One registered strategy, paired with the poll status it writes and the name a swap control shows
/// for it (B-056, B-057, ADR-0015).
/// </summary>
/// <param name="Source">The strategy, by the seam it registered as.</param>
/// <param name="Name">The name a swap control shows; registration's, never the seam's (B-037).</param>
/// <param name="Status">What the strategy's poller reports; <see cref="NoPollStatus"/> for one that does not poll (B-056).</param>
/// <param name="Description">The source's fleet presentation, selected with its vehicles (B-060, ADR-0018).</param>
internal sealed record TrackerSourceEntry(ITrackerSourceStrategy Source, string Name, IPollStatus Status, FleetSourceDescription Description);
