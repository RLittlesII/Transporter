using System.Globalization;
using System.Text.Json;
using LanguageExt;
using Transporter.Model;

namespace Transporter.Integrations.AisStream.Messages;

internal static class VesselMessageReader
{
    public static Option<VesselMessage> Read(string frame)
    {
        try
        {
            using var document = JsonDocument.Parse(frame);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("MessageType", out var type)
                || type.ValueKind != JsonValueKind.String)
            {
                return Option<VesselMessage>.None;
            }

            var kind = type.GetString();
            if (kind is not ("PositionReport" or "ShipStaticData")
                || !root.TryGetProperty("MetaData", out var metadata)
                || metadata.ValueKind != JsonValueKind.Object)
            {
                return Option<VesselMessage>.None;
            }

            var mmsi = ReadMmsi(metadata).IfNone(string.Empty);
            if (mmsi.Length == 0
                || !root.TryGetProperty("Message", out var envelope)
                || envelope.ValueKind != JsonValueKind.Object
                || !envelope.TryGetProperty(kind, out var payload)
                || payload.ValueKind != JsonValueKind.Object)
            {
                return Option<VesselMessage>.None;
            }

            return kind == "PositionReport"
                ? ReadPosition(mmsi, payload)
                : ReadStatic(mmsi, payload);
        }
        catch (JsonException)
        {
            return Option<VesselMessage>.None;
        }
    }

    private static Option<VesselMessage> ReadPosition(string mmsi, JsonElement payload)
    {
        var latitude = Number(payload, "Latitude");
        var longitude = Number(payload, "Longitude");
        Option<GeoPosition> position = latitude is >= -90 and <= 90
            && longitude is >= -180 and <= 180
            && latitude != 91 && longitude != 181
                ? new GeoPosition(latitude.Value, longitude.Value)
                : Option<GeoPosition>.None;
        var speed = Number(payload, "Sog");
        var course = Number(payload, "Cog");
        var heading = Number(payload, "TrueHeading");
        return new VesselPositionMessage(
            mmsi,
            position,
            speed is { } s && s != 102.3 ? s : Option<double>.None,
            course is { } c && c != 360 ? c : Option<double>.None,
            heading is { } h && h != 511 ? h : Option<double>.None);
    }

    private static Option<VesselMessage> ReadStatic(string mmsi, JsonElement payload)
    {
        Option<string> name = payload.TryGetProperty("Name", out var nameValue)
            && nameValue.ValueKind == JsonValueKind.String
            && nameValue.GetString() is { } value
                ? value
                : Option<string>.None;
        Option<int> shipType = payload.TryGetProperty("Type", out var typeValue)
            && typeValue.ValueKind == JsonValueKind.Number
            && typeValue.TryGetInt32(out var code)
                ? code
                : Option<int>.None;
        return new VesselStaticMessage(mmsi, name, shipType);
    }

    private static Option<string> ReadMmsi(JsonElement metadata)
    {
        if (!metadata.TryGetProperty("MMSI", out var value))
        {
            return Option<string>.None;
        }

        var digits = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var numeric) => numeric.ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => value.GetString(),
            _ => null,
        };
        return digits is { Length: >= 1 and <= 9 } && digits.All(char.IsAsciiDigit)
            ? digits.PadLeft(9, '0')
            : Option<string>.None;
    }

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
        && double.IsFinite(number)
            ? number
            : null;
}
