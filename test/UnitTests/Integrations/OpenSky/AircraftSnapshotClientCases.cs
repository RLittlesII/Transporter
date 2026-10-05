namespace Transponder.UnitTests.Integrations.OpenSky;

/// <summary>The ways B-022 says a row can be unreadable.</summary>
public enum RowDefect
{
    /// <summary>An element count below the seventeen the index table declares.</summary>
    TwelveElements,

    /// <summary>An element count above the eighteen the index table declares.</summary>
    TwentyFiveElements,

    /// <summary>Index 0 is <see langword="null"/>, so the row identifies no aircraft.</summary>
    NullIdentifier,

    /// <summary>Index 8 is a string where the index table declares a flag.</summary>
    FlagIsAString,
}

/// <summary>
/// Index 17 against the category it reads as: absent and present-with-zero are two answers, and
/// element count alone decides which (B-018).
/// </summary>
public sealed class CategoryCases : TheoryData<OpenSkyPayload, int?>
{
    public CategoryCases()
    {
        Add(OpenSkyPayloads.Row(null), null);
        Add(OpenSkyPayloads.Row("null"), null);
        Add(OpenSkyPayloads.Row("0"), 0);
        Add(OpenSkyPayloads.Row("3"), 3);
    }
}

/// <summary>
/// Index 1 against the callsign it reads as: the wire's padding is removed, and padding alone is
/// absent rather than empty (B-019).
/// </summary>
public sealed class CallsignCases : TheoryData<OpenSkyPayload, string?>
{
    public CallsignCases()
    {
        Add(OpenSkyPayloads.Row("1", callsign: "\"FLT0421\""), "FLT0421");
        Add(OpenSkyPayloads.Row("1", callsign: "\"FLT42   \""), "FLT42");
        Add(OpenSkyPayloads.Row("1", callsign: "\"        \""), null);
        Add(OpenSkyPayloads.Row("1", callsign: "null"), null);
    }
}

/// <summary>
/// Index 14 against the squawk it reads as: a code keeps its leading zeros, so <c>"0021"</c> is
/// four characters and never the number 21 (B-020).
/// </summary>
public sealed class SquawkCases : TheoryData<OpenSkyPayload, string?>
{
    public SquawkCases()
    {
        Add(OpenSkyPayloads.Row("1", squawk: "\"0021\""), "0021");
        Add(OpenSkyPayloads.Row("1", squawk: "\"7700\""), "7700");
        Add(OpenSkyPayloads.Row("1", squawk: "\"0000\""), "0000");
        Add(OpenSkyPayloads.Row("1", squawk: "null"), null);
    }
}

/// <summary>
/// Four rows in which one is unreadable, once per defect: each is excluded and counted, and the
/// three beside it survive (B-022).
/// </summary>
public sealed class UnreadableRowCases : TheoryData<OpenSkyPayload>
{
    public UnreadableRowCases()
    {
        foreach (var defect in Enum.GetValues<RowDefect>())
        {
            Add(OpenSkyPayloads.FourRowsOneDefective(defect));
        }
    }
}

/// <summary>
/// Whether the application offers category grouping, against the payload the provider answers
/// with: asking for extended rows is what makes a category present at all (B-025).
/// </summary>
public sealed class CategoryGroupingCases : TheoryData<bool, OpenSkyPayload, int?>
{
    public CategoryGroupingCases()
    {
        Add(true, OpenSkyPayloads.ThreeRows, 1);
        Add(false, OpenSkyPayloads.SeventeenElementRow, null);
    }
}
