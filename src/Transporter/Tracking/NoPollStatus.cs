using System;
using System.Reactive.Linq;

namespace Transporter.Tracking;

/// <summary>
/// The poll status of a source that does not poll: <see cref="PollStatus.None"/> at once, and nothing
/// after (fleet-pipeline B-040).
/// </summary>
/// <remarks>
/// It never completes on its own, so completion stays the tracker's disposal and nothing else's
/// (fleet-pipeline B-004).
/// </remarks>
internal sealed class NoPollStatus : IPollStatus
{
    /// <inheritdoc/>
    public IObservable<PollStatus> Status { get; } = Observable.Never<PollStatus>().StartWith(PollStatus.None);
}
