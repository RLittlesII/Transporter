using System;
using System.IO;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rocket.Surgery.Airframe;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Integrations.OpenSky.Model;
using Transponder.Integrations.OpenSky.Replay;
using Transponder.Recording;
using Transponder.Tracking;
using Transponder.Tracking.Sources;

namespace Transponder.Integrations.OpenSky.Container;

/// <summary>
/// Registers the replay chain: a second instance of the live client, cache and strategy over the
/// contract a recording stands in for. The one public surface of the replay half, as
/// <see cref="OpenSkyRegistration.AddOpenSky"/> is of the live half.
/// </summary>
public static class OpenSkyReplayRegistration
{
    /// <summary>The key every replay registration carries, so the live chain's own registrations stay the unkeyed ones.</summary>
    internal const string Chain = "aircraft-replay";

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
    /// The chain is registered behind <see cref="IAircraftReplayTrackerSource"/> and not as an
    /// <see cref="ITrackerSourceStrategy"/>: selecting it is `0012`'s, along with the recording's
    /// name, its root and the startup report. Nothing here is selectable yet, which is why
    /// registering this leaves the live chain's behaviour exactly as it was.
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

        return services;
    }
}
