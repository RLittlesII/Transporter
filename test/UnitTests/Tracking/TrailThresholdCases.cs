namespace Transporter.UnitTests.Tracking;

/// <summary>Both directions catch a gap mark recomputed under a later threshold.</summary>
public sealed class TrailThresholdCases : TheoryData<int, int, bool>
{
    public TrailThresholdCases()
    {
        Add(5, 10, true);
        Add(10, 5, false);
    }
}
