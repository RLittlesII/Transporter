using System.Text.Json;
using System.Text.Json.Serialization;
using Transponder.Integrations.OpenSky.Http;

namespace Transponder.Integrations.OpenSky.Contracts;

[JsonConverter(typeof(OpenSkyStateRowConverter))]
internal sealed class OpenSkyStateRow
{
    public OpenSkyStateRow(IReadOnlyList<JsonElement> elements) => _elements = elements;

    public int Count => _elements.Count;

    public JsonElement this[int index] => _elements[index];

    private readonly IReadOnlyList<JsonElement> _elements;
}
