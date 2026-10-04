using Flurl.Http.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Integrations.OpenSky.Http;

namespace Transponder.Integrations.OpenSky.Container;

/// <summary>
/// Registers the OpenSky integration into a service collection. This is the integration's only public surface:
/// every other type in it is internal, so a consumer names the contract and never an implementation.
/// </summary>
public static class OpenSkyRegistration
{
    /// <summary>
    /// Adds the OpenSky API contract, aliased to the transport that reaches the provider over HTTP.
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <param name="baseUrl">The provider's base URL.</param>
    /// <returns>The same collection, so registration chains.</returns>
    public static IServiceCollection AddOpenSky(this IServiceCollection services, string baseUrl)
    {
        services.AddSingleton<IFlurlClientCache>(_ => new FlurlClientCache().Add(OpenSkyHttpApi.ClientName, baseUrl));
        services.AddSingleton<IOpenSkyApi, OpenSkyHttpApi>();

        return services;
    }
}
