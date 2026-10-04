using AwesomeAssertions;
using Transponder.Integrations.OpenSky.Contracts;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class OpenSkyApiFakeTests
{
    [Fact]
    public async Task GivenAFakeWithNoResponseConfigured_WhenTheEndpointIsCalled_ThenItThrowsNamingTheUnsetResponse()
    {
        // Given
        IOpenSkyApi fake = new OpenSkyApiFake();

        // When
        var call = async () => await fake.GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        (await call.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*NextStates*");
        fake.GetType().Assembly.IsDynamic.Should().BeFalse();
        fake.GetType().Assembly.Should().BeSameAs(typeof(OpenSkyApiFakeTests).Assembly);
    }
}
