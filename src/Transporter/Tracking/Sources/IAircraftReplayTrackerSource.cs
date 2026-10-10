namespace Transporter.Tracking.Sources;

/// <summary>The replay chain's seam, empty for the reason <see cref="IAircraftTrackerSource"/> is.</summary>
/// <remarks>
/// It exists because registration resolves each strategy by its seam to pair it with its
/// <see cref="TrackerSourceEntry"/>: two aircraft strategies with no seam to tell them apart would
/// both answer <see cref="IAircraftTrackerSource"/>, and one would be registered twice.
/// </remarks>
internal interface IAircraftReplayTrackerSource : ITrackerSourceStrategy;
