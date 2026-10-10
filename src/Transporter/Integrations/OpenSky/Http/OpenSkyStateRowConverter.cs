using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Transporter.Integrations.OpenSky.Contracts;

namespace Transporter.Integrations.OpenSky.Http;

internal sealed class OpenSkyStateRowConverter : JsonConverter<OpenSkyStateRow>
{
    /// <inheritdoc/>
    public override OpenSkyStateRow Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"A state row is a JSON array of positional elements; found {reader.TokenType}.");
        }

        var elements = new List<JsonElement>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            elements.Add(JsonElement.ParseValue(ref reader));
        }

        return new OpenSkyStateRow(elements);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, OpenSkyStateRow value, JsonSerializerOptions options) =>
        throw new NotSupportedException("A state row is read from the provider and never written back to it.");
}
