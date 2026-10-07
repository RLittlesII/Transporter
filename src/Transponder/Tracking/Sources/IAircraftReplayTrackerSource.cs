namespace Transponder.Tracking.Sources;

/// <summary>The replay chain's seam, empty for the reason <see cref="IAircraftTrackerSource"/> is.</summary>
/// <remarks>
/// It exists because <see cref="SwappingTrackerSource.Select(System.Type)"/> resolves a strategy
/// by the one registration a seam is an instance of: two aircraft strategies with no seam to tell
/// them apart would both answer <see cref="IAircraftTrackerSource"/> and make every swap throw.
/// </remarks>
internal interface IAircraftReplayTrackerSource : ITrackerSourceStrategy;
