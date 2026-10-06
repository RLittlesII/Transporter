using System;

namespace Transponder.Tracking.Sources;

/// <summary>Told to <see cref="SourceSwapActor"/> to make one strategy live.</summary>
internal sealed class SwapSource
{
    private SwapSource(Type strategy) => Strategy = strategy;

    /// <summary>Gets the per-type seam naming the strategy to make live.</summary>
    public Type Strategy { get; }

    /// <summary>Addresses the message at one strategy.</summary>
    /// <typeparam name="TStrategy">The per-type seam naming it.</typeparam>
    /// <returns>The message to tell.</returns>
    public static SwapSource To<TStrategy>()
        where TStrategy : ITrackerSource => new(typeof(TStrategy));
}
