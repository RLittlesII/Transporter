namespace Transporter.Tracking;

/// <summary>What a source registers as, so the container can enumerate strategies without the selector (ADR-0011).</summary>
internal interface ITrackerSourceStrategy : ITrackerSource;
