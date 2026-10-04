using AwesomeAssertions;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Integrations.OpenSky.Contracts;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class OpenSkyApiFakeTests
{
    [Fact]
    public async Task GivenAFakeWithNoResponseConfigured_WhenTheEndpointIsCalled_ThenItThrowsNamingTheUnsetResponse()
    {
        // Given
        OpenSkyApiFake sut = new OpenSkyApiFakeFixture();

        // When
        var call = async () => await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        (await call.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*response*");
    }

    [Fact]
    public async Task GivenAFakeBuiltWithAResponse_WhenTheEndpointIsCalled_ThenThatResponseArrives()
    {
        // Given
        var configured = new OpenSkyStatesResponse { Time = 1791124330, States = [] };
        OpenSkyApiFake sut = new OpenSkyApiFakeFixture().WithResponse(configured);

        // When
        var response = await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        response.Should().BeSameAs(configured);
    }
}

[AutoFixture(typeof(OpenSkyApiFake))]
internal partial class OpenSkyApiFakeFixture;
