using System;

namespace Transponder.Recording;

/// <summary>
/// The writer in force when nothing is being recorded: it is handed every payload and keeps none.
/// </summary>
/// <remarks>
/// A null object rather than a nullable dependency, so the transport's tap is one unconditional
/// call and there is no branch that could behave differently with recording on. That is B-004
/// made true by construction rather than by assertion — the only difference between recording and
/// not is which implementation the container resolved.
/// </remarks>
internal sealed class UnrecordedPayloads : IRecordingWriter
{
    /// <inheritdoc/>
    public void Write(DateTimeOffset receivedAt, string body)
    {
    }
}
