using Akka.Actor;

namespace Transponder.Tracking.Sources;

/// <summary>Performs the swap: a swap is an effect, so it is told rather than published.</summary>
internal sealed class SourceSwapActor : ReceiveActor
{
    /// <summary>Initializes a new instance of the <see cref="SourceSwapActor"/> class.</summary>
    /// <param name="source">The decorator that selects among the registered strategies.</param>
    public SourceSwapActor(SwappingTrackerSource source) =>
        Receive<SwapSource>(message => source.Select(message.Strategy));
}
