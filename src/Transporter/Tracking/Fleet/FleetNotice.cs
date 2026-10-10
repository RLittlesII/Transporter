using System;

namespace Transporter.Tracking.Fleet;

/// <summary>
/// What the pipeline says happened: a changeset changed something, or the feed went quiet, or it
/// resumed (B-025, B-027).
/// </summary>
/// <remarks>
/// Values, not presentation (§ 4 row 9). No duration, no colour, no severity and no text: that a
/// quiet feed is worth interrupting someone for is a judgement, and <c>fleet-dashboard</c> makes
/// it.
/// </remarks>
public sealed record FleetNotice
{
    /// <summary>Gets what the notice is about.</summary>
    public required FleetNoticeKind Kind { get; init; }

    /// <summary>Gets the observed instant the notice reports, which is never a wall-clock read (B-018).</summary>
    public required DateTimeOffset Instant { get; init; }

    /// <summary>Gets how many vehicles were in the fleet when the notice was raised.</summary>
    public required int Tracked { get; init; }

    /// <summary>Gets how many vehicles the changeset added.</summary>
    public required int Added { get; init; }

    /// <summary>Gets how many it updated.</summary>
    public required int Updated { get; init; }

    /// <summary>Gets how many it removed.</summary>
    public required int Removed { get; init; }
}
