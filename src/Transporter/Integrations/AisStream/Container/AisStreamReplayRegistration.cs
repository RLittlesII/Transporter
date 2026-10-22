using DynamicData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.AisStream.Configuration;
using Transporter.Recording;
using Transporter.Tracking;
using Transporter.Tracking.Sources;

namespace Transporter.Integrations.AisStream.Container;

/// <summary>Registers a recorded vessel strategy over an opened stream.</summary>
public static class AisStreamReplayRegistration
{
    internal const string Chain = "vessel-replay";

    /// <summary>Adds vessel playback using the provider's own projection.</summary>
    /// <param name="services">The registrations to extend.</param>
    /// <param name="recording">The opened, checked recording.</param>
    /// <param name="configuration">The source's silence threshold.</param>
    /// <returns>The supplied registrations.</returns>
    internal static IServiceCollection AddVesselReplay(
        this IServiceCollection services,
        Stream recording,
        IConfiguration configuration)
    {
        services.AddOptions<AisStreamOptions>().Bind(configuration.GetSection(AisStreamOptions.Section));
        services.TryAddSingleton<VesselSnapshotMapper>();
        services.AddKeyedSingleton<IRecordingPacer>(
            Chain,
            (provider, _) => new RecordingPacer(
                recording,
                provider.GetRequiredService<ISchedulerProvider>(),
                provider.GetRequiredService<ILogger<RecordingPacer>>()));
        services.AddKeyedSingleton(
            Chain,
            static (_, _) => new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi));
        services.AddKeyedSingleton<Func<IDisposable>>(
            Chain,
            static (provider, key) => new VesselReplayPlayback(
                provider.GetRequiredKeyedService<IRecordingPacer>(key),
                provider.GetRequiredKeyedService<SourceCache<VesselSnapshot, string>>(key),
                provider.GetRequiredService<IObservedClockWriter>(),
                provider.GetRequiredService<ISchedulerProvider>()).Start);
        services.AddKeyedSingleton<ITrackerSourceStrategy>(
            Chain,
            static (provider, key) => VesselTrackerSource.FromSubscription(
                provider.GetRequiredKeyedService<Func<IDisposable>>(key),
                provider.GetRequiredKeyedService<SourceCache<VesselSnapshot, string>>(key),
                provider.GetRequiredService<VesselSnapshotMapper>()));
        services.AddSingleton<ITrackerSourceStrategy>(
            static provider => provider.GetRequiredKeyedService<ITrackerSourceStrategy>(Chain));
        services.AddSingleton(static provider => new TrackerSourceEntry(
            provider.GetRequiredKeyedService<ITrackerSourceStrategy>(Chain),
            "Recorded vessels",
            new NoPollStatus(),
            VesselFleetDescription.WithSilentAfter(provider.GetRequiredService<IOptions<AisStreamOptions>>().Value.SilentAfter)));
        return services;
    }
}
