namespace Transporter.UnitTests.Tracking;

/// <summary>
/// How long a vehicle has been silent against whether the tracker's own default marks it, with
/// nothing configured (B-017).
/// </summary>
/// <remarks>
/// The boundary is the case worth having: four minutes and six minutes bracket the five-minute
/// default, and five itself is tolerated because the claim is "longer than the threshold".
/// </remarks>
public sealed class DefaultThresholdCases : TheoryData<int, bool>
{
    public DefaultThresholdCases()
    {
        Add(4, false);
        Add(5, false);
        Add(6, true);
        Add(60, true);
    }
}
