using System;
using Transporter.Integrations.OpenSky.Model;

namespace Transporter.UnitTests.Container;

/// <summary>
/// The two documents the head packages: its settings, and the developer's user-secrets store.
/// </summary>
/// <remarks>
/// Every value is invented, the credentials above all (<c>aircraft-source</c> § 4 row 12). The
/// packaged values differ from the options' defaults, so a setting read back as packaged was read
/// rather than defaulted. Each value is written once, here, and the documents are composed from
/// it, so a test asserts the value a document carries rather than a copy of it.
/// </remarks>
internal static class TransporterSettingsPayloads
{
    /// <summary>The base URL <see cref="Packaged"/> carries.</summary>
    public const string BaseUrl = "https://opensky.test/api";

    /// <summary>The base URL <see cref="StoreNamingAPackagedSetting"/> carries in its place.</summary>
    public const string StoreBaseUrl = "https://opensky-store.test/api";

    /// <summary>The client id <see cref="Store"/> carries.</summary>
    public const string ClientId = "synthetic-client-id";

    /// <summary>The client secret <see cref="Store"/> carries.</summary>
    public const string ClientSecret = "synthetic-client-secret";

    /// <summary>The polling interval <see cref="Packaged"/> carries.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

    /// <summary>The bounding box <see cref="Packaged"/> carries.</summary>
    public static readonly BoundingBox Box = new()
    {
        LatitudeMinimum = 10.5,
        LongitudeMinimum = -20.5,
        LatitudeMaximum = 11.5,
        LongitudeMaximum = -19.5,
    };

    /// <summary>
    /// <c>appsettings.json</c> as the head packages it: a base URL, an interval and a box, and no
    /// credential (`0047`).
    /// </summary>
    public static readonly string Packaged = FormattableString.Invariant($$"""
        {
          "OpenSky": {
            "BaseUrl": "{{BaseUrl}}",
            "PollInterval": "{{PollInterval:c}}",
            "Box": {
              "LatitudeMinimum": {{Box.LatitudeMinimum}},
              "LongitudeMinimum": {{Box.LongitudeMinimum}},
              "LatitudeMaximum": {{Box.LatitudeMaximum}},
              "LongitudeMaximum": {{Box.LongitudeMaximum}}
            }
          }
        }
        """);

    /// <summary>
    /// <c>secrets.json</c> as <c>dotnet user-secrets set</c> writes it: one flat object whose keys
    /// are configuration paths.
    /// </summary>
    public static readonly string Store = $$"""
        {
          "OpenSky:ClientId": "{{ClientId}}",
          "OpenSky:ClientSecret": "{{ClientSecret}}"
        }
        """;

    /// <summary>
    /// A store that names a setting <see cref="Packaged"/> names too, with another value: the one
    /// arrangement in which the order of the two layers can be read.
    /// </summary>
    public static readonly string StoreNamingAPackagedSetting = $$"""
        {
          "OpenSky:BaseUrl": "{{StoreBaseUrl}}"
        }
        """;
}
