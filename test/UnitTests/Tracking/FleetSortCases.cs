namespace Transporter.UnitTests.Tracking;

/// <summary>
/// Each sortable column the aircraft description offers, against the order it puts the two aircraft
/// the sort tests report (B-009, B-010).
/// </summary>
/// <remarks>
/// The two are built so that no two columns agree: callsign puts <c>a1b2c3</c> first, country puts
/// <c>d4e5f6</c> first, and last contact puts <c>a1b2c3</c> first — so a comparer wired to the wrong
/// column fails a case rather than passing by coincidence.
/// </remarks>
public sealed class SortedColumnCases : TheoryData<string, string>
{
    public SortedColumnCases()
    {
        Add("Callsign", "a1b2c3,d4e5f6");
        Add("Origin country", "d4e5f6,a1b2c3");
        Add("Last contact", "a1b2c3,d4e5f6");
    }
}
