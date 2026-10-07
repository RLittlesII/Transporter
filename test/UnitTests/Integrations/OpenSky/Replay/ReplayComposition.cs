using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Airframe;
using Transponder.Integrations.OpenSky;
using Transponder.Integrations.OpenSky.Container;
using Transponder.Scheduling;
using Transponder.UnitTests.Scheduling;

namespace Transponder.UnitTests.Integrations.OpenSky.Replay;

/// <summary>
/// The replay chain as an application composes it: the OpenSky integration's own registrations,
/// and the replay chain over an opened recording.
/// </summary>
/// <remarks>
/// A test resolves the chain rather than assembling one, which is the whole reason registration is
/// a method (B-052's reading applied here). The scheduler is registered before
/// <see cref="OpenSkyRegistration.AddOpenSky"/>, which leaves a provider the host already chose
/// alone — so every wait in the chain lands on the scheduler the test advances.
/// </remarks>
internal static class ReplayComposition
{
    /// <summary>Composes the chain over a recording.</summary>
    /// <param name="recording">The opened recording the pacer reads.</param>
    /// <param name="scheduler">The scheduler every wait in the chain is scheduled on.</param>
    /// <returns>The provider, which the caller disposes.</returns>
    internal static ServiceProvider Over(Stream recording, TestScheduler scheduler)
    {
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ISchedulerProvider>(schedulers);
        services.AddOpenSky(Settings());
        services.AddAircraftReplay(recording);

        return services.BuildServiceProvider();
    }

    /// <summary>Opens a recording held in memory, because no test reads a file.</summary>
    /// <param name="lines">The recorded lines.</param>
    /// <returns>The stream, which the caller disposes.</returns>
    internal static MemoryStream Recorded(string lines) => new(Encoding.UTF8.GetBytes(lines));

    /// <summary>The live chain's settings, carrying a box so the live half composes at all.</summary>
    /// <returns>The configuration.</returns>
    /// <remarks>
    /// No credential: nothing here resolves the token source, and a composition with no live
    /// transport is B-011's subject rather than this helper's.
    /// </remarks>
    private static IConfiguration Settings() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpenSkyOptions.Section}:{nameof(OpenSkyOptions.BaseUrl)}"] = OpenSkyOptions.DefaultBaseUrl,
                [$"{OpenSkyOptions.Section}:Box:LatitudeMinimum"] = "28.8",
                [$"{OpenSkyOptions.Section}:Box:LongitudeMinimum"] = "-96.0",
                [$"{OpenSkyOptions.Section}:Box:LatitudeMaximum"] = "30.4",
                [$"{OpenSkyOptions.Section}:Box:LongitudeMaximum"] = "-94.2",
            })
            .Build();
}
