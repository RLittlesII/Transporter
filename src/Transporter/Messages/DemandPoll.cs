namespace Transporter.Messages;

/// <summary>
/// Told to the actor that polls the live source, to say a poll is wanted (`fleet-dashboard` B-028,
/// `aircraft-source` B-053, ADR-0012).
/// </summary>
/// <remarks>
/// <para>
/// Declared here rather than in the integration that receives it, and it is also the key the actor
/// is registered under. A gesture that demanded a poll by naming <c>AircraftPollActor</c> would put
/// a provider's name on the dashboard, and the closing act's swap would leave the button telling an
/// aircraft-shaped actor about a fleet of vessels. A source that polls registers under this key and
/// receives this message; the surface that presses the button knows neither which source is live nor
/// what polling one costs.
/// </para>
/// <para>
/// <c>Messages/</c> is its own namespace rather than <c>Tracking/</c> because a message is not a
/// tracking type: <c>TRN0006</c> lets a view model name a class under <c>Tracking</c> only when
/// <see cref="Transporter.Tracking.IFleetTracker"/> publishes it, and widening that rule for a
/// message would weaken the boundary it exists to hold (decided while building `0058`,
/// 2026-10-07). What lives here is the other direction across the same seam — what a consumer tells
/// an actor, nameable by both sides and by neither layer's internals.
/// </para>
/// <para>
/// No payload: what is demanded is one poll of whatever the live source polls, and who demanded it
/// changes nothing. Whether the poll happens is the actor's answer — the throttle is
/// `aircraft-source` B-053's, and nothing here repeats it.
/// </para>
/// </remarks>
internal sealed class DemandPoll
{
    private DemandPoll()
    {
    }

    /// <summary>Gets the message to tell.</summary>
    public static DemandPoll Instance { get; } = new();
}
