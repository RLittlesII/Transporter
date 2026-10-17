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

    public static readonly string PackagedSettings = """{"AisStream":{"ReconnectAfter":"00:00:09"}}""";

    public static readonly string UserSecrets = """{"AisStream":{"ApiKey":"synthetic-credential"}}""";
}
