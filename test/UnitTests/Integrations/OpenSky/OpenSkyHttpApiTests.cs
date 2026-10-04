using AwesomeAssertions;
using Flurl.Http.Configuration;
using Flurl.Http.Testing;
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
        IOpenSkyApi transport = new OpenSkyHttpApi(Cache());

        // When
        var result = await transport.GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        result.IsRight.Should().BeTrue();
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
    public async Task GivenAThrottledResponse_WhenAPollIsSent_ThenTheRetryAfterHeaderArrivesAsDataRatherThanAnException()
    {
        // Given
        using var http = new HttpTest();
        http.RespondWith(
            status: 429,
            headers: new Dictionary<string, string> { [OpenSkyHttpApi.RetryAfterHeader] = "42" });
        IOpenSkyApi transport = new OpenSkyHttpApi(Cache());

        // When
        var result = await transport.GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        result.IsLeft.Should().BeTrue();
        result.Match(Right: static _ => TimeSpan.MinValue, Left: static throttled => throttled.RetryAfter)
            .Should().Be(TimeSpan.FromSeconds(42));
    }

    private static IFlurlClientCache Cache() =>
        new FlurlClientCache().Add(OpenSkyHttpApi.ClientName, "https://opensky.invalid/api");
}
