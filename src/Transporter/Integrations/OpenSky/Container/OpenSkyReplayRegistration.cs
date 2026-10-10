using System;
using System.Collections.Generic;
using System.IO;
using DynamicData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.OpenSky.Contracts;
using Transporter.Integrations.OpenSky.Model;
using Transporter.Integrations.OpenSky.Replay;
using Transporter.Recording;
using Transporter.Tracking;
using Transporter.Tracking.Sources;

namespace Transporter.Integrations.OpenSky.Container;

/// <summary>
/// Registers the replay chain: a second instance of the live client, cache and strategy over the
/// contract a recording stands in for. The one public surface of the replay half, as
/// <see cref="OpenSkyRegistration.AddOpenSky"/> is of the live half.
/// </summary>
public static class OpenSkyReplayRegistration
{
    /// <summary>The key every replay registration carries, so the live chain's own registrations stay the unkeyed ones.</summary>
    internal const string Chain = "aircraft-replay";

    /// <summary>The name a swap control shows for the aircraft recording (B-057).</summary>
    internal const string RecordedAircraft = "Recorded aircraft";

    /// <summary>
    /// The replay chain's own options: no interval, because the recorded spacing is the whole
    /// cadence (B-007), and a box that satisfies the client's guard and reaches no request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A second options instance rather than a changed global: the live chain reads the configured
    /// interval and box and is untouched by this.
    /// </para>
    /// <para>
    /// The box is four zeroes on purpose. The client throws without one (B-050) and the replay
    /// contract reads it and ignores it, so any value here is dead — and a plausible-looking box
    /// would suggest a filter that does not exist. Four zeroes is visibly a placeholder.
    /// </para>
    /// </remarks>
    internal static readonly OpenSkyOptions Replayed = new()
    {
        PollInterval = TimeSpan.Zero,
        Box = new BoundingBox
        {
            LatitudeMinimum = 0,
            LongitudeMinimum = 0,
            LatitudeMaximum = 0,
            LongitudeMaximum = 0,
        },
    };

    /// <summary>
    /// Adds the replay half: the recordings configuration names, checked, and a source over each one
    /// that is usable.
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <param name="configuration">
    /// Where the recordings are named — one key per source under <c>Replay</c>, resolved against
    /// <c>Recording:Root</c> — and where the shared part reads the integration's options.
    /// </param>
    /// <returns>The same collection, so registration chains.</returns>
    /// <remarks>
    /// <para>
    /// Calls <see cref="OpenSkyRegistration.AddOpenSkyShared"/> and not <c>AddOpenSky</c>, so a
    /// composition that registers only replay starts with no credential configured (B-011,
    /// § 11 row 4). An application that registers both halves calls both, and the shared part runs
    /// once.
    /// </para>
    /// <para>
    /// Configuration is read here rather than bound and resolved later, because whether a source is
    /// registered at all depends on what it says: a recording that is unnamed, absent, unreadable
    /// or too short leaves its source unregistered and therefore unselectable (B-024, B-026), and a
    /// registration cannot be withdrawn once the container is built. <see cref="ReplayOptions"/> is
    /// bound as well, so what the report names and what configuration holds are the same values.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAircraftReplay(this IServiceCollection services, IConfiguration configuration) =>
        services.AddAircraftReplay(
            configuration,
            new RecordingLibrary(
                configuration[$"{RecordingOptions.Section}:{nameof(RecordingOptions.Root)}"] ?? RecordingOptions.DefaultRoot));

