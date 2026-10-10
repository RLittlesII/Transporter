using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Transporter.Recording;

/// <summary>
/// Appends one NDJSON line per payload — the provider's bytes verbatim beside the instant they
/// arrived — to the writer it was handed (B-001, B-002).
/// </summary>
/// <remarks>
/// <para>
/// Takes an open <see cref="TextWriter"/> and opens nothing: resolving configuration to a
/// destination happens once, where the recorder is registered, which is the same split
/// <c>adr/0001</c> item 5 makes for the reader. A test hands it a <see cref="StringWriter"/> and
/// needs no file system.
/// </para>
/// <para>
/// The body is spliced in as text rather than being deserialized and written back. That is what
/// B-002 means by verbatim, and it is what keeps a recording replayable after a converter fix
/// (B-014): a payload this code has read and re-emitted is this repository's reading of it, not
/// the payload.
/// </para>
/// </remarks>
internal sealed class RecordingWriter : IRecordingWriter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingWriter"/> class.
    /// </summary>
    /// <param name="destination">The open writer each line is appended to.</param>
    /// <param name="logger">Where a failed write is recorded, since it never reaches the caller.</param>
    public RecordingWriter(TextWriter destination, ILogger<RecordingWriter> logger)
    {
        _destination = destination;
        _logger = logger;
    }

    /// <summary>The instant format ADR-0004's example writes: milliseconds, and UTC as <c>Z</c>.</summary>
    internal const string InstantFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// The line is composed first and written in one call, rather than a call per field. A
    /// recording is line-oriented, so a second write arriving between two of them would produce a
    /// line that is neither payload — and the whole-line write is what makes that impossible
    /// rather than merely unlikely.
    /// </para>
    /// <para>
    /// Flushed per line, because a recorder is stopped with a keystroke and what was already
    /// written has to be usable without a clean close (ADR-0004 § "Decision drivers"). The cost is
    /// one flush per poll, which at a fifteen-second interval is nothing.
    /// </para>
    /// <para>
    /// Every failure is caught and logged rather than left on the task. B-004 makes recording
    /// observationally transparent, and a faulted task awaited by the poll loop would change what
    /// the fleet sees — which is the one thing a tap may not do.
    /// </para>
    /// </remarks>
    public async Task Write(DateTimeOffset receivedAt, string body)
    {
        try
        {
            if (body.AsSpan().IndexOfAny('\r', '\n') >= 0)
            {
                // ADR-0004 fixes one line per payload and B-002 fixes the bytes, and a payload
                // carrying a line break cannot satisfy both. Written anyway, because omitting a
                // payload is what B-002 forbids outright, and logged because the recording that
                // results is one the reader will treat as torn.
                _logger.LogWarning(
                    "A payload carried a line break, so the recording's line-per-payload shape is broken from here on. "
                    + "OpenSky sends compact JSON; a provider that pretty-prints needs ADR-0004 amended.");
            }

            var instant = receivedAt.ToUniversalTime().ToString(InstantFormat, CultureInfo.InvariantCulture);

            await _destination.WriteAsync($"{{\"receivedAt\":\"{instant}\",\"body\":{body}}}\n").ConfigureAwait(false);
            await _destination.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            // B-004.
            _logger.LogWarning(failure, "A payload could not be recorded; the fleet is unaffected and polling continues.");
        }
    }

    private readonly TextWriter _destination;
    private readonly ILogger<RecordingWriter> _logger;
}
