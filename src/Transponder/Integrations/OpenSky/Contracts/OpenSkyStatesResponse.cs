using System.Text.Json.Serialization;

namespace Transponder.Integrations.OpenSky.Contracts;

internal sealed record OpenSkyStatesResponse
{
    [JsonPropertyName("time")]
    public required long Time { get; init; }

    [JsonPropertyName("states")]
    public required IReadOnlyList<OpenSkyStateRow> States { get; init; }
}
