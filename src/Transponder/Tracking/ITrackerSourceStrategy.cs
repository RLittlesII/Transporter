namespace Transponder.Tracking;

/// <summary>
/// What a source registers as, so the container can hand the selector every strategy without
/// handing it itself (ADR-0011).
/// </summary>
/// <remarks>
/// Empty, like the per-type seams below it: it adds nothing to <see cref="ITrackerSource"/> and
/// exists only so that the type consumers resolve and the type a strategy registers as are two
/// different things.
/// </remarks>
internal interface ITrackerSourceStrategy : ITrackerSource;
