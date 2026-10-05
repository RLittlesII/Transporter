using AwesomeAssertions;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class OpenSkyStatesResponseTests
{
    /// <summary>
    /// B-001 and B-002. The envelope carries the reported time and the rows under the provider's own
    /// names, and a row is still the provider's positional shape — a count and an indexer — rather
    /// than a named per-aircraft type.
    /// </summary>
    [Fact]
    public void GivenAReportedTimeAndThreeRows_WhenTheResponseIsRead_ThenBothArriveNamedAsTheProviderNamesThem()
    {
        // Given, When
        var response = OpenSkyPayloads.ThreeRows.Response;

        // Then
        DateTimeOffset.FromUnixTimeSeconds(response.Time).Should().Be(OpenSkyPayloads.ReportedInstant);
        response.States.Should().HaveCount(3);
        response.States[0][0].GetString().Should().Be("a1b2c3");
        response.States[0].Count.Should().Be(18);
    }
}
