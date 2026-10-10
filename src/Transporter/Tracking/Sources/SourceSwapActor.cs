using System.Linq;
using Akka.Actor;
using Transporter.Messages;

namespace Transporter.Tracking.Sources;

/// <summary>Performs the swap, and answers what a swap can select (B-057, ADR-0015).</summary>
/// <remarks>A swap is an effect, so it is told; the targets are asked, because registration fixes them.</remarks>
internal sealed class SourceSwapActor : ReceiveActor
{
    /// <summary>Initializes a new instance of the <see cref="SourceSwapActor"/> class.</summary>
    /// <param name="source">The decorator that selects among the registered strategies.</param>
    public SourceSwapActor(SwappingTrackerSource source)
    {
        var targets = new SwapTargets(
            source.Entries.Select(static (entry, index) => new SwapTarget(entry.Name, index)).ToArray());

        Receive<GetSwapTargets>(_ => Sender.Tell(targets));
        Receive<SwapSource>(message => source.Select(source.Entries[message.Target.Index]));
    }
}