    /// <summary>
    /// Adds the aircraft replay chain over an opened recording.
    /// </summary>
    /// <param name="services">The collection to register into, with the OpenSky integration's shared registrations already in it.</param>
    /// <param name="recording">
    /// The recording, already opened and seekable. Resolving a name against a root and opening the
    /// file is the caller's, which is `adr/0001` item 5 and B-027: the components below hold the
    /// time base and no file system.
    /// </param>
    /// <returns>The same collection, so registration chains.</returns>
    /// <remarks>
    /// <para>
    /// Every piece of the chain is its own registration under <see cref="Chain"/> rather than one
    /// factory building the lot, so the composition a test resolves is the composition an
    /// application runs — the objection to a second graph assembled by hand (B-052's reading).
    /// A keyed registration is what makes "the same class, a second instance" expressible: the
    /// live client, cache and options keep their own unkeyed registrations and are untouched.
    /// </para>
    /// <para>
    /// The chain is registered behind <see cref="IAircraftReplayTrackerSource"/> and as one more
    /// <see cref="ITrackerSourceStrategy"/> beside the live one. Both are true at once and have to
    /// be: the seam is how the selector tells two instances of one class apart, and the strategy
    /// registration is how the selector is handed it at all.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAircraftReplay(this IServiceCollection services, Stream recording)
    {
        services.AddKeyedSingleton<IRecordingPacer>(
            Chain,
            (provider, _) => new RecordingPacer(
                recording,
                provider.GetRequiredService<ISchedulerProvider>(),
                provider.GetRequiredService<ILogger<RecordingPacer>>()));

        services.AddKeyedSingleton<IOpenSkyApi>(
            Chain,
            static (provider, key) => new ReplayOpenSkyApi(
                provider.GetRequiredKeyedService<IRecordingPacer>(key),
                provider.GetRequiredService<ILogger<ReplayOpenSkyApi>>()));

        // Its own cache: sharing the live one would leave the outgoing feed's aircraft in the
        // collection under the incoming one, which B-016 forbids being visible.
        services.AddKeyedSingleton(
            Chain,
            static (_, _) => new SourceCache<AircraftSnapshot, string>(static snapshot => snapshot.Icao24));

        services.AddKeyedSingleton<IOptions<OpenSkyOptions>>(Chain, static (_, _) => Options.Create(Replayed));

        services.AddKeyedSingleton<IAircraftSnapshotClient>(
            Chain,
            static (provider, key) => new AircraftSnapshotClient(
                provider.GetRequiredKeyedService<IOpenSkyApi>(key),
                provider.GetRequiredKeyedService<SourceCache<AircraftSnapshot, string>>(key),
                provider.GetRequiredService<IObservedClockWriter>(),
                provider.GetRequiredKeyedService<IOptions<OpenSkyOptions>>(key),
                provider.GetRequiredService<ISchedulerProvider>(),
                provider.GetRequiredService<ILogger<AircraftSnapshotClient>>()));

        services.AddSingleton<IAircraftReplayTrackerSource>(static provider => new AircraftReplayTrackerSource(
            new AircraftTrackerSource(
                provider.GetRequiredKeyedService<IAircraftSnapshotClient>(Chain),
                provider.GetRequiredKeyedService<SourceCache<AircraftSnapshot, string>>(Chain),
                provider.GetRequiredService<AircraftSnapshotMapper>())));

        // One more strategy, which is the whole of selection: the decorator is handed every
        // registration of this seam and replay is one of them, so there is no replay-only selector
        // and no offline-mode flag to add (B-015). Never as ITrackerSource — that reaches consumers
        // in place of the selector and the swap then silently does nothing (ADR-0011).
        services.AddSingleton<ITrackerSourceStrategy>(
            static provider => provider.GetRequiredService<IAircraftReplayTrackerSource>());
        services.AddSingleton(static provider =>
            new TrackerSourceEntry(provider.GetRequiredService<IAircraftReplayTrackerSource>(), RecordedAircraft));

        return services;
    }

    /// <summary>
    /// Adds the replay half over a given recordings root.
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <param name="configuration">Where the recordings are named, and the shared part's settings.</param>
    /// <param name="recordings">The root to resolve and open the named recordings in.</param>
    /// <returns>The same collection, so registration chains.</returns>
    /// <remarks>
    /// The root is a parameter here because a rehearsal recording is operational data that is never
    /// a fixture (B-006), so a test of this registration has no file to point at and supplies
    /// recordings of its own. B-025's resolution is <see cref="RecordingLibrary"/>'s and is proven
    /// against that type.
    /// </remarks>
    internal static IServiceCollection AddAircraftReplay(
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

        // Reported and then closed: the vessel feed is unspecified, so nothing consumes a vessel
        // recording until `0013`, and a handle held open for a run nobody reads is a leak with a
        // claim attached to it. The row is still produced, because B-020 expects the closing act's
        // recording to be cleared by this report rather than at the swap.
        var vessels = CheckedRecording.Check(
            nameof(ReplayOptions.Vessels),
            configuration[$"{ReplayOptions.Section}:{nameof(ReplayOptions.Vessels)}"],
            recordings,
            FleetTracker.DefaultStaleAfter);

        vessels.Payloads?.Dispose();

        services.AddSingleton<IReadOnlyList<CheckedRecording>>([aircraft, vessels]);
        services.AddSingleton<ReplayStartupReport>();
        services.AddSingleton<IHostedService>(static provider => provider.GetRequiredService<ReplayStartupReport>());

        if (aircraft.Payloads is { } payloads)
        {
            services.AddAircraftReplay(payloads);
        }

        return services;
    }
}
