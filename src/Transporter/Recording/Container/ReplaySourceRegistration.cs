using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Transporter.Integrations.AisStream.Configuration;
using Transporter.Integrations.AisStream.Container;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Tracking;

namespace Transporter.Recording.Container;

/// <summary>Checks both named recordings and registers each usable source once.</summary>
public static class ReplaySourceRegistration
{
    /// <summary>Adds the named replay sources beneath one startup report.</summary>
    /// <param name="services">The registrations to extend.</param>
    /// <param name="configuration">The recording names and root.</param>
    /// <returns>The supplied registrations.</returns>
    public static IServiceCollection AddReplaySources(this IServiceCollection services, IConfiguration configuration) =>
        services.AddReplaySources(
            configuration,
            new RecordingLibrary(
                configuration[$"{RecordingOptions.Section}:{nameof(RecordingOptions.Root)}"] ?? RecordingOptions.DefaultRoot));

    /// <summary>Adds replay sources over a supplied recording library.</summary>
    /// <param name="services">The registrations to extend.</param>
    /// <param name="configuration">The recording names and source thresholds.</param>
    /// <param name="recordings">The root to resolve and open names in.</param>
    /// <returns>The supplied registrations.</returns>
    internal static IServiceCollection AddReplaySources(
        this IServiceCollection services,
        IConfiguration configuration,
        IRecordingLibrary recordings)
    {
        services.AddOpenSkyShared(configuration);
        services.AddOptions<ReplayOptions>().Bind(configuration.GetSection(ReplayOptions.Section));

        var aircraft = CheckedRecording.Check(
            nameof(ReplayOptions.Aircraft),
            configuration[$"{ReplayOptions.Section}:{nameof(ReplayOptions.Aircraft)}"],
            recordings,
            FleetTracker.DefaultStaleAfter);
        var vessels = CheckedRecording.Check(
            nameof(ReplayOptions.Vessels),
            configuration[$"{ReplayOptions.Section}:{nameof(ReplayOptions.Vessels)}"],
            recordings,
            configuration.GetValue(
                $"{AisStreamOptions.Section}:{nameof(AisStreamOptions.SilentAfter)}",
                new AisStreamOptions().SilentAfter));

        services.AddSingleton<IReadOnlyList<CheckedRecording>>([aircraft, vessels]);
        services.AddSingleton<ReplayStartupReport>();
        services.AddSingleton<IHostedService>(static provider => provider.GetRequiredService<ReplayStartupReport>());

        if (aircraft.Payloads is { } aircraftPayloads)
        {
            services.AddAircraftReplay(aircraftPayloads);
        }

        if (vessels.Payloads is { } vesselPayloads)
        {
            services.AddVesselReplay(vesselPayloads, configuration);
        }

        return services;
    }
}
