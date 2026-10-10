using System;
using System.Threading.Tasks;

namespace Transporter.Recording;

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
    /// <remarks>
    /// Completed rather than asynchronous: there is no file, so there is nothing to await. The tap
    /// still awaits it, which is what keeps the call site identical either way.
    /// </remarks>
    public Task Write(DateTimeOffset receivedAt, string body) => Task.CompletedTask;
}
