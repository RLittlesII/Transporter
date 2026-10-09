using System.Collections.Generic;

namespace Transponder.Tracking.Sources;

/// <summary>What <see cref="SourceSwapActor"/> answers to <see cref="GetSwapTargets"/>: one target per registered strategy, in registration order.</summary>
/// <param name="Targets">The targets.</param>
internal sealed record SwapTargets(IReadOnlyList<SwapTarget> Targets);
