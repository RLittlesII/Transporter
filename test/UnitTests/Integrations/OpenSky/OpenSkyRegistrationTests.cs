using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Transponder.Integrations.OpenSky.Container;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Integrations.OpenSky.Http;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class OpenSkyRegistrationTests
{
    [Fact]
    public void GivenTheConstructedChain_WhenTheContractIsResolved_ThenItsOwnImplementationArrivesAndNoImplementationTypeResolves()
    {
        // Given
        var services = new ServiceCollection().AddOpenSky("https://opensky.invalid/api");

        // When
        using var provider = services.BuildServiceProvider();
        var contract = provider.GetRequiredService<IOpenSkyApi>();

        // Then
        contract.Should().BeOfType<OpenSkyHttpApi>();
        provider.GetService(typeof(OpenSkyHttpApi)).Should().BeNull();
        services.Where(static registration => registration.ServiceType == typeof(IOpenSkyApi)).Should().ContainSingle();
    }
}
