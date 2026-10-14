namespace Transporter.UnitTests.Integrations.OpenSky.Replay;

public sealed class RecordingInspectionFailureCases : TheoryData<bool, bool>
{
    public RecordingInspectionFailureCases()
    {
        Add(false, false);
        Add(false, true);
        Add(true, false);
        Add(true, true);
    }
}
