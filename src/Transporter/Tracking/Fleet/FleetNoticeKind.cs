namespace Transporter.Tracking.Fleet;

/// <summary>What a notice is about (B-025, B-027).</summary>
/// <remarks>
/// An enum rather than three record types: every consumer handles all three, so a hierarchy would
/// be matched on at each of them, and a notice carries values rather than behaviour (§ 4 row 9).
/// </remarks>
public enum FleetNoticeKind
{
    /// <summary>A changeset arrived and changed something.</summary>
    Updated,

    /// <summary>Nothing has arrived for longer than the staleness threshold.</summary>
    Quiet,

    /// <summary>The first changeset after a quiet spell.</summary>
    Resumed,
}
