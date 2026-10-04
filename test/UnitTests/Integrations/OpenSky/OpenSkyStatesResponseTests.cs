using System.Text.Json;
using AwesomeAssertions;
using Transponder.Integrations.OpenSky.Contracts;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class OpenSkyStatesResponseTests
{
    [Fact]
    public void GivenAReportedTimeAndThreeRows_WhenTheResponseIsRead_ThenBothArriveNamedAsTheProviderNamesThem()
    {
        // Given
        var payload = File.ReadAllText(Path.Combine("Integrations", "OpenSky", "Fixtures", "states-three-rows.json"));

        // When
        var response = JsonSerializer.Deserialize<OpenSkyStatesResponse>(payload);

        // Then
        response.Should().NotBeNull();
        DateTimeOffset.FromUnixTimeSeconds(response!.Time)
            .Should().Be(new DateTimeOffset(2026, 10, 4, 14, 32, 10, TimeSpan.Zero));
        response.States.Should().HaveCount(3);
        response.States[0][0].GetString().Should().Be("a1b2c3");
        response.States[0].Count.Should().Be(18);
    }
}
