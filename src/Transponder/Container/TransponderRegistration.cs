using System.Reactive.Concurrency;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rocket.Surgery.Airframe;
using Transponder.Features.Fleet.ViewModels;
using Transponder.Integrations.OpenSky.Container;
using Transponder.Scheduling;
using Transponder.Tracking.Container;

namespace Transponder.Container;

/// <summary>
/// Everything the application is composed of that is not a MAUI type, in one method (`0047`).
/// </summary>
/// <remarks>
/// What stays in the head is what no test project here can reference - the pages, the window and
/// the asset the settings are read from - so a registration that goes missing from the graph a view
/// model needs is a failing test rather than a window that never opens.
/// </remarks>
public static class TransponderRegistration
{
    /// <summary>Adds the integration, the tracker over it, and the view models that bind what it publishes.</summary>
    /// <param name="services">The collection to register into.</param>
    /// <param name="configuration">Where the integration reads its settings and its credentials from.</param>
    /// <param name="userInterfaceThread">The scheduler a view binds on, which only the head can name.</param>
    /// <returns>The same collection, so registration chains.</returns>
    /// <remarks>
    /// The schedulers are registered before the integration, which adds a provider of its own only
    /// when the host has chosen none: a view binds on the thread that owns the window, and a poll
    /// does not.
    /// </remarks>
    public static IServiceCollection AddTransponder(
        this IServiceCollection services,
        IConfiguration configuration,
        IScheduler userInterfaceThread)
    {
        services.AddSingleton<ISchedulerProvider>(
            _ => new SchedulerProvider(userInterfaceThread, TaskPoolScheduler.Default));

        services.AddOpenSky(configuration);
        services.AddFleetTracking();
        services.AddTransient<FleetViewModel>();

        return services;
    }
}
