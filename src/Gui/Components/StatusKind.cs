namespace Gui.Components;

/// <summary>Every status the page can show, each told by a glyph and a word as well as a colour (B-032).</summary>
public enum StatusKind
{
    /// <summary>Reported within the stale threshold; neutral on purpose.</summary>
    Fresh,

    /// <summary>Changed in the latest changeset.</summary>
    Updated,

    /// <summary>Silent past the threshold, marked and kept (B-008).</summary>
    Stale,

    /// <summary>Reporting no position.</summary>
    NoFix,

    /// <summary>Added in the latest changeset.</summary>
    Added,

    /// <summary>Removed in the latest changeset.</summary>
    Removed,

    /// <summary>The provider is refusing polls.</summary>
    Throttled,

    /// <summary>Nothing has arrived for longer than the threshold.</summary>
    Quiet,
}
