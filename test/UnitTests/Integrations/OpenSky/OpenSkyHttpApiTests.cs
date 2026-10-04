using AwesomeAssertions;
using Flurl.Http.Configuration;
using Flurl.Http.Testing;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
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
}

[AutoFixture(typeof(OpenSkyHttpApi))]
internal partial class OpenSkyHttpApiFixture
{
    public OpenSkyHttpApiFixture() =>
        WithCache(new FlurlClientCache().Add(OpenSkyHttpApi.ClientName, "https://opensky.invalid/api"));
}
