namespace Transporter.Messages;

/// <summary>Asked of <see cref="Transporter.Tracking.Sources.SourceSwapActor"/> for what a swap can select; answered with <see cref="SwapTargets"/>.</summary>
/// <remarks>
/// <para>Asked once rather than subscribed to, because registration fixes the list for a run (ADR-0015).</para>
/// <para>
/// Declared here rather than beside the actor because a view model asks it: <c>TRN0006</c> reports
/// one that names a class under <c>Tracking</c> the seam does not publish, as
/// <see cref="DemandPoll"/> records (item 0085).
/// </para>
/// </remarks>
internal sealed class GetSwapTargets
{
    private GetSwapTargets()
    {
    }

    /// <summary>Gets the message to ask.</summary>
    public static GetSwapTargets Instance { get; } = new();
}
