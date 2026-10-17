namespace Transporter.UnitTests.Integrations.AisStream;

/// <summary>A normal service close and a lost receive both require a fresh subscription.</summary>
public sealed class VesselDisconnectionCases : TheoryData<bool>
{
    public VesselDisconnectionCases()
    {
        Add(false);
        Add(true);
    }
}
