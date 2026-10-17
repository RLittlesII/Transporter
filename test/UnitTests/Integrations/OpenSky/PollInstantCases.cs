namespace Transporter.UnitTests.Integrations.OpenSky;

/// <summary>Each outcome must carry request start whether cadence or a demand initiated it.</summary>
public sealed class PollInstantCases : TheoryData<bool, int>
{
    public PollInstantCases()
    {
        Add(false, 200);
        Add(true, 200);
        Add(false, 429);
        Add(true, 429);
        Add(false, 500);
        Add(true, 500);
    }
}
