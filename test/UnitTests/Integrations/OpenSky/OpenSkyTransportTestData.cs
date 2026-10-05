using Flurl.Http.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Airframe;
using Transponder.Integrations.OpenSky;
using Transponder.Integrations.OpenSky.Http;

namespace Transponder.UnitTests.Integrations.OpenSky;

/// <summary>
/// What the transport's tests need in common: the two named Flurl clients, a token source over
/// them, and a scheduler provider handing one <see cref="TestScheduler"/> to both members.
/// </summary>
internal static class OpenSkyTransportTestData
{
    /// <summary>A URL on the token endpoint's host, matched by <see cref="TokenCalls"/>.</summary>
    internal const string TokenCalls = "*openid-connect/token*";

    /// <summary>A URL on the states endpoint, matched where a test asserts the poll.</summary>
    internal const string StatesCalls = "*/states/all*";

    /// <summary>The credentials a test runs with — synthetic, and asserted never to be logged.</summary>
    internal static readonly OpenSkyCredentials Credentials = new()
    {
        ClientId = "synthetic-client-id",
        ClientSecret = "synthetic-client-secret",
    };

    /// <summary>Both named clients, as the registration builds them.</summary>
    /// <returns>The cache the transport and the token source ask for their clients.</returns>
    internal static IFlurlClientCache Clients() =>
        new FlurlClientCache()
            .Add(OpenSkyHttpApi.ClientName, "https://opensky.invalid/api")
            .Add(OpenSkyTokenSource.ClientName, OpenSkyTokenSource.TokenUrl);

    /// <summary>A scheduler provider whose every member is the one scheduler a test advances.</summary>
    /// <param name="scheduler">The scheduler to hand back.</param>
    /// <returns>The provider.</returns>
    internal static ISchedulerProvider Scheduling(TestScheduler scheduler)
    {
        var schedulers = Substitute.For<ISchedulerProvider>();

        schedulers.BackgroundThread.Returns(scheduler);
        schedulers.UserInterfaceThread.Returns(scheduler);

        return schedulers;
    }

    /// <summary>A real token source, so a test exercises refresh rather than a stand-in for it.</summary>
    /// <param name="clients">The client cache to reach the token endpoint through.</param>
    /// <param name="scheduler">Where expiry is measured from.</param>
    /// <param name="logger">Where the refresh is recorded.</param>
    /// <returns>The token source.</returns>
    internal static OpenSkyTokenSource Tokens(
        IFlurlClientCache clients,
        TestScheduler scheduler,
        RecordingLogger<OpenSkyTokenSource> logger) =>
        new(clients, Options.Create(Credentials), logger, Scheduling(scheduler));
}
