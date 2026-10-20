using System.Reactive.Subjects;
using Transporter.Tracking;

namespace Transporter.Integrations.OpenSky.Polling;

/// <summary>
/// The OpenSky poller's status: when the next poll is due, and the interval a <c>429</c> asked for
/// while it refuses (B-054, B-055, ADR-0013).
/// </summary>
/// <remarks>
/// Written by the snapshot client, which is where a poll's instant and outcome are both known, and
/// read through <see cref="IPollStatus"/> by the swap decorator. A singleton beside the client, so
/// the two meet on one instance. It starts with <see cref="PollStatus.None"/>, which is the seam's
/// contract: a subscriber reads the status in force at once.
/// </remarks>
internal sealed class AircraftPollStatus : IPollStatus
{
    /// <inheritdoc/>
    public IObservable<PollStatus> Status => _status;

    /// <summary>Gets the status in force, which a failed poll keeps the refusal of.</summary>
    internal PollStatus Current => _status.Value;

    /// <summary>Reports the status a poll, or the cadence stopping, decided.</summary>
    /// <param name="status">The status now in force.</param>
    public void Write(PollStatus status) => _status.OnNext(status);

    private readonly BehaviorSubject<PollStatus> _status = new(PollStatus.None);
}
