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
    /// <summary>Adds the swap decorator and the fleet tracker over it, at the application's lifetime.</summary>
    /// <param name="services">The collection to register into.</param>
    /// <returns>The same collection, so registration chains.</returns>
    /// <remarks>The decorator is one object behind two registrations: the actor names the class, consumers name the seam.</remarks>
    public static IServiceCollection AddFleetTracking(this IServiceCollection services)
    {
        services.AddSingleton<SwappingTrackerSource>();
        services.AddSingleton<ITrackerSource>(static provider => provider.GetRequiredService<SwappingTrackerSource>());
        services.AddSingleton<IFleetTracker, FleetTracker>();

        return services;
    }

    /// <summary>Starts the actor that performs the swap.</summary>
    /// <param name="registry">Where a view model resolves the actor from.</param>
    /// <param name="system">The system the actor is started in.</param>
    /// <param name="resolver">How the actor reaches the decorator the container owns.</param>
    /// <returns>The same registry, so registration chains.</returns>
    /// <remarks>Public because the actor and the decorator are not: the head names this and never either type.</remarks>
    public static IActorRegistry AddFleetTrackingActors(
        this IActorRegistry registry,
        ActorSystem system,
        IDependencyResolver resolver)
    {
        registry.Register<SourceSwapActor>(system.ActorOf(resolver.Props<SourceSwapActor>(), nameof(SourceSwapActor)));

        return registry;
    }
}
