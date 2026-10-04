using System.Net;
using Flurl.Http;
using Flurl.Http.Configuration;
using Transponder.Integrations.OpenSky.Contracts;

namespace Transponder.Integrations.OpenSky.Http;

internal sealed class OpenSkyHttpApi : IOpenSkyApi
{
    public OpenSkyHttpApi(IFlurlClientCache clients) => _clients = clients;

    internal const string ClientName = "opensky";

    internal const string RetryAfterHeader = "X-Rate-Limit-Retry-After-Seconds";

    /// <inheritdoc/>
    async Task<OpenSkyStatesResponse> IOpenSkyApi.GetStates(
        double lamin,
        double lomin,
        double lamax,
        double lomax,
        bool extended,
        CancellationToken cancellationToken)
    {
        using var response = await _clients.Get(ClientName)
            .Request("states", "all")
            .SetQueryParams(new { lamin, lomin, lamax, lomax, extended = extended ? 1 : 0 })
            .AllowHttpStatus((int) HttpStatusCode.TooManyRequests)
            .GetAsync(cancellationToken: cancellationToken);

        if (response.StatusCode == (int) HttpStatusCode.TooManyRequests)
        {
            throw new OpenSkyThrottledException(RetryAfter(response));
        }

        return await response.GetJsonAsync<OpenSkyStatesResponse>();
    }

    private static TimeSpan RetryAfter(IFlurlResponse response) =>
        response.Headers.TryGetFirst(RetryAfterHeader, out var header) && int.TryParse(header, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;

    private readonly IFlurlClientCache _clients;
}
