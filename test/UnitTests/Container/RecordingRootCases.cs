namespace Transporter.UnitTests.Container;

public sealed class RecordingRootCases : TheoryData<string?, string?, string>
{
    public RecordingRootCases()
    {
        Add(null, null, "platform/recordings");
        Add("packaged/recordings", null, "packaged/recordings");
        Add("packaged/recordings", "store/recordings", "store/recordings");
        Add(null, "store/recordings", "store/recordings");
    }
}
