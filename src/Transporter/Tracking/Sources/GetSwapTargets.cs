namespace Transporter.Tracking.Sources;

/// <summary>Asked of <see cref="SourceSwapActor"/> for what a swap can select; answered with <see cref="SwapTargets"/>.</summary>
/// <remarks>Asked once rather than subscribed to, because registration fixes the list for a run (ADR-0015).</remarks>
internal sealed class GetSwapTargets
{
    private GetSwapTargets()
    {
    }

    /// <summary>Gets the message to ask.</summary>
    public static GetSwapTargets Instance { get; } = new();
}
