namespace Transporter.UnitTests;

/// <summary>
/// The name of each message a view model tells the swap actor or reads from its answer
/// (`fleet-dashboard` section 7, the swap picker; item 0085).
/// </summary>
/// <remarks>
/// Names rather than types, so the test that reads them still compiles and still says something
/// when one is declared in the wrong namespace, which is the case it exists for.
/// </remarks>
public sealed class SwapMessageCases : TheoryData<string>
{
    public SwapMessageCases()
    {
        Add("GetSwapTargets");
        Add("SwapTargets");
        Add("SwapTarget");
        Add("SwapSource");
    }
}
