using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Flurl.Http.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.OpenSky.Configuration;

namespace Transporter.Integrations.OpenSky.Authentication;

/// <summary>
/// Obtains an OAuth2 client-credentials token and holds it until it expires.
/// </summary>
/// <remarks>
/// The refresh is logged and the token is not: B-027 bans a token, a client id or a client secret
/// from a log line. Expiry is read from the provider's own <c>expires_in</c> rather than assumed
/// to be thirty minutes, and trimmed by <see cref="Margin"/> so a token does not expire in flight.
/// Time comes from the injected scheduler provider, never the wall clock — the same background
/// scheduler the client polls on, so a test advances one object (§ 4 row 14).
/// </remarks>
internal sealed class OpenSkyTokenSource : IOpenSkyTokenSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenSkyTokenSource"/> class.
    /// </summary>
    /// <param name="clients">The Flurl client cache the token endpoint is reached through.</param>
    /// <param name="credentials">The client id and secret, validated at startup (B-029).</param>
    /// <param name="logger">Where a refresh is recorded — never what it returned (B-027).</param>
    /// <param name="schedulers">Where time is read from, rather than the wall clock.</param>
    public OpenSkyTokenSource(
        IFlurlClientCache clients,
        IOptions<OpenSkyCredentials> credentials,
        ILogger<OpenSkyTokenSource> logger,
        ISchedulerProvider schedulers)
    {
        _clients = clients;
        _credentials = credentials;
        _logger = logger;
        _schedulers = schedulers;
    }

    /// <summary>The named Flurl client the token endpoint is reached through.</summary>
    internal const string ClientName = "opensky-auth";

    /// <summary>The token endpoint, which is on a different host from the API itself (README.md § "Authentication").</summary>
    internal const string TokenUrl =
        "https://auth.opensky-network.org/auth/realms/opensky-network/protocol/openid-connect/token";

    /// <summary>How early a token is treated as expired, so one does not die between the check and the call.</summary>
    internal static readonly TimeSpan Margin = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public async Task<string> Current(CancellationToken cancellationToken)
    {
        if (_token is { } held && _schedulers.BackgroundThread.Now < _expires - Margin)
        {
            return held;
        }

        var credentials = _credentials.Value;

        var granted = await _clients.Get(ClientName)
            .Request()
            .PostUrlEncodedAsync(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = credentials.ClientId ?? string.Empty,
                    ["client_secret"] = credentials.ClientSecret ?? string.Empty,
                },
                cancellationToken: cancellationToken)
            .ReceiveJson<TokenGrant>();

        _token = granted.AccessToken;
        _expires = _schedulers.BackgroundThread.Now + TimeSpan.FromSeconds(granted.ExpiresIn);

        // B-027.
        _logger.LogDebug(
            "OpenSky token refreshed; it expires in {ExpiresInSeconds} seconds.",
            granted.ExpiresIn);

        return granted.AccessToken;
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        _token = null;
        _expires = DateTimeOffset.MinValue;
    }

    /// <summary>What the token endpoint answers with, named as it names them.</summary>
    private sealed record TokenGrant
    {
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public required int ExpiresIn { get; init; }
    }

    private readonly IFlurlClientCache _clients;
    private readonly IOptions<OpenSkyCredentials> _credentials;
    private readonly ILogger<OpenSkyTokenSource> _logger;
    private readonly ISchedulerProvider _schedulers;
    private string? _token;
    private DateTimeOffset _expires = DateTimeOffset.MinValue;
}
