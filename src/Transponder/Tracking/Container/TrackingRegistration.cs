using Microsoft.Extensions.DependencyInjection;

namespace Transponder.Tracking.Container;

/// <summary>
/// Registers the pipeline over the tracker seam. The provider's own chain is its integration's
/// registration, and this one adds nothing of a provider's to it.
/// </summary>
public static class TrackingRegistration
{
    /// <summary>
    /// Adds the fleet tracker, at the application's lifetime, over whatever is registered as
    /// <see cref="ITrackerSource"/> (B-052, ADR-0009 decision 7).
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <returns>The same collection, so registration chains.</returns>
    /// <remarks>
    /// One tracker for the application, because the pipeline is built once and a second instance
    /// would be a second connection to the seam and a second collection of the same vehicles
    /// (B-042).
    /// </remarks>
    public static IServiceCollection AddFleetTracking(this IServiceCollection services)
    {
        services.AddSingleton<IFleetTracker, FleetTracker>();

        return services;
    }
}
