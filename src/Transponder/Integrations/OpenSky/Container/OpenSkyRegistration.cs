using System;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using DynamicData;
using Flurl.Http.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rocket.Surgery.Airframe;
using Transponder.Integrations.OpenSky.Authentication;
using Transponder.Integrations.OpenSky.Configuration;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Integrations.OpenSky.Http.Api;
using Transponder.Scheduling;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;
using Transponder.Tracking.Sources;

namespace Transponder.Integrations.OpenSky.Container;

/// <summary>
/// Registers the OpenSky integration into a service collection. This is the integration's only public surface:
/// every other type in it is internal, so a consumer names the contract and never an implementation.
/// </summary>
public static class OpenSkyRegistration
{
    /// <summary>
    /// Adds the OpenSky API contract, aliased to the transport that reaches the provider over HTTP, and the
    /// snapshot cache the client writes into. The cache is a plain keyed store with the application's lifetime:
    /// it has no diff policy, no projection and no clock, and the absence of a wrapper is what makes that true
    /// by construction rather than by assertion.
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <param name="configuration">
    /// Where the options and the credentials are read from. The <c>OpenSky</c> section carries the base URL,
    /// the poll interval and the bounding box, and the client id and secret beside them.
    /// </param>
    /// <returns>The same collection, so registration chains.</returns>
    /// <remarks>
    /// <para>
    /// This method is the whole composition of the integration, and it takes configuration rather than
    /// pre-bound options so that there is exactly one of it: an application and a test both call this and
    /// differ only in what configuration they supply. A test that assembled the same graph by hand would be a
    /// second composition to keep in step, and the first production scenario it missed would pass.
    /// </para>
    /// <para>
    /// The options and the credentials are two registrations, not one, and both validate on start: an absent
    /// credential or an absent bounding box stops the application here rather than failing the first poll on
    /// stage (B-029, B-050).
    /// </para>
    /// </remarks>
    public static IServiceCollection AddOpenSky(this IServiceCollection services, IConfiguration configuration)
    {
        var validator = new OpenSkyConfigurationValidator();

        services.AddSingleton<IValidateOptions<OpenSkyOptions>>(validator);
        services.AddSingleton<IValidateOptions<OpenSkyCredentials>>(validator);
        services.AddOptions<OpenSkyOptions>().Bind(configuration.GetSection(OpenSkyOptions.Section)).ValidateOnStart();
        services.AddOptions<OpenSkyCredentials>().Bind(configuration.GetSection(OpenSkyOptions.Section)).ValidateOnStart();

        services.AddSingleton<IFlurlClientCache>(static provider => new FlurlClientCache()
            .Add(OpenSkyHttpApi.ClientName, provider.GetRequiredService<IOptions<OpenSkyOptions>>().Value.BaseUrl)
            .Add(OpenSkyTokenSource.ClientName, OpenSkyTokenSource.TokenUrl));

        services.TryAddSchedulers();

        // One clock object behind two interfaces, registered once and aliased to each: the write side reaches
        // the integration and the read side reaches the tracker, so which one a constructor names is what
        // decides whether it can advance time (ADR-0007).
        services.AddSingleton<ObservedClock>();
        services.AddSingleton<IObservedClock>(static provider => provider.GetRequiredService<ObservedClock>());
        services.AddSingleton<IObservedClockWriter>(static provider => provider.GetRequiredService<ObservedClock>());
        services.AddSingleton<IObservedClockTicks>(static provider => provider.GetRequiredService<ObservedClock>());

        services.AddSingleton<IOpenSkyTokenSource, OpenSkyTokenSource>();
        services.AddSingleton<IOpenSkyApi, OpenSkyHttpApi>();
        services.AddSingleton(static _ => new SourceCache<AircraftSnapshot, string>(static snapshot => snapshot.Icao24));
        services.AddSingleton<AircraftSnapshotClient>();

        // The strategy and its projection, registered behind the per-type seam so nothing resolves the
        // class itself (B-033, B-034). The decorator that selects between strategies is the tracker's
        // registration, not this one's.
        services.AddSingleton<AircraftSnapshotMapper>();
        services.AddSingleton<IAircraftTrackerSource, AircraftTrackerSource>();

        // This line, and one like it per source, is the whole of adding a strategy (ADR-0011). It is
        // deliberately not a registration of ITrackerSource: a strategy registered as the seam
        // consumers resolve reaches them in place of the selector and the swap silently does nothing
        // (B-038, and B-052's composition test is what catches it).
        services.AddSingleton<ITrackerSourceStrategy>(static provider => provider.GetRequiredService<IAircraftTrackerSource>());

        // What the live source offers a view, as a value the swap replaces rather than a stage anyone
        // rebuilds (fleet-pipeline B-020, B-021). A subject rather than Observable.Return so the
        // stream does not complete the moment it is read; who pushes the next description when the
        // source changes is fleet-pipeline B-020 and B-021's, and no claim here carries it.
        services.AddSingleton(static _ => new BehaviorSubject<FleetSourceDescription>(AircraftFleetDescription.Offered));
        services.AddSingleton<IObservable<FleetSourceDescription>>(
            static provider => provider.GetRequiredService<BehaviorSubject<FleetSourceDescription>>());

        return services;
    }

    /// <summary>
    /// Registers the scheduler provider every time-based element here takes, unless the host has already
    /// chosen one.
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <remarks>
    /// The poll interval, the throttle's deferral and the token's expiry all read this one provider's
    /// background scheduler, so a test advances time rather than waiting for it (§ 4 row 14). A provider
    /// rather than a bare <c>IScheduler</c> keeps "which thread" a stated choice at the call site: the host
    /// is where the user-interface thread is a real dispatcher, and it may register its own provider before
    /// calling this — nothing here reads an ambient clock either way.
    /// </remarks>
    private static void TryAddSchedulers(this IServiceCollection services)
    {
        foreach (var registered in services)
        {
            if (registered.ServiceType == typeof(ISchedulerProvider))
            {
                return;
            }
        }

        services.AddSingleton<ISchedulerProvider>(static _ =>
            new SchedulerProvider(CurrentThreadScheduler.Instance, TaskPoolScheduler.Default));
    }
}
