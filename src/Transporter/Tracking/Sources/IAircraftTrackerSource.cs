namespace Transporter.Tracking.Sources;

/// <summary>The aircraft strategy's seam.</summary>
/// <remarks>
/// Empty, and the emptiness is the claim: B-037 bans widening a per-type seam with
/// source-describing members, which is only visible if the interface exists and declares nothing.
/// </remarks>
internal interface IAircraftTrackerSource : ITrackerSourceStrategy;
