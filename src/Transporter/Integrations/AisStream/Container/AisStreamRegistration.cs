using System.Globalization;
using DynamicData;
using LanguageExt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.AisStream.Configuration;
using Transporter.Integrations.OpenSky;
using Transporter.Recording;
using Transporter.Tracking;
using Transporter.Tracking.Sources;

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
        services.AddOptions<RecordingOptions>().Bind(configuration.GetSection(RecordingOptions.Section));
        services.AddKeyedSingleton<IRecordingWriter>("vessels", static (provider, _) =>
        {
            var options = provider.GetRequiredService<IOptions<RecordingOptions>>().Value;
            if (!options.Enabled)
            {
                return new UnrecordedPayloads();
            }

            Directory.CreateDirectory(options.Root);
            var name = $"vessels-{DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ", CultureInfo.InvariantCulture)}.ndjson";
            return new RecordingWriter(
                new StreamWriter(Path.Combine(options.Root, name), append: true),
                provider.GetRequiredService<ILogger<RecordingWriter>>());
        });
        services.AddSingleton(static provider => new VesselClient(
            provider.GetRequiredService<Func<IVesselSocket>>(),
            provider.GetRequiredService<SourceCache<VesselSnapshot, string>>(),
            provider.GetRequiredService<IObservedClockWriter>(),
            provider.GetRequiredService<IOptions<AisStreamOptions>>(),
            provider.GetRequiredService<IOptions<OpenSkyOptions>>(),
            provider.GetRequiredService<ISchedulerProvider>(),
            provider.GetRequiredKeyedService<IRecordingWriter>("vessels"),
            provider.GetRequiredService<ILogger<VesselClient>>()));
        services.AddSingleton<VesselSnapshotMapper>();
        services.AddSingleton<IVesselTrackerSource, VesselTrackerSource>();
        services.AddSingleton<ITrackerSourceStrategy>(static provider => provider.GetRequiredService<IVesselTrackerSource>());
        services.AddSingleton(static provider => new TrackerSourceEntry(
            provider.GetRequiredService<IVesselTrackerSource>(),
            "Vessels",
            new NoPollStatus(),
            VesselFleetDescription.WithSilentAfter(provider.GetRequiredService<IOptions<AisStreamOptions>>().Value.SilentAfter)));
        return services;
    }
}
