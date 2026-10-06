using System;

namespace Transponder.Recording;

/// <summary>
/// Writes one recorded line per payload, in the format
/// <c>ADR-0004</c> fixes (B-001).
/// </summary>
/// <remarks>
/// A tap, not a source: an implementation is handed a payload something else already fetched, so
/// recording opens no socket and spends no credit (B-003). It is also the only thing in the
/// repository that knows the line's shape — a second writer of it would drift the first time the
/// format moved.
/// </remarks>
internal interface IRecordingWriter
{
    /// <summary>
    /// Records one payload beside the instant it arrived.
    /// </summary>
    /// <param name="receivedAt">
    /// The instant the payload arrived, which paces playback and nothing else. It is never the
    /// observed instant — that stays on the provider's own envelope inside
    /// <paramref name="body"/> (ADR-0004 § "Consequences").
    /// </param>
    /// <param name="body">The payload, exactly as the provider sent it.</param>
    /// <remarks>
    /// Never throws. Recording is observationally transparent (B-004), and a write that failed
    /// loudly would make the fleet's behaviour depend on whether a disk had room.
    /// </remarks>
    void Write(DateTimeOffset receivedAt, string body);
}
