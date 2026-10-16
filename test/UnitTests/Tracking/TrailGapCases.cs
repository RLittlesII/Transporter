namespace Transporter.UnitTests.Tracking;

/// <summary>Below, at, and above five minutes distinguish silence from an ordinary report.</summary>
public sealed class TrailGapCases : TheoryData<double, bool>
{
    public TrailGapCases()
    {
        Add(15, false);
        Add(300, false);
        Add(450, true);
    }
}
