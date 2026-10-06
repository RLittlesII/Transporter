namespace Transponder.Recording;

/// <summary>
/// Whether a run records, and the one directory recordings live in.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Root"/> is deliberately on this type rather than beside the recording names a
/// replay source reads, because <c>adr/0001</c> item 2 requires a rehearsal to write and a replay
/// to read <em>the same place</em> — two types each carrying a root is two places for one answer,
/// and the second one to be edited is the one that is wrong on stage.
/// </para>
/// <para>
/// <see cref="Enabled"/> defaults to off, so recording is something a rehearsal turns on rather
/// than something every run does. A recording nobody asked for is operational data accumulating
/// on a developer's disk (B-006).
/// </para>
/// </remarks>
internal sealed class RecordingOptions
{
    /// <summary>The configuration section this binds under.</summary>
    internal const string Section = "Recording";

    /// <summary>The directory recordings are written to and read from, unless configuration says otherwise.</summary>
    internal const string DefaultRoot = "recordings";

    /// <summary>
    /// Gets or sets a value indicating whether this run records what it polls. Off unless
    /// configuration turns it on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the directory recordings resolve against, relative to the running
    /// application. Defaulted, unlike the bounding box: a relative <c>recordings/</c> beside the
    /// application is an answer nobody has a stake in, where a box nobody chose is a demo pointed
    /// at open ocean.
    /// </summary>
    public string Root { get; set; } = DefaultRoot;
}
