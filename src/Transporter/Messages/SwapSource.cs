namespace Transporter.Messages;

/// <summary>Told to <see cref="Transporter.Tracking.Sources.SourceSwapActor"/> to make one strategy live.</summary>
internal sealed class SwapSource
{
    private SwapSource(SwapTarget target) => Target = target;

    /// <summary>Gets the target, as <see cref="Transporter.Tracking.Sources.SourceSwapActor"/> answered it, naming the strategy to make live.</summary>
    public SwapTarget Target { get; }

    /// <summary>Addresses the message at one target.</summary>
    /// <param name="target">A target from <see cref="SwapTargets"/>.</param>
    /// <returns>The message to tell.</returns>
    public static SwapSource To(SwapTarget target) => new(target);
}
