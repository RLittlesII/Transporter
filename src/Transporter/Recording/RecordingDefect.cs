namespace Transporter.Recording;

/// <summary>Why a configured recording cannot be replayed, as the startup report names it (B-026).</summary>
internal enum RecordingDefect
{
    /// <summary>Nothing is wrong with it.</summary>
    None,

    /// <summary>Configuration names no recording for that source, so there is nothing to be wrong (B-024).</summary>
    Unnamed,

    /// <summary>The named recording is not under the recordings root.</summary>
    Absent,

    /// <summary>It is there and could not be opened, or holds no line a pacer can read.</summary>
    Unreadable,

    /// <summary>It spans less than the staleness threshold, so staleness never shows on stage (B-005).</summary>
    TooShort,
}
