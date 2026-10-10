namespace Transporter.UnitTests.Container;

/// <summary>
/// The two documents the head packages: its settings, and the developer's user-secrets store.
/// </summary>
/// <remarks>
/// Every value is invented, the credentials above all (<c>aircraft-source</c> § 4 row 12). The
/// packaged values differ from the options' defaults, so a setting read back as packaged was read
/// rather than defaulted.
/// </remarks>
public static class TransporterSettingsPayloads
{
    /// <summary>The base URL <see cref="Packaged"/> carries.</summary>
    public const string BaseUrl = "https://opensky.test/api";

    /// <summary>The client id <see cref="Store"/> carries.</summary>
    public const string ClientId = "synthetic-client-id";

    /// <summary>The client secret <see cref="Store"/> carries.</summary>
    public const string ClientSecret = "synthetic-client-secret";

    /// <summary>
    /// <c>appsettings.json</c> as the head packages it: a base URL, an interval and a box, and no
    /// credential (`0047`).
    /// </summary>
    public static readonly string Packaged = """
        {
          "OpenSky": {
            "BaseUrl": "https://opensky.test/api",
            "PollInterval": "00:00:20",
            "Box": {
              "LatitudeMinimum": 10.5,
              "LongitudeMinimum": -20.5,
              "LatitudeMaximum": 11.5,
              "LongitudeMaximum": -19.5
            }
          }
        }
        """;

    /// <summary>
    /// <c>secrets.json</c> as <c>dotnet user-secrets set</c> writes it: one flat object whose keys
    /// are configuration paths.
    /// </summary>
    public static readonly string Store = """
        {
          "OpenSky:ClientId": "synthetic-client-id",
          "OpenSky:ClientSecret": "synthetic-client-secret"
        }
        """;
}
