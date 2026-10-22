using System.Text.Json;
using System.Text.Json.Nodes;

namespace Transporter.UnitTests.Integrations.AisStream;

/// <summary>Synthetic frames and settings for the vessel connection boundary.</summary>
internal static class VesselPayloads
{
    public static readonly string CompressionDenied =
        """{"MessageType":"SubscriptionConfirmation","Message":{"CompressionEnabled":false}}""";

    public static readonly string BaseStationReport = """
        {
          "MessageType": "BaseStationReport",
          "MetaData": {"MMSI": 366999001},
          "Message": {"BaseStationReport": {"Latitude": 29.8, "Longitude": -95.1}}
        }
        """;

    public static readonly string PositionReport = """
        {
          "MessageType": "PositionReport",
          "MetaData": {"MMSI": 366999001, "latitude": 29.70, "longitude": -95.00},
          "Message": {"PositionReport": {"UserID": 366999001, "Latitude": 29.70, "Longitude": -95.00, "Sog": 12.4, "Cog": 86.7, "TrueHeading": 87}}
        }
        """;

    public static readonly string MovedPositionReport = """
        {
          "MessageType": "PositionReport",
          "MetaData": {"MMSI": 366999001, "latitude": 29.71, "longitude": -95.01},
          "Message": {"PositionReport": {"UserID": 366999001, "Latitude": 29.71, "Longitude": -95.01, "Sog": 13.4, "Cog": 87.7, "TrueHeading": 88}}
        }
        """;

    public static readonly string SecondPositionReport = """
        {
          "MessageType": "PositionReport",
          "MetaData": {"MMSI": 366999002, "latitude": 29.40, "longitude": -94.90},
          "Message": {"PositionReport": {"UserID": 366999002, "Latitude": 29.40, "Longitude": -94.90, "Sog": 9.1, "Cog": 90.0, "TrueHeading": 90}}
        }
        """;

    public static readonly string StaticData = """
        {
          "MessageType": "ShipStaticData",
          "MetaData": {"MMSI": 366999003},
          "Message": {"ShipStaticData": {"UserID": 366999003, "Name": "GULF TRADER", "Type": 70}}
        }
        """;

    public static readonly string PositionBeforeStaticData = """
        {
          "MessageType": "PositionReport",
          "MetaData": {"MMSI": 366999003, "latitude": 29.40, "longitude": -94.90},
          "Message": {"PositionReport": {"UserID": 366999003, "Latitude": 29.40, "Longitude": -94.90, "Sog": 8.0, "Cog": 90.0, "TrueHeading": 90}}
        }
        """;

    public static readonly string PackagedSettings = """{"AisStream":{"ReconnectAfter":"00:00:09"}}""";

    public static readonly string UserSecrets = """{"AisStream":{"ApiKey":"synthetic-credential"}}""";

    public static string WithReportedTime(string frame, object? reported)
    {
        var message = JsonNode.Parse(frame)!;
        message["MetaData"]!["time_utc"] = JsonSerializer.SerializeToNode(reported);
        return message.ToJsonString();
    }
}
