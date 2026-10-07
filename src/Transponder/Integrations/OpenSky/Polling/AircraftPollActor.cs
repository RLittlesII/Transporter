using System;
using System.Threading;
using Akka.Actor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rocket.Surgery.Airframe;
using Transponder.Messages;

namespace Transponder.Integrations.OpenSky.Polling;

/// <summary>
/// Performs a poll on demand, and refuses one inside the configured interval so demand cannot make
/// the source spend credits faster than its own cadence (B-053, ADR-0012).
/// </summary>
/// <remarks>
/// The window is measured from the last poll the source actually made, cadence polls included: a
/// window measured from the last demanded one spends a credit for a snapshot the cadence was about
/// to fetch anyway.
/// </remarks>
internal sealed class AircraftPollActor : ReceiveActor
{
    /// <summary>Initializes a new instance of the <see cref="AircraftPollActor"/> class.</summary>
    /// <param name="poll">The poller, and when it last called the provider.</param>
    /// <param name="options">The interval the refusal window is (B-050).</param>
    /// <param name="schedulers">Where time is read, so a test advances it (§ 4 row 14).</param>
    /// <param name="logger">Where a refusal and a failed poll are recorded.</param>
    public AircraftPollActor(
        IDemandedPoll poll,
        IOptions<OpenSkyOptions> options,
        ISchedulerProvider schedulers,
        ILogger<AircraftPollActor> logger) =>
        ReceiveAsync<DemandPoll>(async _ =>
        {
            var interval = options.Value.PollInterval;
            var now = schedulers.BackgroundThread.Now;

            if (poll.LastPoll.Match(last => now - last < interval, static () => false))
            {
                // B-053: silent to the caller, and the cadence is left alone.
                logger.LogDebug(
                    "A demanded poll fell inside the {PollIntervalSeconds}-second interval and was refused.",
                    interval.TotalSeconds);

                return;
            }

            try
            {
                await poll.PollNow(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                // A demanded poll that failed is this message's loss and not the actor's: a restart
                // would drop the queue behind it, and the caller was told rather than asked.
                logger.LogWarning(failure, "A demanded OpenSky poll failed; the cadence is unaffected.");
            }
        });
}
