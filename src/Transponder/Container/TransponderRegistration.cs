using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReactiveMarbles.Locator;
using ReactiveMarbles.Mvvm;
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
    /// <para>
    /// The schedulers are registered before the integration, which adds a provider of its own only
    /// when the host has chosen none: a view binds on the thread that owns the window, and a poll
    /// does not.
    /// </para>
    /// <para>
    /// Replay is registered beside the live source rather than instead of it, and configuration
    /// decides only which recording it would read — never whether it is the live one, which is the
    /// selector's (replay-source B-015). A run with no recording named registers no replay source,
    /// so the control is absent rather than offering a wrong recording, and the startup report says
    /// so (B-024, B-026).
    /// </para>
    /// </remarks>
    public static IServiceCollection AddTransponder(
        this IServiceCollection services,
        IConfiguration configuration,
        IScheduler userInterfaceThread)
    {
        services.AddSingleton<ISchedulerProvider>(
            _ => new SchedulerProvider(userInterfaceThread, TaskPoolScheduler.Default));

        // ReactiveMarbles' own locator, which `RxCommand` reads its exception handler and default
        // schedulers from. It is process-wide, so the composition root is the one place that may
        // write it: a view model doing this would be resolving from a static, and every view model
        // takes its schedulers by constructor instead (`fleet-pipeline` B-005).
        ServiceLocator.Current().AddCoreRegistrations(
            userInterfaceThread,
            TaskPoolScheduler.Default,
            Observer.Create<Exception>(static error => ExceptionDispatchInfo.Capture(error).Throw()));

        services.AddOpenSky(configuration);
        services.AddAircraftReplay(configuration);
        services.AddFleetTracking();
        services.AddTransient<FleetViewModel>();
        services.AddTransient<FleetSummaryViewModel>();

        return services;
    }
}
