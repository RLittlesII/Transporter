namespace Gui.Components;

/// <summary>What the platform says about motion, read where a view is about to animate (B-038).</summary>
internal static class Motion
{
    /// <summary>Whether the person has asked the platform for reduced motion.</summary>
    /// <returns><see langword="true"/> when nothing should move.</returns>
    /// <remarks>Read each time rather than cached, so a change in Settings applies to the next pulse.</remarks>
    public static bool IsReduced()
    {
#if IOS || MACCATALYST
        return UIKit.UIAccessibility.IsReduceMotionEnabled;
#else
        return false;
#endif
    }
}
