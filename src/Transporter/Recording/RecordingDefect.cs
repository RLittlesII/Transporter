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

    /// <summary>It could not be opened or read, or its stream cannot seek.</summary>
    Unreadable,

    /// <summary>It opened, but holds no readable recorded line.</summary>
    NoReadableLine,

    /// <summary>It spans less than the staleness threshold, so staleness never shows on stage (B-005).</summary>
    TooShort,
}
