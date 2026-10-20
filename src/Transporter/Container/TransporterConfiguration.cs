using Microsoft.Extensions.Configuration;
using Transporter.Recording;

namespace Transporter.Container;

/// <summary>
/// Composes the configuration the application is built from.
/// </summary>
public static class TransporterConfiguration
{
    /// <summary>
    /// Layers the developer's user-secrets store over the packaged settings (B-058).
    /// </summary>
    /// <param name="settings">The packaged <c>appsettings.json</c>.</param>
    /// <param name="secrets">The store packaged beside it, or <see langword="null"/> when the build found none.</param>
    /// <param name="defaultRecordingRoot">The platform recordings root, when supplied.</param>
    /// <returns>The configuration <c>AddTransporter</c> is handed.</returns>
    public static IConfiguration Compose(Stream settings, Stream? secrets, string? defaultRecordingRoot = null)
    {
        var builder = new ConfigurationBuilder();

        if (defaultRecordingRoot is not null)
        {
            builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RecordingOptions.Section}:{nameof(RecordingOptions.Root)}"] = defaultRecordingRoot,
            });
        }

        builder.AddJsonStream(settings);

        if (secrets is not null)
        {
            builder.AddJsonStream(secrets);
        }

        return builder.Build();
    }
}
