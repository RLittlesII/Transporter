namespace Transporter.UnitTests.Integrations.OpenSky.Replay;

public sealed class RecordingStartupCases : TheoryData<string?, bool, bool, string>
{
    public RecordingStartupCases()
    {
        Add(null, false, false, "not there");
        Add(null, true, false, "could not be opened or read");
        Add(string.Empty, true, false, "holds no readable recorded line");
        Add("unparseable\n", true, false, "holds no readable recorded line");
        Add(ReplayRecordingCases.SpanningNinetySeconds, true, false, "covers 90 seconds");
        Add(ReplayRecordingCases.SpanningSixMinutes, true, true, "covers 360 seconds");
    }
}
