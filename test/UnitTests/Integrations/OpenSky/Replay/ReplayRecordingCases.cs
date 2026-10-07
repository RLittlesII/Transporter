using Transponder.UnitTests.Integrations.OpenSky;

namespace Transponder.UnitTests.Integrations.OpenSky.Replay;

/// <summary>
/// Every synthetic recording a replay test reads, in the NDJSON shape ADR-0004 fixes: one line per
/// payload, the arrival instant beside the provider's own JSON.
/// </summary>
/// <remarks>
/// <para>
/// Built from <see cref="OpenSkyPayloads"/> wherever the payload itself does not matter, so a
/// recording and a live response in two tests are the same bytes rather than two transcriptions of
/// one payload.
/// </para>
/// <para>
/// A rehearsal recording is operational data and is never a fixture (replay-source B-006): every
/// line here is invented, and no test reads a file.
/// </para>
/// <para>
/// Every arrival instant carries the same milliseconds. A recorded gap of 15.005 seconds against an
/// advance of fifteen hangs the run with no test named, which is
/// [lesson 0019](../../../../../.spec/lessons/0019-an-advance-that-stops-short-hangs-instead-of-failing.md).
/// </para>
/// </remarks>
internal static class ReplayRecordingCases
{
    /// <summary>The instant the recording's first payload arrived.</summary>
    internal const string FirstArrival = "2026-10-04T14:32:11.113Z";

    /// <summary>Fifteen seconds after <see cref="FirstArrival"/>, exactly.</summary>
    internal const string SecondArrival = "2026-10-04T14:32:26.113Z";

    /// <summary>Ninety seconds after <see cref="FirstArrival"/>, exactly.</summary>
    internal const string NinetySecondsIn = "2026-10-04T14:33:41.113Z";

    /// <summary>Six minutes after <see cref="FirstArrival"/>, which is past the staleness threshold.</summary>
    internal const string SixMinutesIn = "2026-10-04T14:38:11.113Z";

    /// <summary>How long a vehicle is silent for in <see cref="SilentSixMinutesIn"/>.</summary>
    internal const long SilenceSeconds = 360;

    /// <summary>
    /// One payload recorded years after the instant it reports, so the two instants cannot be
    /// confused for one another (B-009).
    /// </summary>
    internal static readonly string ArrivedLongAfterItReports = Line("2030-01-01T00:00:00.000Z", OpenSkyPayloads.ThreeRows);

    /// <summary>Two polls, fifteen seconds apart — the recorded cadence B-007's second test is about.</summary>
    internal static readonly string TwoPollsFifteenSecondsApart = string.Concat(
        Line(FirstArrival, OpenSkyPayloads.ThreeRows),
        Line(SecondArrival, OpenSkyPayloads.SecondPoll));

    /// <summary>
    /// Two polls six minutes apart, which is the shortest recording a source may be registered over:
    /// a recording that does not outlast the staleness threshold cannot show an aircraft go quiet
    /// (B-005, B-026).
    /// </summary>
    internal static readonly string SpanningSixMinutes = string.Concat(
        Line(FirstArrival, OpenSkyPayloads.ThreeRows),
        Line(SixMinutesIn, OpenSkyPayloads.SecondPoll));

    /// <summary>
    /// Two polls ninety seconds apart: a real recording, cut short — the test capture taken after
    /// the rehearsal, which is the one the startup report exists to catch (B-026).
    /// </summary>
    internal static readonly string SpanningNinetySeconds = string.Concat(
        Line(FirstArrival, OpenSkyPayloads.ThreeRows),
        Line(NinetySecondsIn, OpenSkyPayloads.SecondPoll));

    /// <summary>
    /// Two polls fifteen seconds apart whose one aircraft last reported at the first, so the second
    /// payload's reported time puts it six minutes silent — past the tracker's five (B-010).
    /// </summary>
    internal static readonly string SilentSixMinutesIn = string.Concat(
        Line(FirstArrival, Reporting(OpenSkyPayloads.ReportedTime, OpenSkyPayloads.ReportedTime)),
        Line(SecondArrival, Reporting(OpenSkyPayloads.ReportedTime + SilenceSeconds, OpenSkyPayloads.ReportedTime)));

    /// <summary>Composes one recorded line, in the shape the recorder writes.</summary>
    /// <param name="instant">The instant the payload arrived, which paces playback and nothing else.</param>
    /// <param name="body">The payload, as the provider sent it.</param>
    /// <returns>The line, newline included.</returns>
    /// <remarks>
    /// The body is collapsed onto one line, because the format is one line per payload and a
    /// newline inside a body is the end of the line rather than whitespace inside it. The payloads
    /// here are raw string literals for a reader's sake; the provider sends one line, and so does
    /// the recorder. Every value is still the provider's own — nothing is reformatted but the
    /// line breaks.
    /// </remarks>
    internal static string Line(string instant, string body) =>
        $"{{\"receivedAt\":\"{instant}\",\"body\":{OneLine(body)}}}\n";

    /// <summary>The same JSON with its line breaks turned into the whitespace JSON allows anywhere.</summary>
    /// <param name="json">The payload, as a literal spanning lines.</param>
    /// <returns>The payload on one line.</returns>
    private static string OneLine(string json) => json.Replace("\r\n", " ").Replace("\n", " ");

    /// <summary>One aircraft, reporting a given instant and a given last contact.</summary>
    /// <param name="time">The envelope's own reported time, in the provider's seconds.</param>
    /// <param name="lastContact">When the aircraft was last heard from, in the provider's seconds.</param>
    /// <returns>The payload.</returns>
    private static string Reporting(long time, long lastContact) =>
        $"{{ \"time\": {time}, \"states\": [[\"a1b2c3\", \"TRN0001 \", \"Testland\", {lastContact}, {lastContact}, "
        + "-95.3698, 29.7604, 1234.5, false, 128.6, 91.2, -1.3, null, 1250.0, \"0021\", false, 0, 1]] }";
}
