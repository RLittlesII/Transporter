namespace Transponder.Integrations.OpenSky;

/// <summary>
/// The OAuth2 client credentials, and nothing else. Separate from <see cref="OpenSkyOptions"/> so
/// that logging the configuration cannot reach a secret: B-027 bans a token, a client id or a
/// client secret from any log line, exception message, fixture or diagnostic.
/// </summary>
/// <remarks>
/// Supplied from user secrets or environment variables and never committed (§ 4 row 12). Both
/// members are nullable because an absent one is the case B-029 is about, and a validator names
/// which is missing at startup rather than a poll failing later with a <c>401</c>.
/// </remarks>
internal sealed class OpenSkyCredentials
{
    /// <summary>Gets or sets the OAuth2 client id.</summary>
    public string? ClientId { get; set; }

    /// <summary>Gets or sets the OAuth2 client secret.</summary>
    public string? ClientSecret { get; set; }
}
