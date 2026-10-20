namespace Transporter.Tracking.Fleet;

/// <summary>
/// The most recent changes the source made, oldest first, with their totals (fleet-pipeline B-039).
/// </summary>
/// <remarks>
/// The totals are summed over <see cref="Notices"/> where the window is built, so a consumer sums
/// nothing (fleet-dashboard B-023, B-037). Values, not presentation, like the notices it holds.
/// </remarks>
public sealed record FleetNoticeWindow
{
    /// <summary>Gets the changes the window holds, oldest first; every one is <see cref="FleetNoticeKind.Updated"/>.</summary>
    public IReadOnlyList<FleetNotice> Notices { get; init; } = [];

    /// <summary>Gets how many vehicles those changes added.</summary>
    public int Added { get; init; }

    /// <summary>Gets how many they updated.</summary>
    public int Updated { get; init; }

    /// <summary>Gets how many they removed.</summary>
    public int Removed { get; init; }
}
