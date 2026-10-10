using System;
using System.IO;

namespace Transporter.Recording;

/// <summary>
/// The configured recordings root on disk: one directory a rehearsal writes to and a replay reads
/// from (<c>adr/0001</c> item 2, B-025).
/// </summary>
/// <remarks>
/// <para>
/// Resolution is a combine against the root and nothing else, so neither the root nor the name has
/// to carry an absolute path — the root is relative to the running application unless configuration
/// says otherwise, and the name is the recording's own, as
/// <see href="../../../.spec/adr/0004-ndjson-recording-format.md">ADR-0004</see> writes it.
/// </para>
/// <para>
/// This is the only file system in the replay half. The pacer is handed an opened stream and the
/// report is handed this (B-027), so the components holding the time base and the reporting have no
/// I/O to stub.
/// </para>
/// </remarks>
internal sealed class RecordingLibrary : IRecordingLibrary
{
    /// <summary>Initializes a new instance of the <see cref="RecordingLibrary"/> class.</summary>
    /// <param name="root">The configured recordings root.</param>
    public RecordingLibrary(string root)
    {
        _root = root;
    }

    /// <inheritdoc/>
    public string Resolve(string name) => Path.Combine(_root, name);

    /// <inheritdoc/>
    public bool Holds(string name) => File.Exists(Resolve(name));

    /// <inheritdoc/>
    /// <remarks>
    /// Answers <see langword="null"/> rather than throwing, because an unreadable recording is a
    /// row in the startup report and not the end of the application: the live demo is unaffected
    /// and stopping the host for a missing fallback turns a degraded talk into no talk.
    /// </remarks>
    public Stream? Open(string name)
    {
        try
        {
            return File.OpenRead(Resolve(name));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private readonly string _root;
}
