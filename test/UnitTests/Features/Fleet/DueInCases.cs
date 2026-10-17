namespace Transporter.UnitTests.Features.Fleet;

/// <summary>B-036: ordinary and refused waits, already-due reports and an absent poll instant.</summary>
public sealed class DueInCases : TheoryData<int?, int>
{
    public DueInCases()
    {
        Add(15, 15);
        Add(42, 42);
        Add(0, 0);
        Add(-1, 0);
        Add(null, 0);
    }
}
