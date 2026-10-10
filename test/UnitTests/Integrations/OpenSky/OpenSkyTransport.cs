using Flurl.Http.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using Transporter.Integrations.OpenSky.Authentication;
using Transporter.Integrations.OpenSky.Configuration;
using Transporter.Integrations.OpenSky.Http.Api;
using Transporter.Scheduling;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Integrations.OpenSky;

/// <summary>
/// What the transport's tests stand up in common: the two named Flurl clients, and a real token
/// source over them.
/// </summary>
/// <remarks>
/// Not test <em>data</em> — xUnit's class and member data are the <see cref="TheoryData{T}"/>
/// classes beside this one — but the collaborators a test arranges. The token source here is the
/// real type rather than a substitute, because B-026 is about refreshing and retrying and a
/// substitute would assert the arrangement rather than the behaviour.
/// </remarks>
internal static class OpenSkyTransport
{
    /// <summary>A URL on the token endpoint, matched where a test asserts the refresh.</summary>
    internal const string TokenCalls = "*openid-connect/token*";

    /// <summary>A URL on the states endpoint, matched where a test asserts the poll.</summary>
    internal const string StatesCalls = "*/states/all*";

    /// <summary>The base URL a test's Flurl client is built on; no test reaches the real host.</summary>
    internal const string BaseUrl = "https://opensky.invalid/api";

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
            .Add(OpenSkyHttpApi.ClientName, BaseUrl)
            .Add(OpenSkyTokenSource.ClientName, OpenSkyTokenSource.TokenUrl);

    /// <summary>A real token source, so a test exercises refresh rather than a stand-in for it.</summary>
    /// <param name="clients">The client cache to reach the token endpoint through.</param>
    /// <param name="scheduler">Where expiry is measured from.</param>
    /// <param name="logger">Where the refresh is recorded.</param>
    /// <returns>The token source.</returns>
    internal static OpenSkyTokenSource Tokens(
        IFlurlClientCache clients,
        TestScheduler scheduler,
        RecordingLogger<OpenSkyTokenSource> logger) =>
        new(clients, Options.Create(Credentials), logger, Schedulers(scheduler));

    /// <summary>One scheduler in both positions, so advancing it advances the whole chain.</summary>
    /// <param name="scheduler">The scheduler a test advances.</param>
    /// <returns>The provider the real type implements.</returns>
    internal static SchedulerProvider Schedulers(TestScheduler scheduler) =>
        new SchedulerProviderFixture().WithTestScheduler(scheduler);
}
