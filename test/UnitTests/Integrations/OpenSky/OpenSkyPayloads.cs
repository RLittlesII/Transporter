namespace Transponder.UnitTests.Integrations.OpenSky;

/// <summary>
/// Every synthetic <c>/states/all</c> payload a test reads, and the builders that vary one element
/// of a row.
/// </summary>
/// <remarks>
/// Static fields rather than committed <c>.json</c> files: a unit test that reads from disk is a
/// unit test with a dependency on the file system, and however small the cost of one read is, it is
/// paid by every test on every run. Keeping the payloads here also puts the row a test is about
/// next to the index table it is read against.
/// <para>
/// Every value is invented — callsigns, <c>icao24</c> values, countries and positions
/// (<c>transponder-conventions</c> § testing).
/// </para>
/// </remarks>
public static class OpenSkyPayloads
{
    /// <summary>The reported time every payload here carries, as the wire carries it.</summary>
    public const long ReportedTime = 1791124330L;

    /// <summary>
    /// Three readable rows: one complete, one reporting almost nothing, one in between.
    /// </summary>
    public static readonly OpenSkyPayload ThreeRows = """
        {
          "time": 1791124330,
          "states": [
            ["a1b2c3", "TRN0001 ", "Testland", 1791124315, 1791124320, -95.3698, 29.7604, 1234.5, false, 128.6, 91.2, -1.3, null, 1250.0, "0021", false, 0, 1],
            ["d4e5f6", null, "Testland", null, 1791124310, null, null, null, true, null, null, null, null, null, null, false, 0, 2],
            ["070809", "TRN0003 ", "Exampleland", 1791124300, 1791124305, -95.1, 29.9, 900.0, false, 100.0, 270.0, 0.0, null, 915.0, null, false, 2, 3]
          ]
        }
        """;

    /// <summary>
    /// The same first row, seventeen elements long: the provider answered without the category.
    /// </summary>
    public static readonly OpenSkyPayload SeventeenElementRow = """
        {
          "time": 1791124330,
          "states": [
            ["a1b2c3", "TRN0001 ", "Testland", 1791124315, 1791124320, -95.3698, 29.7604, 1234.5, false, 128.6, 91.2, -1.3, null, 1250.0, "0021", false, 0]
          ]
        }
        """;

    /// <summary>
    /// A second poll against <see cref="ThreeRows"/>: <c>a1b2c3</c> unchanged, <c>d4e5f6</c> at a
    /// new barometric altitude, <c>070809</c> gone, <c>b1c2d3</c> newly present.
    /// </summary>
    public static readonly OpenSkyPayload SecondPoll = """
        {
          "time": 1791124345,
          "states": [
            ["a1b2c3", "TRN0001 ", "Testland", 1791124315, 1791124320, -95.3698, 29.7604, 1234.5, false, 128.6, 91.2, -1.3, null, 1250.0, "0021", false, 0, 1],
            ["d4e5f6", null, "Testland", null, 1791124310, null, null, 10668.0, true, null, null, null, null, null, null, false, 0, 2],
            ["b1c2d3", "TRN0004 ", "Testland", 1791124340, 1791124344, -95.2, 29.8, 600.0, false, 90.0, 180.0, 1.0, null, 610.0, "1200", false, 0, 1]
          ]
        }
        """;

    /// <summary>A payload carrying the reported time and no rows at all.</summary>
    public static readonly OpenSkyPayload NoRows = $"{{ \"time\": {ReportedTime}, \"states\": [] }}";

    /// <summary>The instant <see cref="ReportedTime"/> is, for an assertion to name it once.</summary>
    public static DateTimeOffset ReportedInstant => DateTimeOffset.FromUnixTimeSeconds(ReportedTime);

    /// <summary>
    /// One row, varying only the elements a test is about.
    /// </summary>
    /// <param name="category">Index 17's raw JSON, or <see langword="null"/> for a 17-element row.</param>
    /// <param name="callsign">Index 1's raw JSON.</param>
    /// <param name="squawk">Index 14's raw JSON.</param>
    /// <param name="sensors">Index 12's raw JSON.</param>
    /// <returns>A payload carrying that one row.</returns>
    public static OpenSkyPayload Row(
        string? category,
        string callsign = "\"TRN0001 \"",
        string squawk = "\"0021\"",
        string sensors = "null")
    {
        var elements = Elements(callsign: callsign, squawk: squawk, sensors: sensors);

        return Payload(category is null ? elements : [.. elements, category]);
    }

    /// <summary>
    /// <see cref="ThreeRows"/> with a fourth row that cannot be read, in one of the ways B-022 names.
    /// </summary>
    /// <param name="defect">What is wrong with the fourth row.</param>
    /// <returns>A payload of four rows, three of them readable.</returns>
    public static OpenSkyPayload FourRowsOneDefective(RowDefect defect)
    {
        var defective = defect switch
        {
            RowDefect.TwelveElements => "[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]",
            RowDefect.TwentyFiveElements => Row(string.Join(", ", Enumerable.Repeat("0", 25))),
            RowDefect.NullIdentifier => Row(string.Join(", ", Elements(icao24: "null"))),
            RowDefect.FlagIsAString => Row(string.Join(", ", Elements(icao24: "\"ff0011\"", onGround: "\"yes\""))),
            _ => throw new ArgumentOutOfRangeException(nameof(defect), defect, "No such defect."),
        };

        var readable = ThreeRows.Json[(ThreeRows.Json.IndexOf('[', StringComparison.Ordinal) + 1)..ThreeRows.Json.LastIndexOf(']')];

        return $"{{ \"time\": {ReportedTime}, \"states\": [{readable.Trim().TrimEnd(',')}, {defective}] }}";

        static string Row(string elements) => $"[{elements}]";
    }

    /// <summary>The elements of one row, in the provider's own order, as raw JSON.</summary>
    /// <param name="icao24">Index 0.</param>
    /// <param name="callsign">Index 1.</param>
    /// <param name="onGround">Index 8.</param>
    /// <param name="sensors">Index 12.</param>
    /// <param name="squawk">Index 14.</param>
    /// <returns>Seventeen elements, index 0 through index 16.</returns>
    private static string[] Elements(
        string icao24 = "\"a1b2c3\"",
        string callsign = "\"TRN0001 \"",
        string onGround = "false",
        string sensors = "null",
        string squawk = "\"0021\"") =>
    [
        icao24,
        callsign,
        "\"Testland\"",
        "1791124315",
        "1791124320",
        "-95.3698",
        "29.7604",
        "1234.5",
        onGround,
        "128.6",
        "91.2",
        "-1.3",
        sensors,
        "1250.0",
        squawk,
        "false",
        "0",
    ];

    /// <summary>Wraps row elements in the envelope the provider sends them in.</summary>
    /// <param name="elements">The row's elements, as raw JSON.</param>
    /// <returns>The payload.</returns>
    private static OpenSkyPayload Payload(string[] elements) =>
        $"{{ \"time\": {ReportedTime}, \"states\": [[{string.Join(", ", elements)}]] }}";
}
