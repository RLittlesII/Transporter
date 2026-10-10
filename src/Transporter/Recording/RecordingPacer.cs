using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rocket.Surgery.Airframe;

namespace Transporter.Recording;

/// <summary>
/// Reads an opened recording forward and releases each payload when the recorded spacing says it is
/// due, rewinding at the end rather than ending (B-007, B-008, B-012, B-027).
/// </summary>
/// <remarks>
/// <para>
/// Takes an opened stream and resolves nothing: the configured name and root become a file once,
/// where the replay source is registered, which is the split <c>adr/0001</c> item 5 makes and B-027
/// claims. So the one component carrying time-base logic has no file system in it, and a test
/// drives it from a <see cref="MemoryStream"/>.
/// </para>
/// <para>
/// The stream must be seekable, because the loop seeks to its beginning rather than reopening a
/// file this type never opened — it does not own the stream and does not close it. The guard is in
/// the constructor on purpose: a non-seekable stream otherwise fails at the loop boundary, which is
/// minutes into a talk.
/// </para>
/// <para>
/// The payload is answered as the text the line holds and is never deserialized here;
/// <see cref="RecordedLine"/> is what reads a line, and the startup report reads one through the
/// same type rather than a parser of its own. Both halves of B-002's verbatim are then the same
/// bytes end to end — <see cref="RecordingWriter"/> splices the body in as text and this reads
/// it back out as text — and no line type exists for the format to drift between.
/// </para>
/// </remarks>
internal sealed class RecordingPacer : IRecordingPacer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingPacer"/> class.
    /// </summary>
    /// <param name="recording">The opened, seekable recording to read (B-027).</param>
    /// <param name="schedulers">Where the recorded wait is taken, so no test sleeps (§ 4 row 8).</param>
    /// <param name="logger">Where a discarded line is recorded, since B-012 makes it ordinary.</param>
    /// <exception cref="ArgumentException"><paramref name="recording"/> cannot seek.</exception>
    public RecordingPacer(Stream recording, ISchedulerProvider schedulers, ILogger<RecordingPacer> logger)
    {
        if (!recording.CanSeek)
        {
            throw new ArgumentException(
                "A recording is replayed by seeking to its beginning at the end, so the stream has to be seekable.",
                nameof(recording));
        }

        _recording = recording;
        _schedulers = schedulers;
        _logger = logger;
        _reader = new StreamReader(recording, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// The recording holds no readable line, so there is nothing to pace and rewinding would spin.
    /// B-026's startup report is what catches this before replay is selectable; reaching it means a
    /// recording was handed over without going through that report.
    /// </exception>
    /// <remarks>
    /// Each discarded line costs one payload and nothing else (B-012): a recording is a log, so a
    /// final line cut mid-write — the normal result of stopping a rehearsal — and a complete line
    /// that will not parse are the same case.
    /// </remarks>
    public async Task<string> Next(CancellationToken cancellationToken)
    {
        var rewinds = 0;

        while (true)
        {
            var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (line is null)
            {
                if (++rewinds > 1)
                {
                    throw new InvalidOperationException(
                        "The recording holds no readable line, so there is no cadence to replay (B-026).");
                }

                Rewind();
                continue;
            }

            if (!RecordedLine.TryRead(line, out var receivedAt, out var body))
            {
                // B-012.
                _logger.LogDebug("A recorded line could not be read and was discarded; replay continues past it.");
                continue;
            }

            var wait = Wait(receivedAt);

            if (wait > TimeSpan.Zero)
            {
                await _schedulers.BackgroundThread.Sleep(wait, cancellationToken).ConfigureAwait(false);
            }

            _previous = receivedAt;

            return body;
        }
    }

    /// <summary>
    /// How long this payload waits: the gap it arrived after, nothing before the first, and the
    /// recording's last gap across the loop boundary.
    /// </summary>
    /// <param name="receivedAt">The instant this payload arrived.</param>
    /// <returns>The recorded wait.</returns>
    /// <remarks>
    /// There is no recorded interval between a recording's final payload and its first, and
    /// inventing a fixed one is exactly what B-007 forbids — so the most recent observed cadence
    /// stands in, which is the honest answer available.
    /// </remarks>
    private TimeSpan Wait(DateTimeOffset receivedAt)
    {
        if (_rewound)
        {
            _rewound = false;

            return _lastGap;
        }

        if (_previous is not { } previous)
        {
            return TimeSpan.Zero;
        }

        var gap = receivedAt - previous;

        // A recorder writes in poll order, so a gap that is not forward means the recording was
        // assembled by something else; the payload is still released, immediately.
        _lastGap = gap > TimeSpan.Zero ? gap : TimeSpan.Zero;

        return _lastGap;
    }

    /// <summary>Returns to the first payload, keeping everything a consumer holds (B-008).</summary>
    /// <remarks>
    /// Nothing is cleared and nothing is signalled. The first payload is released as an ordinary
    /// one, so a cache above sees the changes between the last payload and the first rather than a
    /// removal of every vehicle, and the stream neither completes nor faults.
    /// </remarks>
    private void Rewind()
    {
        _recording.Seek(0, SeekOrigin.Begin);
        _reader.DiscardBufferedData();
        _rewound = true;
    }

    private readonly Stream _recording;
    private readonly ISchedulerProvider _schedulers;
    private readonly ILogger<RecordingPacer> _logger;
    private readonly StreamReader _reader;
    private DateTimeOffset? _previous;
    private TimeSpan _lastGap;
    private bool _rewound;
}
