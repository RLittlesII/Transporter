namespace Transporter.Messages;

/// <summary>One thing a swap can select: a name to show, and the handle <see cref="SwapSource"/> carries back (B-057).</summary>
/// <param name="Name">The name the strategy's registration gave it.</param>
/// <param name="Index">The entry's position in registration order; opaque to everything above the actor.</param>
internal sealed record SwapTarget(string Name, int Index);
