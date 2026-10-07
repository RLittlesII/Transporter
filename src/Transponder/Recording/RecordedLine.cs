using System;
using System.Text.Json;

namespace Transponder.Recording;

/// <summary>
/// Reads one recorded line into the instant that paces it and the payload that leaves it, in the
/// shape <see href="../../../.spec/adr/0004-ndjson-recording-format.md">ADR-0004</see> fixes.
/// </summary>
/// <remarks>
/// One reader for the format, because there are two readers of recordings: the pacer that replays
/// one and the startup report that measures one's span (B-026). Two parsers of the same line is the
/// drift ADR-0004 exists to prevent — the second one written is the one that reads a field the
/// recorder stopped writing.
/// </remarks>
internal static class RecordedLine
{
    /// <summary>Reads one recorded line.</summary>
    /// <param name="line">The line as the recording holds it.</param>
    /// <param name="receivedAt">The instant the payload arrived.</param>
    /// <param name="body">The payload, as the provider sent it.</param>
    /// <returns><see langword="true"/> when the line was read; <see langword="false"/> to discard it.</returns>
    /// <remarks>
    /// The two instants ADR-0004 puts on a line have one job each, and this reads only the first:
    /// <c>receivedAt</c> paces playback and never leaves the pacer, while the reported time that
    /// becomes the observed instant stays inside <paramref name="body"/> for whatever substitutes
    /// above (B-009, B-010).
    /// </remarks>
    internal static bool TryRead(string line, out DateTimeOffset receivedAt, out string body)
    {
        receivedAt = default;
        body = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(line);

            if (!document.RootElement.TryGetProperty("receivedAt", out var instant)
                || !document.RootElement.TryGetProperty("body", out var payload)
                || !instant.TryGetDateTimeOffset(out receivedAt))
            {
                return false;
            }

            body = payload.GetRawText();

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // A line whose receivedAt is not a string at all, which TryGetDateTimeOffset throws on.
            return false;
        }
    }
}
