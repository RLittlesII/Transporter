namespace Transporter.UnitTests.Integrations.AisStream;

/// <summary>Numeric MMSIs include both full width and a key whose first zero is lost in JSON.</summary>
public sealed class VesselMmsiCases : TheoryData<string, string>
{
    public VesselMmsiCases()
    {
        Add(VesselPayloads.PositionReport, "366999001");
        Add(VesselPayloads.PositionReport.Replace("366999001", "36999001", StringComparison.Ordinal), "036999001");
    }
}

/// <summary>Each AIS unavailable encoding and each missing coordinate has its own boundary case.</summary>
public sealed class VesselUnavailableValueCases : TheoryData<string, string>
{
    public VesselUnavailableValueCases()
    {
        Add(VesselPayloads.PositionReport.Replace("29.70", "91", StringComparison.Ordinal), "position");
        Add(VesselPayloads.PositionReport.Replace("-95.00", "181", StringComparison.Ordinal), "position");
        Add(VesselPayloads.PositionReport.Replace("\"Latitude\": 29.70, ", string.Empty, StringComparison.Ordinal), "position");
        Add(VesselPayloads.PositionReport.Replace(", \"Longitude\": -95.00", string.Empty, StringComparison.Ordinal), "position");
        Add(VesselPayloads.PositionReport.Replace("\"Sog\": 12.4", "\"Sog\": 102.3", StringComparison.Ordinal), "speed");
        Add(VesselPayloads.PositionReport.Replace("\"Cog\": 86.7", "\"Cog\": 360", StringComparison.Ordinal), "course");
        Add(VesselPayloads.PositionReport.Replace("\"TrueHeading\": 87", "\"TrueHeading\": 511", StringComparison.Ordinal), "heading");
    }
}

/// <summary>Static names include whitespace, AIS padding and values that normalize to absence.</summary>
public sealed class VesselStaticNameCases : TheoryData<string, string?>
{
    public VesselStaticNameCases()
    {
        Add(VesselPayloads.StaticData.Replace("GULF TRADER", "  BAY RUNNER  ", StringComparison.Ordinal), "BAY RUNNER");
        Add(VesselPayloads.StaticData.Replace("GULF TRADER", "BAY RUNNER@@@@", StringComparison.Ordinal), "BAY RUNNER");
        Add(VesselPayloads.StaticData.Replace("GULF TRADER", "   ", StringComparison.Ordinal), null);
        Add(VesselPayloads.StaticData.Replace("GULF TRADER", "@@@@@@@@", StringComparison.Ordinal), null);
        Add(VesselPayloads.StaticData.Replace("\"Name\": \"GULF TRADER\", ", string.Empty, StringComparison.Ordinal), null);
    }
}
