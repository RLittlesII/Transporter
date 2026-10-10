using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Transporter.Integrations.OpenSky.Configuration;

/// <summary>
/// Fails startup when a credential or the bounding box is absent, naming which one.
/// </summary>
/// <remarks>
/// Registered with <c>ValidateOnStart</c>, so B-029's "the application fails at startup" is the
/// host refusing to start rather than the first poll throwing — by which time a presenter is
/// already on stage. A message names the absent setting and never a configured value, which is
/// B-027's ban read against the failure path (§ 4 row 12).
/// </remarks>
internal sealed class OpenSkyConfigurationValidator : IValidateOptions<OpenSkyCredentials>, IValidateOptions<OpenSkyOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OpenSkyCredentials options)
    {
        var absent = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            absent.Add($"{OpenSkyOptions.Section}:{nameof(OpenSkyCredentials.ClientId)}");
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            absent.Add($"{OpenSkyOptions.Section}:{nameof(OpenSkyCredentials.ClientSecret)}");
        }

        return absent.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(Absent("credential", absent));
    }

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OpenSkyOptions options) =>
        options.Box is null
            ? ValidateOptionsResult.Fail(
                Absent("setting", [$"{OpenSkyOptions.Section}:{nameof(OpenSkyOptions.Box)}"])
                + " The bounding box has no default, because a box nobody chose is a demo pointed at open ocean.")
            : ValidateOptionsResult.Success;

    private static string Absent(string kind, IReadOnlyList<string> settings) =>
        $"OpenSky {kind}{(settings.Count == 1 ? " " : "s ")}{string.Join(" and ", settings)} "
        + $"{(settings.Count == 1 ? "is" : "are")} not configured. Supply "
        + $"{(settings.Count == 1 ? "it" : "them")} through user secrets or the environment.";
}
