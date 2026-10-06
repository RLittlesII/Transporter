using Akka.Actor;

namespace Transponder.Tracking.Sources;

/// <summary>
/// Performs the swap: the control tells this, and this tells the decorator
/// (`fleet-dashboard` B-016, § 4 row 6).
/// </summary>
/// <remarks>
/// An actor rather than an observable value because a swap is an effect, and thin on purpose — the
/// selection it performs is the decorator's and there is nothing here to supervise yet. It has no
/// <c>Props</c> of its own: a collaborator the container owns reaches an actor through Akka's
/// dependency resolver, which is <c>TrackingRegistration.AddFleetTrackingActors</c>.
/// </remarks>
internal sealed class SourceSwapActor : ReceiveActor
{
    /// <summary>Initializes a new instance of the <see cref="SourceSwapActor"/> class.</summary>
    /// <param name="source">The decorator that selects among the registered strategies.</param>
    public SourceSwapActor(SwappingTrackerSource source) =>
        Receive<SwapSource>(message => source.Select(message.Strategy));
}
