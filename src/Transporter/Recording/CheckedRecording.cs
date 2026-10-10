using System;
using System.IO;
using System.Text;

namespace Transporter.Recording;

/// <summary>
/// One configured recording, checked before anything selects it: what it is called, where it
/// resolved to, what is wrong with it, and the stream to replay it from when nothing is (B-026).
/// </summary>
/// <remarks>
/// <para>
/// The check runs where the replay source is registered, which is what makes a defect a startup
/// report rather than a surprise at the swap. A recording that fails it leaves its source
/// unregistered, the same way B-024 leaves a source with no recording named unregistered: the
/// control is absent rather than offering a wrong recording.
/// </para>
/// <para>
/// The stream is opened by the check and handed on, because resolving configuration to a stream
/// happens once (<c>adr/0001</c> item 5) — so the span is measured on the same handle the pacer
/// then reads, and a recording is opened once per run rather than once per loop.
/// </para>
/// </remarks>
internal sealed class CheckedRecording
{
    private CheckedRecording(string source, string name, string path, RecordingDefect defect, TimeSpan span, Stream? payloads)
    {
        Source = source;
        Name = name;
        Path = path;
        Defect = defect;
        Span = span;
        Payloads = payloads;
    }

    /// <summary>Gets the replay source this recording was named for, which is the configuration key.</summary>
    public string Source { get; }

    /// <summary>Gets the recording's file name, as configuration spelled it.</summary>
    public string Name { get; }

    /// <summary>Gets where the name resolved to under the recordings root.</summary>
    public string Path { get; }

    /// <summary>Gets what is wrong with it, which is what the report names.</summary>
    public RecordingDefect Defect { get; }

    /// <summary>Gets how much of the provider's own time it covers, measured from the arrival instants.</summary>
    public TimeSpan Span { get; }

    /// <summary>Gets the opened recording, or <see langword="null"/> when there is nothing to replay.</summary>
    public Stream? Payloads { get; }

    /// <summary>Gets a value indicating whether a source may be registered over this recording.</summary>
    public bool Usable => Defect is RecordingDefect.None;

    /// <summary>
    /// Checks the recording configuration named for one source.
    /// </summary>
    /// <param name="source">The replay source the recording was named for, which is the configuration key.</param>
    /// <param name="name">The configured file name, or <see langword="null"/> when none was named.</param>
    /// <param name="recordings">The recordings root to resolve and open it in.</param>
    /// <param name="shortest">
    /// The span a recording has to cover to be worth replaying, which is the staleness threshold:
    /// a recording shorter than it cannot show an aircraft going quiet (B-005).
    /// </param>
    /// <returns>The row, carrying the opened recording when it is usable.</returns>
    /// <remarks>
    /// An unusable recording is closed here rather than handed back, so a defect costs no handle
    /// for the length of a run.
    /// </remarks>
    internal static CheckedRecording Check(string source, string? name, IRecordingLibrary recordings, TimeSpan shortest)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new CheckedRecording(source, string.Empty, string.Empty, RecordingDefect.Unnamed, TimeSpan.Zero, payloads: null);
        }

        var path = recordings.Resolve(name);

        if (!recordings.Holds(name))
        {
            return new CheckedRecording(source, name, path, RecordingDefect.Absent, TimeSpan.Zero, payloads: null);
        }

        var payloads = recordings.Open(name);

        if (payloads is null || !payloads.CanSeek)
        {
            payloads?.Dispose();

            return new CheckedRecording(source, name, path, RecordingDefect.Unreadable, TimeSpan.Zero, payloads: null);
        }

        var span = Spans(payloads);

        if (span is not { } covered)
        {
            payloads.Dispose();

            return new CheckedRecording(source, name, path, RecordingDefect.Unreadable, TimeSpan.Zero, payloads: null);
        }

        if (covered < shortest)
        {
            payloads.Dispose();

            return new CheckedRecording(source, name, path, RecordingDefect.TooShort, covered, payloads: null);
        }

        return new CheckedRecording(source, name, path, RecordingDefect.None, covered, payloads);
    }

    /// <summary>
    /// How much time the recording covers, from its first readable arrival instant to its last.
    /// </summary>
    /// <param name="payloads">The opened recording, rewound before it is answered.</param>
    /// <returns>The span, or <see langword="null"/> when no line could be read.</returns>
    /// <remarks>
    /// Reads every line, which at a demo's length is cheap and at an hour's recording is not —
    /// <c>adr/0001</c> names this as the thing to revisit if a longer recording ever matters, and
    /// not the naming. Unreadable lines are skipped the way the pacer skips them (B-012), so a
    /// recording cut mid-write still reports the span of what it holds.
    /// </remarks>
    private static TimeSpan? Spans(Stream payloads)
    {
        using var reader = new StreamReader(payloads, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        DateTimeOffset? first = null;
        DateTimeOffset last = default;

        while (reader.ReadLine() is { } line)
        {
            if (!RecordedLine.TryRead(line, out var receivedAt, out _))
            {
                continue;
            }

            first ??= receivedAt;
            last = receivedAt;
        }

        payloads.Seek(0, SeekOrigin.Begin);

        return first is { } beginning ? last - beginning : null;
    }
}
