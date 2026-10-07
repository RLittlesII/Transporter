using System;
using System.Globalization;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using DynamicData;
using Flurl.Http.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rocket.Surgery.Airframe;
using Transponder.Integrations.OpenSky.Authentication;
using Transponder.Integrations.OpenSky.Configuration;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Integrations.OpenSky.Http.Api;
using Transponder.Integrations.OpenSky.Polling;
using Transponder.Recording;
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
    /// This method is the live composition of the integration, and it takes configuration rather than
    /// pre-bound options so that there is exactly one of it: an application and a test both call this and
    /// differ only in what configuration they supply. A test that assembled the same graph by hand would be a
    /// second composition to keep in step, and the first production scenario it missed would pass.
    /// </para>
    /// <para>
    /// The options and the credentials are two registrations, not one, and both validate on start: an absent
    /// credential or an absent bounding box stops the application here rather than failing the first poll on
    /// stage (B-029, B-050). The options are <see cref="AddOpenSkyShared"/>'s and the credentials are this
    /// method's, which is replay-source § 11 row 4: a composition with no live transport has no credential to
    /// be missing, and one with a live transport validates exactly as it did before.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddOpenSky(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenSkyShared(configuration);

        services.AddSingleton<IValidateOptions<OpenSkyCredentials>>(new OpenSkyConfigurationValidator());
        services.AddOptions<OpenSkyCredentials>().Bind(configuration.GetSection(OpenSkyOptions.Section)).ValidateOnStart();

        services.AddSingleton<IFlurlClientCache>(static provider => new FlurlClientCache()
            .Add(OpenSkyHttpApi.ClientName, provider.GetRequiredService<IOptions<OpenSkyOptions>>().Value.BaseUrl)
            .Add(OpenSkyTokenSource.ClientName, OpenSkyTokenSource.TokenUrl));

        services.AddRecordingWriter();

        services.AddSingleton<IOpenSkyTokenSource, OpenSkyTokenSource>();
        services.AddSingleton<IOpenSkyApi, OpenSkyHttpApi>();
        services.AddSingleton(static _ => new SourceCache<AircraftSnapshot, string>(static snapshot => snapshot.Icao24));
        services.AddSingleton<AircraftSnapshotClient>();
        services.AddSingleton<IAircraftSnapshotClient>(static provider => provider.GetRequiredService<AircraftSnapshotClient>());

        // One client object behind two seams: the strategy's subscription owns the cadence, and the
        // actor demands a poll outside it (B-053).
        services.AddSingleton<IDemandedPoll>(static provider => provider.GetRequiredService<AircraftSnapshotClient>());

        services.AddSingleton<IAircraftTrackerSource, AircraftTrackerSource>();

        // Never as ITrackerSource: that reaches consumers in place of the selector (ADR-0011).
        services.AddSingleton<ITrackerSourceStrategy>(static provider => provider.GetRequiredService<IAircraftTrackerSource>());

        return services;
    }

    /// <summary>Starts the actor a demanded poll is told to.</summary>
    /// <param name="registry">Where a view model resolves the actor from.</param>
    /// <param name="system">The system the actor is started in.</param>
    /// <param name="resolver">How the actor reaches the client the container owns.</param>
    /// <returns>The same registry, so registration chains.</returns>
    /// <remarks>
    /// This integration's, not the tracker's: what is polled, how often, and what refuses a poll are
    /// the provider's business (ADR-0012). Public because the actor and the client are not.
    /// </remarks>
    public static IActorRegistry AddOpenSkyActors(
        this IActorRegistry registry,
        ActorSystem system,
        IDependencyResolver resolver)
    {
        registry.Register<AircraftPollActor>(system.ActorOf(resolver.Props<AircraftPollActor>(), nameof(AircraftPollActor)));

        return registry;
    }

    /// <summary>
    /// Adds what every composition of this integration holds, live or replayed: the options, the
    /// observed clock, the projection, the schedulers and the fleet's description.
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <param name="configuration">Where the options and the recordings root are read from.</param>
    /// <returns>The same collection, so registration chains.</returns>
    /// <remarks>
    /// <para>
    /// The split is replay-source § 11 row 4's answer. <see cref="OpenSkyCredentials"/> and their
    /// <c>ValidateOnStart</c> are the live transport's, not this integration's, so a composition
    /// that registers only replay has no credential to be missing and starts on a laptop with no
    /// secrets and no network — which is replay-source B-011 and the situation that Feature exists
    /// for. A composition that registers the live transport validates exactly as it did before.
    /// </para>
    /// <para>
    /// Calling this twice is what an application that registers both halves does, so it returns
    /// having done nothing the second time. The marker is the clock, because one clock object behind
    /// three aliases is the registration here that two of them would break.
    /// </para>
    /// </remarks>
    internal static IServiceCollection AddOpenSkyShared(this IServiceCollection services, IConfiguration configuration)
    {
        foreach (var registered in services)
        {
            if (registered.ServiceType == typeof(ObservedClock))
            {
                return services;
            }
        }

        services.AddSingleton<IValidateOptions<OpenSkyOptions>>(new OpenSkyConfigurationValidator());
        services.AddOptions<OpenSkyOptions>().Bind(configuration.GetSection(OpenSkyOptions.Section)).ValidateOnStart();
        services.AddOptions<RecordingOptions>().Bind(configuration.GetSection(RecordingOptions.Section));

        services.TryAddSchedulers();

        // One clock object behind two interfaces, registered once and aliased to each: the write side reaches
        // the integration and the read side reaches the tracker, so which one a constructor names is what
        // decides whether it can advance time (ADR-0007).
        services.AddSingleton<ObservedClock>();
        services.AddSingleton<IObservedClock>(static provider => provider.GetRequiredService<ObservedClock>());
        services.AddSingleton<IObservedClockWriter>(static provider => provider.GetRequiredService<ObservedClock>());
        services.AddSingleton<IObservedClockTicks>(static provider => provider.GetRequiredService<ObservedClock>());

        services.AddSingleton<AircraftSnapshotMapper>();

        // A subject rather than Observable.Return, so the stream does not complete when it is read.
        services.AddSingleton(static _ => new BehaviorSubject<FleetSourceDescription>(AircraftFleetDescription.Offered));
        services.AddSingleton<IObservable<FleetSourceDescription>>(
            static provider => provider.GetRequiredService<BehaviorSubject<FleetSourceDescription>>());

        return services;
    }

    /// <summary>
    /// Registers what records a payload: either a writer over a file or the one that keeps nothing.
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <remarks>
    /// <para>
    /// The live transport's, because the tap is: a composition with no live poll records nothing,
    /// and the options this reads are the shared part's, since a replay reads the root a rehearsal
    /// wrote to (<c>adr/0001</c> item 2).
    /// </para>
    /// <para>
    /// Always registers an <see cref="IRecordingWriter"/>, so the transport's tap is one
    /// unconditional call and recording cannot change the shape of the code that runs
    /// (B-004). What configuration decides is which implementation answers, not whether the call
    /// happens.
    /// </para>
    /// <para>
    /// The file is opened by the factory on first resolve rather than here, so a host that never
    /// polls never creates one, and the name is ADR-0004's:
    /// <c>&lt;source&gt;-&lt;utc-instant&gt;.ndjson</c>, one file per run. Opening it is this
    /// registration's job and not the writer's, the same split <c>adr/0001</c> item 5 makes for the
    /// reader.
    /// </para>
    /// <para>
    /// An append handle, not a truncating one: a run that is restarted inside the same second must
    /// not silently erase the recording it is about to extend.
    /// </para>
    /// </remarks>
    private static void AddRecordingWriter(this IServiceCollection services) =>
        services.AddSingleton<IRecordingWriter>(static provider =>
        {
            var options = provider.GetRequiredService<IOptions<RecordingOptions>>().Value;

            if (!options.Enabled)
            {
                return new UnrecordedPayloads();
            }

            Directory.CreateDirectory(options.Root);

            var name = $"aircraft-{DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ", CultureInfo.InvariantCulture)}.ndjson";
            var destination = new StreamWriter(Path.Combine(options.Root, name), append: true);

            return new RecordingWriter(destination, provider.GetRequiredService<ILogger<RecordingWriter>>());
        });

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
