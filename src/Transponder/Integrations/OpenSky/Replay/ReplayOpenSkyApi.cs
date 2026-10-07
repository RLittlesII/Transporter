using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Recording;

namespace Transponder.Integrations.OpenSky.Replay;

/// <summary>
/// The contract over a recording: the second and last implementation of it, one per transport
/// (aircraft-source B-007 as amended). A call completes when the pacer says the next recorded
/// payload is due, so the only wait in the poll loop above it is the recorded one (B-007).
/// </summary>
/// <remarks>
/// <para>
/// It introduces no snapshot client, no cache, no converter and no projection (B-022): the
/// payload is deserialized into the provider's own envelope, whose positional rows read through
/// the converter already attached to <see cref="OpenSkyStateRow"/>, and everything after that is
/// the live chain's — the same client class, the same reader, the same mapper (B-013).
/// </para>
/// <para>
/// The box and the extended flag are read and ignored, because a recording was taken with the
/// ones it was taken with. Honouring them would mean filtering or synthesising a payload, which
/// is a converter of this component's own and B-022's plainest exclusion.
/// </para>
/// </remarks>
internal sealed class ReplayOpenSkyApi : IOpenSkyApi
{
    /// <summary>Initializes a new instance of the <see cref="ReplayOpenSkyApi"/> class.</summary>
    /// <param name="pacer">
    /// Where the next recorded payload comes from, when the recorded spacing says it is due. It
    /// hands back the body as text: the recording holds what the provider sent, so the reader that
    /// runs over it is today's rather than the one in force when the recording was taken (B-014).
    /// </param>
    /// <param name="logger">Where a discarded payload is recorded, at debug, as the pacer records a torn line.</param>
    public ReplayOpenSkyApi(IRecordingPacer pacer, ILogger<ReplayOpenSkyApi> logger)
    {
        _pacer = pacer;
        _logger = logger;
    }

    /// <summary>
    /// How many unreadable payloads in a row end the replay rather than being skipped past.
    /// </summary>
    /// <remarks>
    /// A payload that is a complete line and not a states response costs one payload and nothing
    /// else, which is B-012's rule for a line the pacer cannot read. Skipping needs a bound,
    /// because this chain's interval is zero: a call that always failed would be answered
    /// immediately and asked again, and the loop would spin a core instead of waiting. A thousand
    /// consecutive unreadable payloads is a file that is not a recording, and saying so ends the
    /// chain the way an unreadable recording does.
    /// </remarks>
    internal const int DiscardsBeforeGivingUp = 1_000;

    /// <inheritdoc/>
    /// <exception cref="FormatException">
    /// <see cref="DiscardsBeforeGivingUp"/> payloads in a row could not be read as a states
    /// response.
    /// </exception>
    async Task<OpenSkyStatesResponse> IOpenSkyApi.GetStates(
        double lamin,
        double lomin,
        double lamax,
        double lomax,
        bool extended,
        CancellationToken cancellationToken)
    {
        for (var discarded = 0; discarded < DiscardsBeforeGivingUp; discarded++)
        {
            var body = await _pacer.Next(cancellationToken).ConfigureAwait(false);

            if (Read(body) is { } payload)
            {
                return payload;
            }

            _logger.LogDebug("A recorded payload is not a states response and was discarded; replay continues past it.");
        }

        throw new FormatException(
            $"{DiscardsBeforeGivingUp} recorded payloads in a row could not be read as a states response, so this "
            + "file is not a recording of this provider.");
    }

    /// <summary>Reads one recorded payload, or answers nothing when it cannot be read.</summary>
    /// <param name="body">The payload, as the recording holds it.</param>
    /// <returns>The envelope, or <see langword="null"/> when the payload is not a states response.</returns>
    /// <remarks>
    /// A recorded body is whatever the provider answered with on the day, which includes an error
    /// payload that is valid JSON and carries no rows at all. The recording is a log rather than a
    /// contract, so one such payload is discarded exactly as a torn line is.
    /// </remarks>
    private static OpenSkyStatesResponse? Read(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<OpenSkyStatesResponse>(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private readonly IRecordingPacer _pacer;
    private readonly ILogger<ReplayOpenSkyApi> _logger;
}
