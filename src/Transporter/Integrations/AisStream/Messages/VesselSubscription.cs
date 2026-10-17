using System.Text.Json;
using Transporter.Integrations.OpenSky.Model;

namespace Transporter.Integrations.AisStream.Messages;

internal static class VesselSubscription
{
    public static string Write(string apiKey, BoundingBox box) =>
        JsonSerializer.Serialize(new
        {
            APIKey = apiKey,
            BoundingBoxes = new[]
            {
                new[]
                {
                    new[] { box.LatitudeMinimum, box.LongitudeMinimum },
                    new[] { box.LatitudeMaximum, box.LongitudeMaximum },
                },
            },
            FilterMessageTypes = new[] { "PositionReport", "ShipStaticData" },
        });
}
