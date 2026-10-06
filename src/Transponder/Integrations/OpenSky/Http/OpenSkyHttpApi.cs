using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Flurl.Http.Configuration;
using Microsoft.Extensions.Logging;
using Transponder.Integrations.OpenSky.Authentication;
using Transponder.Integrations.OpenSky.Contracts;

namespace Transponder.Integrations.OpenSky.Http.Api;

/// <summary>
/// The contract over HTTP: the one implementation per transport B-007 counts, taking the Flurl
/// client cache, the token source and the logger the credit header is written to.
/// </summary>
internal sealed class OpenSkyHttpApi : IOpenSkyApi
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenSkyHttpApi"/> class.
    /// </summary>
    /// <param name="clients">The Flurl client cache this transport asks for its named client.</param>
    /// <param name="tokens">The one place a bearer token is obtained and held.</param>
    /// <param name="logger">Where the remaining credit is written, at debug (B-027).</param>
    public OpenSkyHttpApi(IFlurlClientCache clients, IOpenSkyTokenSource tokens, ILogger<OpenSkyHttpApi> logger)
    {
        _clients = clients;
        _tokens = tokens;
        _logger = logger;
    }

    /// <summary>The named Flurl client the provider's API is reached through.</summary>
    internal const string ClientName = "opensky";

    /// <summary>The header carrying how long the provider asked to be left alone for (B-028).</summary>
    internal const string RetryAfterHeader = "X-Rate-Limit-Retry-After-Seconds";

    /// <summary>The header carrying what is left of the day's credits (B-027).</summary>
    internal const string RemainingHeader = "X-Rate-Limit-Remaining";

    /// <inheritdoc/>
    async Task<OpenSkyStatesResponse> IOpenSkyApi.GetStates(
        double lamin,
        double lomin,
        double lamax,
        double lomax,
        bool extended,
        CancellationToken cancellationToken)
    {
        using var first = await Send(lamin, lomin, lamax, lomax, extended, true, cancellationToken);

        if (first.StatusCode != (int) HttpStatusCode.Unauthorized)
        {
            return await Read(first);
        }

        // B-026, second half.
        _tokens.Invalidate();

        using var retried = await Send(lamin, lomin, lamax, lomax, extended, false, cancellationToken);

        return await Read(retried);
    }

    private static TimeSpan RetryAfter(IFlurlResponse response) =>
        response.Headers.TryGetFirst(RetryAfterHeader, out var header) && int.TryParse(header, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;

    /// <summary>
    /// Sends one request, carrying the current token.
    /// </summary>
    /// <param name="lamin">The box's lower latitude bound.</param>
    /// <param name="lomin">The box's lower longitude bound.</param>
    /// <param name="lamax">The box's upper latitude bound.</param>
    /// <param name="lomax">The box's upper longitude bound.</param>
    /// <param name="extended">Whether to ask for the category element.</param>
    /// <param name="allowUnauthorized">
    /// Whether a <c>401</c> comes back as a response to inspect rather than as an exception. True
    /// for the first attempt, which may refresh and retry; false for that retry, so a second
    /// rejection is Flurl's exception instead of another refresh — retrying again would spend a
    /// credit to learn what the first retry already said (B-026).
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The response, whether or not it carries a payload.</returns>
    private async Task<IFlurlResponse> Send(
        double lamin,
        double lomin,
        double lamax,
        double lomax,
        bool extended,
        bool allowUnauthorized,
        CancellationToken cancellationToken)
    {
        var request = _clients.Get(ClientName)
            .Request("states", "all")
            .SetQueryParams(new { lamin, lomin, lamax, lomax, extended = extended ? 1 : 0 })
            .WithOAuthBearerToken(await _tokens.Current(cancellationToken))
            .AllowHttpStatus((int) HttpStatusCode.TooManyRequests);

        if (allowUnauthorized)
        {
            request = request.AllowHttpStatus((int) HttpStatusCode.Unauthorized);
        }

        return await request.GetAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Reads a response the provider has answered with, logging what is left of the budget.
    /// </summary>
    /// <param name="response">The response to read.</param>
    /// <returns>The payload, when there is one.</returns>
    /// <exception cref="OpenSkyThrottledException">
    /// The provider answered <c>429</c>. It is inspected here, where the headers still are, so the
    /// seconds <c>X-Rate-Limit-Retry-After-Seconds</c> asked for travel on the exception rather
    /// than being lost with the response (B-028, ADR-0008). Every other non-2xx status is already
    /// an exception because nothing allowed it.
    /// </exception>
    /// <remarks>
    /// <c>X-Rate-Limit-Remaining</c> is written at debug on every poll (B-027): the header is the
    /// only place a burn rate is visible before it bites, and any call style that keeps the body
    /// and throws the response away loses it (§ 4 row 9).
    /// </remarks>
    private async Task<OpenSkyStatesResponse> Read(IFlurlResponse response)
    {
        // B-027.
        if (response.Headers.TryGetFirst(RemainingHeader, out var remaining))
        {
            _logger.LogDebug("OpenSky reports {RemainingCredits} credits remaining.", remaining);
        }

        // B-028.
        return response.StatusCode == (int) HttpStatusCode.TooManyRequests
            ? throw new OpenSkyThrottledException(RetryAfter(response))
            : await response.GetJsonAsync<OpenSkyStatesResponse>();
    }

    private readonly IFlurlClientCache _clients;
    private readonly IOpenSkyTokenSource _tokens;
    private readonly ILogger<OpenSkyHttpApi> _logger;
}
