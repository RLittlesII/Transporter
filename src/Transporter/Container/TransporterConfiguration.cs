using System.IO;
using Microsoft.Extensions.Configuration;

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
    /// <returns>The configuration <c>AddTransporter</c> is handed.</returns>
    public static IConfiguration Compose(Stream settings, Stream? secrets)
    {
        var builder = new ConfigurationBuilder().AddJsonStream(settings);

        if (secrets is not null)
        {
            builder.AddJsonStream(secrets);
        }

        return builder.Build();
    }
}
