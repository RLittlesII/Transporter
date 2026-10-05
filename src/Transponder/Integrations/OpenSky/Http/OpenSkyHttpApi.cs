using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Flurl.Http.Configuration;
using Microsoft.Extensions.Logging;
using Transponder.Integrations.OpenSky.Contracts;

namespace Transponder.Integrations.OpenSky.Http;

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

        // B-026's second half: a token can die before it expires, so a 401 refreshes and retries
        // this request once — and once only. The retry does not allow a 401, so a second rejection
        // is Flurl's exception rather than another refresh: retrying again would spend a credit to
        // learn what the first retry already said.
        _tokens.Invalidate();

        using var retried = await Send(lamin, lomin, lamax, lomax, extended, false, cancellationToken);

        return await Read(retried);
    }

    private static TimeSpan RetryAfter(IFlurlResponse response) =>
        response.Headers.TryGetFirst(RetryAfterHeader, out var header) && int.TryParse(header, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;

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

    private async Task<OpenSkyStatesResponse> Read(IFlurlResponse response)
    {
        // B-027: what is left of the budget, at debug, on every poll. The header is the only place
        // a burn rate is visible before it bites, and it is lost by any call style that keeps the
        // body and throws the response away (§ 4 row 9).
        if (response.Headers.TryGetFirst(RemainingHeader, out var remaining))
        {
            _logger.LogDebug("OpenSky reports {RemainingCredits} credits remaining.", remaining);
        }

        // B-028: the 429 is read here, where the headers are, so the seconds the provider asked for
        // travel on the exception instead of being lost with the response. Every other non-2xx
        // status is already an exception, because nothing allowed it (ADR-0008).
        return response.StatusCode == (int) HttpStatusCode.TooManyRequests
            ? throw new OpenSkyThrottledException(RetryAfter(response))
            : await response.GetJsonAsync<OpenSkyStatesResponse>();
    }

    private readonly IFlurlClientCache _clients;
    private readonly IOpenSkyTokenSource _tokens;
    private readonly ILogger<OpenSkyHttpApi> _logger;
}
