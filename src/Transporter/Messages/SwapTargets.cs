using System.Collections.Generic;

namespace Transporter.Messages;

/// <summary>What <see cref="Transporter.Tracking.Sources.SourceSwapActor"/> answers to <see cref="GetSwapTargets"/>: one target per registered strategy, in registration order.</summary>
/// <param name="Targets">The targets.</param>
internal sealed record SwapTargets(IReadOnlyList<SwapTarget> Targets);
