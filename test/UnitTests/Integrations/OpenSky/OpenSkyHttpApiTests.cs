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
using Transponder.Integrations.OpenSky.Authentication;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Integrations.OpenSky.Http.Api;
using Transponder.Integrations.OpenSky.Model;
using Transponder.Scheduling;
using Transponder.UnitTests.Scheduling;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class OpenSkyHttpApiTests
{
    /// <summary>
    /// B-006 and B-024. The four coordinates and the extended flag reach the wire under OpenSky's
    /// own query-string keys, so the request matches the provider's documentation line for line and
    /// no bounding box appears on the contract.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenABoundingBoxAndTheExtendedFlag_WhenAPollIsSent_ThenTheRequestCarriesThemAsTheProviderNamesThem()
    {
        // Given
        using var http = new HttpTest();
        http.RespondWith(OpenSkyPayloads.NoRows);
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture();

        // When
        var response = await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        response.Time.Should().Be(OpenSkyPayloads.ReportedTime);
        http.ShouldHaveCalled(OpenSkyTransport.StatesCalls)
            .WithVerb(HttpMethod.Get)
            .WithQueryParam("lamin", 29.4)
            .WithQueryParam("lomin", -95.9)
            .WithQueryParam("lamax", 30.2)
            .WithQueryParam("lomax", -94.8)
            .WithQueryParam("extended", 1)
            .Times(1);
    }

    /// <summary>
    /// B-028, the transport's half. A <c>429</c> is read where the headers still are, so the seconds
    /// the provider asked for travel on the exception rather than being lost with the response.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAThrottledResponse_WhenAPollIsSent_ThenItThrowsCarryingTheSecondsTheProviderAsked()
    {
        // Given
        using var http = new HttpTest();
        http.RespondWith(
            status: (int) HttpStatusCode.TooManyRequests,
            headers: new Dictionary<string, string> { [OpenSkyHttpApi.RetryAfterHeader] = "42" });
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture();

        // When
        var call = async () => await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        (await call.Should().ThrowAsync<OpenSkyThrottledException>())
            .Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(42));
    }

    /// <summary>
    /// B-026. Both halves, because the claim is "not expiry alone": a token good for a minute is
    /// refreshed when the states endpoint rejects the request, that request is retried once with the
    /// new token, and a later poll refreshes again on expiry with no status code to prompt it.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAnExpiredTokenAndGivenAnUnauthorizedResponse_WhenAPollIsSent_ThenTheTokenRefreshesAndTheRequestRetriesOnce()
    {
        // Given
        var scheduler = new TestScheduler();
        var clients = OpenSkyTransport.Clients();
        using var http = new HttpTest();
        http.ForCallsTo(OpenSkyTransport.TokenCalls)
            .RespondWithJson(new { access_token = "token-one", expires_in = 60 })
            .RespondWithJson(new { access_token = "token-two", expires_in = 60 })
            .RespondWithJson(new { access_token = "token-three", expires_in = 60 });
        http.ForCallsTo(OpenSkyTransport.StatesCalls)
            .RespondWith(status: (int) HttpStatusCode.Unauthorized)
            .RespondWith(OpenSkyPayloads.NoRows)
            .RespondWith(OpenSkyPayloads.NoRows);
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture()
            .WithCache(clients)
            .WithSource(OpenSkyTransport.Tokens(clients, scheduler, new RecordingLogger<OpenSkyTokenSource>()));

        // When
        await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);
        await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        http.ShouldHaveCalled(OpenSkyTransport.StatesCalls).Times(3);
        http.ShouldHaveCalled(OpenSkyTransport.TokenCalls).Times(3);
        http.CallLog.Where(static call => call.Request.Url.Path.EndsWith("/states/all", StringComparison.Ordinal))
            .Select(static call => call.Request.Headers.FirstOrDefault("Authorization"))
            .Should()
            .BeEquivalentTo(
                ["Bearer token-one", "Bearer token-two", "Bearer token-three"],
                "the retry carries the new token, not the one that was rejected");
    }

    /// <summary>
    /// B-026's ceiling. The retry does not allow a <c>401</c>, so a second rejection is the caller's
    /// to see: retrying again would spend a credit to learn what the first retry already said.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenASecondUnauthorizedResponse_WhenTheRetryIsRejectedToo_ThenItIsNotRetriedAgain()
    {
        // Given
        var scheduler = new TestScheduler();
        var clients = OpenSkyTransport.Clients();
        using var http = new HttpTest();
        http.ForCallsTo(OpenSkyTransport.TokenCalls)
            .RespondWithJson(new { access_token = "token-one", expires_in = 60 })
            .RespondWithJson(new { access_token = "token-two", expires_in = 60 });
        http.ForCallsTo(OpenSkyTransport.StatesCalls)
            .RespondWith(status: (int) HttpStatusCode.Unauthorized)
            .RespondWith(status: (int) HttpStatusCode.Unauthorized);
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture()
            .WithCache(clients)
            .WithSource(OpenSkyTransport.Tokens(clients, scheduler, new RecordingLogger<OpenSkyTokenSource>()));

        // When
        var call = async () => await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        await call.Should().ThrowAsync<FlurlHttpException>();
        http.ShouldHaveCalled(OpenSkyTransport.StatesCalls).Times(2);
    }

    /// <summary>
    /// B-027. The remaining credit is in a line at debug, because the header is the only place a
    /// burn rate is visible before it bites; the refresh is recorded without what it returned; and
    /// no line anywhere carries a token, a client id or a client secret.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAPollAndATokenRefresh_WhenBothAreLogged_ThenRemainingCreditIsRecordedAtDebugAndNoSecretAppears()
    {
        // Given
        var scheduler = new TestScheduler();
        var clients = OpenSkyTransport.Clients();
        var transportLog = new RecordingLogger<OpenSkyHttpApi>();
        var tokenLog = new RecordingLogger<OpenSkyTokenSource>();
        using var http = new HttpTest();
        http.ForCallsTo(OpenSkyTransport.TokenCalls)
            .RespondWithJson(new { access_token = "token-one", expires_in = 1800 });
        http.ForCallsTo(OpenSkyTransport.StatesCalls)
            .RespondWith(
                OpenSkyPayloads.NoRows,
                headers: new Dictionary<string, string> { [OpenSkyHttpApi.RemainingHeader] = "3412" });
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture()
            .WithCache(clients)
            .WithSource(OpenSkyTransport.Tokens(clients, scheduler, tokenLog))
            .WithLogger(transportLog);

        // When
        await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        transportLog.At(LogLevel.Debug).Should().ContainSingle(static line => line.Contains("3412", StringComparison.Ordinal));
        tokenLog.At(LogLevel.Debug).Should().ContainSingle(static line => line.Contains("refreshed", StringComparison.OrdinalIgnoreCase));
        transportLog.Messages.Concat(tokenLog.Messages)
            .Should()
            .NotContain(static line => line.Contains("token-one", StringComparison.Ordinal))
            .And.NotContain(static line => line.Contains(OpenSkyTransport.Credentials.ClientId!, StringComparison.Ordinal))
            .And.NotContain(static line => line.Contains(OpenSkyTransport.Credentials.ClientSecret!, StringComparison.Ordinal));
    }

    /// <summary>
    /// B-028, both halves and both sides of the seam. The throttled poll is deferred by the header's
    /// twelve seconds rather than by the one-second interval the client would otherwise use — an
    /// interval deliberately shorter than the wait, because at the fifteen-second default the two
    /// would be indistinguishable and the assertion would pass either way. A server error stays an
    /// exception rather than becoming a deferred poll: nothing allowed that status, and no wait can
    /// be derived from a response carrying no retry-after header.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAThrottledResponseAndGivenAServerError_WhenEachIsHandled_ThenTheFirstDefersByTheHeaderAndTheSecondStaysAnException()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
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
                : new TimeoutException("No second response is needed; the call count is what this asserts.")));
        AircraftSnapshotClient client = new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithCache(cache)
            .WithProvider(schedulers)
            .WithOptions(Options.Create(new OpenSkyOptions { Box = Houston, PollInterval = TimeSpan.FromSeconds(1) }));
        Exception? reached = null;
        using var subscription = client.Snapshots.Subscribe(static _ => { }, failure => reached = failure);

        // When
        using var polling = client.Poll();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(11).Ticks);
        var beforeTheHeadersWait = api.ReceivedCalls().Count();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

        // Then
        beforeTheHeadersWait.Should().Be(1, "the next request waits the twelve seconds the provider asked for");
        api.ReceivedCalls().Should().HaveCount(2, "twelve seconds on, the next poll is due");
        reached.Should().BeNull("a throttle is data, and no exception reaches a subscriber of the client's stream");
        cache.Items.Should().BeEmpty("nothing is written to the cache for a throttled poll");

        // And
        using var http = new HttpTest();
        http.ForCallsTo(OpenSkyTransport.TokenCalls).RespondWithJson(new { access_token = "token-one", expires_in = 60 });
        http.ForCallsTo(OpenSkyTransport.StatesCalls).RespondWith(status: (int) HttpStatusCode.InternalServerError);
        var clients = OpenSkyTransport.Clients();
        OpenSkyHttpApi transport = new OpenSkyHttpApiFixture()
            .WithCache(clients)
            .WithSource(OpenSkyTransport.Tokens(clients, scheduler, new RecordingLogger<OpenSkyTokenSource>()));

        var serverError = async () => await ((IOpenSkyApi) transport).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        (await serverError.Should().ThrowAsync<FlurlHttpException>())
            .Which.StatusCode.Should()
            .Be((int) HttpStatusCode.InternalServerError);
        await serverError.Should().NotThrowAsync<OpenSkyThrottledException>();
    }

    /// <summary>The box decisions/0001 chose, so a test asserting the request asserts a real box.</summary>
    private static readonly BoundingBox Houston = new()
    {
        LatitudeMinimum = 28.8,
        LongitudeMinimum = -96.0,
        LatitudeMaximum = 30.4,
        LongitudeMaximum = -94.2,
    };
}

/// <summary>Builds the transport, so a constructor change edits this and not every test.</summary>
[AutoFixture(typeof(OpenSkyHttpApi))]
internal partial class OpenSkyHttpApiFixture
{
    public OpenSkyHttpApiFixture()
    {
        WithCache(OpenSkyTransport.Clients());
        WithLogger(new RecordingLogger<OpenSkyHttpApi>());
        WithSource(Answering("a-token"));
    }

    /// <summary>A token source that hands back one token and never refreshes.</summary>
    /// <param name="token">The token every request carries.</param>
    /// <returns>The substituted token source.</returns>
    private static IOpenSkyTokenSource Answering(string token)
    {
        var tokens = Substitute.For<IOpenSkyTokenSource>();

        tokens.Current(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(token));

        return tokens;
    }
}
