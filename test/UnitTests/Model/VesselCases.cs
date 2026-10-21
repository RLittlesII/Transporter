using LanguageExt;

namespace Transporter.UnitTests.Model;

internal sealed class VesselNameCases : TheoryData<Option<string>, string>
{
    public VesselNameCases()
    {
        Add("  BAY RUNNER  ", "BAY RUNNER");
        Add(Option<string>.None, "366999005");
        Add("   ", "366999005");
        Add("BAY RUNNER@@@@", "BAY RUNNER");
        Add("@@@@@@@@", "366999005");
    }
}

internal sealed class VesselShipTypeCases : TheoryData<int, string>
{
    public VesselShipTypeCases()
    {
        Add(15, "Unknown");
        Add(20, "Wing in ground");
        Add(30, "Fishing");
        Add(31, "Towing");
        Add(37, "Pleasure craft");
        Add(39, "Unknown");
        Add(40, "High-speed craft");
        Add(52, "Tug");
        Add(56, "Unknown");
        Add(60, "Passenger");
        Add(70, "Cargo");
        Add(80, "Tanker");
        Add(90, "Other");
        Add(100, "Unknown");
    }
}

internal sealed class VesselMidCases : TheoryData<string, string>
{
    public VesselMidCases()
    {
        Add("366999001", "United States of America");
        Add("201999001", "Albania (Republic of)");
        Add("306999001", "Netherlands (Kingdom of the)");
        Add("503999001", "Australia");
        Add("636999001", "Liberia (Republic of)");
        Add("775999001", "Venezuela (Bolivarian Republic of)");
        Add("999999001", "Unknown");
    }
}
