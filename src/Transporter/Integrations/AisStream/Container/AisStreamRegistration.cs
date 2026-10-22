using DynamicData;
using LanguageExt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Transporter.Integrations.AisStream.Configuration;

namespace Transporter.Integrations.AisStream.Container;

public static class AisStreamRegistration
{
    /// <summary>Adds the vessel socket, client and configuration.</summary>
    /// <param name="services">The registrations to extend.</param>
    /// <param name="configuration">The application configuration including user secrets.</param>
    /// <returns>The supplied registrations.</returns>
    public static IServiceCollection AddAisStream(this IServiceCollection services, IConfiguration configuration)
    {
        var defaults = new AisStreamOptions();
        var section = configuration.GetSection(AisStreamOptions.Section);
        services.AddSingleton<IOptions<AisStreamOptions>>(_ => Options.Create(new AisStreamOptions
        {
            ApiKey = section[nameof(AisStreamOptions.ApiKey)] is { } key && !string.IsNullOrWhiteSpace(key) ? key : Option<string>.None,
            Endpoint = section.GetValue(nameof(AisStreamOptions.Endpoint), defaults.Endpoint)!,
            ReconnectAfter = section.GetValue(nameof(AisStreamOptions.ReconnectAfter), defaults.ReconnectAfter),
            SilentAfter = section.GetValue(nameof(AisStreamOptions.SilentAfter), defaults.SilentAfter),
        }));
        services.AddSingleton(static _ => new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi));
        services.AddSingleton<Func<IVesselSocket>>(static _ => static () => new ClientVesselSocket());
        services.AddSingleton<VesselClient>();
        return services;
    }
}
