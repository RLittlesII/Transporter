using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Transponder.Tracking.Sources;

namespace Transponder.Tracking.Container;

/// <summary>
/// Registers the pipeline over the tracker seam. The provider's own chain is its integration's
/// registration, and this one adds nothing of a provider's to it.
/// </summary>
public static class TrackingRegistration
{
    /// <summary>
    /// Adds the swap decorator and the fleet tracker over it, both at the application's lifetime
    /// (B-038, B-052, ADR-0009 decision 7, ADR-0011).
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <returns>The same collection, so registration chains.</returns>
    /// <remarks>
    /// <para>
    /// One tracker for the application, because the pipeline is built once and a second instance
    /// would be a second connection to the seam and a second collection of the same vehicles
    /// (B-042). The decorator is one object behind two registrations for the same reason the clock
    /// is: the actor that swaps names the class and every consumer names the seam.
    /// </para>
    /// <para>
    /// Nothing here is order-sensitive. The strategies arrive as the container's own enumerable,
    /// resolved when the seam is first resolved, so an integration registered after this call is
    /// still swapped to (ADR-0011).
    /// </para>
    /// </remarks>
    public static IServiceCollection AddFleetTracking(this IServiceCollection services)
    {
        services.AddSingleton<SwappingTrackerSource>();
        services.AddSingleton<ITrackerSource>(static provider => provider.GetRequiredService<SwappingTrackerSource>());
        services.AddSingleton<IFleetTracker, FleetTracker>();

        return services;
    }

    /// <summary>Starts the actor that performs the swap (`fleet-dashboard` B-016, § 4 row 6).</summary>
    /// <param name="registry">Where a view model resolves the actor from.</param>
    /// <param name="system">The system the actor is started in.</param>
    /// <param name="resolver">How the actor reaches the decorator the container owns.</param>
    /// <returns>The same registry, so registration chains.</returns>
    /// <remarks>
    /// Separate from <see cref="AddFleetTracking"/> because an actor system is the host's and a
    /// service collection is not, and public because the actor and the decorator it is handed are
    /// both internal — the head names this method and never either type.
    /// </remarks>
    public static IActorRegistry AddFleetTrackingActors(
        this IActorRegistry registry,
        ActorSystem system,
        IDependencyResolver resolver)
    {
        registry.Register<SourceSwapActor>(system.ActorOf(resolver.Props<SourceSwapActor>(), nameof(SourceSwapActor)));

        return registry;
    }
}
