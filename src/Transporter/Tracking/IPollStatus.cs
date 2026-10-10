using System;

namespace Transporter.Tracking;

/// <summary>
/// The read side of a source's poll status, starting with the status in force (fleet-pipeline
/// B-040, ADR-0013).
/// </summary>
/// <remarks>
/// Starting with the status in force is this seam's contract, as <see cref="IObservedClockTicks"/>
/// starts with the instant in force: whatever writes it holds the latest, so the tracker keeps no
/// copy of its own that could outlive the one replacing it.
/// </remarks>
public interface IPollStatus
{
    /// <summary>Gets the poll status, starting with the one in force and then every report.</summary>
    IObservable<PollStatus> Status { get; }
}
