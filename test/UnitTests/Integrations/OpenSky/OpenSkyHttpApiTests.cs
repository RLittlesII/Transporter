using System.Net;
using AwesomeAssertions;
using DynamicData;
using Flurl.Http;
using Flurl.Http.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Integrations.OpenSky;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Integrations.OpenSky.Http;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class OpenSkyHttpApiTests
{
    [Fact]
    public async Task GivenABoundingBoxAndTheExtendedFlag_WhenAPollIsSent_ThenTheRequestCarriesThemAsTheProviderNamesThem()
    {
        // Given
        using var http = new HttpTest();
        http.RespondWithJson(new { time = 1791124330, states = Array.Empty<object[]>() });
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture();

        // When
        var response = await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        response.Time.Should().Be(1791124330);
        http.ShouldHaveCalled("*/states/all")
            .WithVerb(HttpMethod.Get)
            .WithQueryParam("lamin", 29.4)
            .WithQueryParam("lomin", -95.9)
            .WithQueryParam("lamax", 30.2)
            .WithQueryParam("lomax", -94.8)
            .WithQueryParam("extended", 1)
            .Times(1);
    }

    [Fact]
    public async Task GivenAThrottledResponse_WhenAPollIsSent_ThenItThrowsCarryingTheSecondsTheProviderAsked()
    {
        // Given
        using var http = new HttpTest();
        http.RespondWith(
            status: 429,
            headers: new Dictionary<string, string> { [OpenSkyHttpApi.RetryAfterHeader] = "42" });
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture();

        // When
        var call = async () => await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        (await call.Should().ThrowAsync<OpenSkyThrottledException>())
            .Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(42));
    }

    [Fact]
    public async Task GivenAnExpiredTokenAndGivenAnUnauthorizedResponse_WhenAPollIsSent_ThenTheTokenRefreshesAndTheRequestRetriesOnce()
    {
        // Given. A token good for a minute, then a second and a third if asked for. The states
        // endpoint rejects the first request and answers the rest.
        var scheduler = new TestScheduler();
        var clients = OpenSkyTransportTestData.Clients();
        using var http = new HttpTest();
        http.ForCallsTo(OpenSkyTransportTestData.TokenCalls)
            .RespondWithJson(new { access_token = "token-one", expires_in = 60 })
            .RespondWithJson(new { access_token = "token-two", expires_in = 60 })
            .RespondWithJson(new { access_token = "token-three", expires_in = 60 });
        http.ForCallsTo(OpenSkyTransportTestData.StatesCalls)
            .RespondWith(status: (int) HttpStatusCode.Unauthorized)
            .RespondWithJson(new { time = 1791124330, states = Array.Empty<object[]>() })
            .RespondWithJson(new { time = 1791124390, states = Array.Empty<object[]>() });
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture()
            .WithCache(clients)
            .WithSource(OpenSkyTransportTestData.Tokens(clients, scheduler, new RecordingLogger<OpenSkyTokenSource>()));

        // When. One poll that is rejected and retried, then a poll after the token has expired.
        await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);
        await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then. The 401 refreshed and retried the request exactly once, and expiry refreshed again
        // without being asked by a status code — which is B-026's "not expiry alone", read both ways.
        http.ShouldHaveCalled(OpenSkyTransportTestData.StatesCalls)
            .Times(3);   // one rejected request, its single retry, and the poll after expiry
        http.ShouldHaveCalled(OpenSkyTransportTestData.TokenCalls)
            .Times(3);   // one to start with, one the 401 forced, and one the expiry forced
        http.CallLog.Where(static call => call.Request.Url.Path.EndsWith("/states/all", StringComparison.Ordinal))
            .Select(static call => call.Request.Headers.FirstOrDefault("Authorization"))
            .Should()
            .BeEquivalentTo(
                ["Bearer token-one", "Bearer token-two", "Bearer token-three"],
                "the retry carries the new token, not the one that was rejected");
    }

    [Fact]
    public async Task GivenASecondUnauthorizedResponse_WhenTheRetryIsRejectedToo_ThenItIsNotRetriedAgain()
    {
        // Given. Both the request and its retry are rejected.
        var scheduler = new TestScheduler();
        var clients = OpenSkyTransportTestData.Clients();
        using var http = new HttpTest();
        http.ForCallsTo(OpenSkyTransportTestData.TokenCalls)
            .RespondWithJson(new { access_token = "token-one", expires_in = 60 })
            .RespondWithJson(new { access_token = "token-two", expires_in = 60 });
        http.ForCallsTo(OpenSkyTransportTestData.StatesCalls)
            .RespondWith(status: (int) HttpStatusCode.Unauthorized)
            .RespondWith(status: (int) HttpStatusCode.Unauthorized);
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture()
            .WithCache(clients)
            .WithSource(OpenSkyTransportTestData.Tokens(clients, scheduler, new RecordingLogger<OpenSkyTokenSource>()));

        // When
        var call = async () => await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then. The second rejection is the caller's to see: retrying again would spend a credit to
        // learn what the first retry already said.
        await call.Should().ThrowAsync<FlurlHttpException>();
        http.ShouldHaveCalled(OpenSkyTransportTestData.StatesCalls).Times(2);
    }

    [Fact]
    public async Task GivenAPollAndATokenRefresh_WhenBothAreLogged_ThenRemainingCreditIsRecordedAtDebugAndNoSecretAppears()
    {
        // Given
        var scheduler = new TestScheduler();
        var clients = OpenSkyTransportTestData.Clients();
        var transportLog = new RecordingLogger<OpenSkyHttpApi>();
        var tokenLog = new RecordingLogger<OpenSkyTokenSource>();
        using var http = new HttpTest();
        http.ForCallsTo(OpenSkyTransportTestData.TokenCalls)
            .RespondWithJson(new { access_token = "token-one", expires_in = 1800 });
        http.ForCallsTo(OpenSkyTransportTestData.StatesCalls)
            .RespondWithJson(
                new { time = 1791124330, states = Array.Empty<object[]>() },
                headers: new Dictionary<string, string> { [OpenSkyHttpApi.RemainingHeader] = "3412" });
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture()
            .WithCache(clients)
            .WithSource(OpenSkyTransportTestData.Tokens(clients, scheduler, tokenLog))
            .WithLogger(transportLog);

        // When
        await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then. The credit is in the line, at debug, and nothing anywhere carries a credential.
        transportLog.At(LogLevel.Debug).Should().ContainSingle(static line => line.Contains("3412", StringComparison.Ordinal));
        tokenLog.At(LogLevel.Debug).Should().ContainSingle(static line => line.Contains("refreshed", StringComparison.OrdinalIgnoreCase));
        transportLog.Messages.Concat(tokenLog.Messages)
            .Should()
            .NotContain(static line => line.Contains("token-one", StringComparison.Ordinal))
            .And.NotContain(static line => line.Contains(OpenSkyTransportTestData.Credentials.ClientId!, StringComparison.Ordinal))
            .And.NotContain(static line => line.Contains(OpenSkyTransportTestData.Credentials.ClientSecret!, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenAThrottledResponseAndGivenAServerError_WhenEachIsHandled_ThenTheFirstDefersByTheHeaderAndTheSecondStaysAnException()
    {
        // Given. A one-second interval, deliberately shorter than the wait the provider asks for:
        // at the fifteen-second default, waiting for the next tick and honouring the header would be
        // indistinguishable and the assertion would pass either way.
        var scheduler = new TestScheduler();
        var cache = new SourceCache<AircraftSnapshot, string>(static snapshot => snapshot.Icao24);
        var polls = 0;
        var api = Substitute.For<IOpenSkyApi>();
        api.GetStates(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<OpenSkyStatesResponse>>(_ => throw (++polls == 1
                ? new OpenSkyThrottledException(TimeSpan.FromSeconds(12))
                : new TimeoutException("No second response is needed; the count is what this asserts.")));
        AircraftSnapshotClient client = new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithCache(cache)
            .WithProvider(OpenSkyTransportTestData.Scheduling(scheduler))
            .WithOptions(
                Options.Create(
                    new OpenSkyOptions
                    {
                        Box = new BoundingBox
                        {
                            LatitudeMinimum = 28.8,
                            LongitudeMinimum = -96.0,
                            LatitudeMaximum = 30.4,
                            LongitudeMaximum = -94.2,
                        },
                        PollInterval = TimeSpan.FromSeconds(1),
                    }));
        Exception? reached = null;
        using var subscription = client.Snapshots.Subscribe(static _ => { }, failure => reached = failure);

        // When. Eleven seconds after the throttled poll — one second short of what was asked for.
        using var polling = client.Poll();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(11).Ticks);
        var beforeTheHeadersWait = api.ReceivedCalls().Count();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

        // Then. The deferral is the header's twelve seconds and not the one-second interval the
        // client would otherwise use, no exception reached the subscriber, and nothing was cached.
        beforeTheHeadersWait.Should().Be(1, "the next request waits the twelve seconds the provider asked for");
        api.ReceivedCalls().Should().HaveCount(2, "twelve seconds on, the next poll is due");
        reached.Should().BeNull("a throttle is data, and no exception reaches a subscriber of the client's stream");
        cache.Items.Should().BeEmpty("nothing is written to the cache for a throttled poll");

        // And. A server error is an exception rather than a deferred poll: nothing allowed the
        // status, so Flurl throws, and no wait can be derived from a response carrying no header.
        using var http = new HttpTest();
        http.ForCallsTo(OpenSkyTransportTestData.TokenCalls)
            .RespondWithJson(new { access_token = "token-one", expires_in = 60 });
        http.ForCallsTo(OpenSkyTransportTestData.StatesCalls).RespondWith(status: 500);
        var clients = OpenSkyTransportTestData.Clients();
        OpenSkyHttpApi transport = new OpenSkyHttpApiFixture()
            .WithCache(clients)
            .WithSource(OpenSkyTransportTestData.Tokens(clients, scheduler, new RecordingLogger<OpenSkyTokenSource>()));

        var serverError = async () => await ((IOpenSkyApi) transport).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        (await serverError.Should().ThrowAsync<FlurlHttpException>())
            .Which.StatusCode.Should()
            .Be(500);
        await serverError.Should().NotThrowAsync<OpenSkyThrottledException>();
    }
}

/// <summary>Builds the transport, so a constructor change edits this and not every test.</summary>
[AutoFixture(typeof(OpenSkyHttpApi))]
internal partial class OpenSkyHttpApiFixture
{
    public OpenSkyHttpApiFixture()
    {
        WithCache(OpenSkyTransportTestData.Clients());
        WithLogger(new RecordingLogger<OpenSkyHttpApi>());
        WithSource(Answering("a-token"));
    }

    private static IOpenSkyTokenSource Answering(string token)
    {
        var tokens = Substitute.For<IOpenSkyTokenSource>();

        tokens.Current(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(token));

        return tokens;
    }
}
