using AwesomeAssertions;
using LanguageExt;
using Transponder.Integrations.OpenSky.Contracts;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class OpenSkyApiContractTests
{
    [Fact]
    public void GivenTheContract_WhenItsMethodsAreInspected_ThenOnePerEndpointReturnsTaskAndTakesCancellationLast()
    {
        // Given
        var contract = typeof(IOpenSkyApi);

        // When
        var methods = contract.GetMethods();

        // Then
        methods.Should().ContainSingle();

        var endpoint = methods.Single();
        endpoint.Name.Should().Be("GetStates").And.NotEndWith("Async");
        endpoint.ReturnType.Should().Be(typeof(Task<Either<OpenSkyThrottled, OpenSkyStatesResponse>>));
        endpoint.GetParameters()[^1].ParameterType.Should().Be(typeof(CancellationToken));
    }
}
