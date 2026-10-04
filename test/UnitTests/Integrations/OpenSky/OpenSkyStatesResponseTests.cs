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

    [Fact]
    public void GivenTheEnvelope_WhenItsMembersAreInspected_ThenStatesIsPositionalAndNoMemberIsAPerAircraftType()
    {
        // Given
        var envelope = typeof(OpenSkyStatesResponse);

        // When
        var members = envelope.GetProperties().Select(static member => member.Name);

        // Then
        members.Should().BeEquivalentTo("Time", "States");
        envelope.GetProperty("States")!.PropertyType.Should().Be(typeof(IReadOnlyList<OpenSkyStateRow>));
        typeof(OpenSkyStateRow).GetProperties()
            .Select(static member => member.Name)
            .Should().BeEquivalentTo("Count", "Item");
    }
}
