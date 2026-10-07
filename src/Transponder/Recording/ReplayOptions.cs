namespace Transponder.Recording;

/// <summary>
/// Which recording each replay source reads (<c>adr/0001</c> item 1, B-024).
/// </summary>
/// <remarks>
/// <para>
/// One key per source and no default compiled in: a source with no recording named is not
/// registered and therefore is not selectable, which is a visible absence rather than a wrong
/// recording. A plausible-looking default is the failure this type exists to make impossible — the
/// same call <c>aircraft-source</c> B-050 makes for the bounding box.
/// </para>
/// <para>
/// The recordings root is not here. It lives on <see cref="RecordingOptions"/>, because a rehearsal
/// writes and a replay reads the same place (<c>adr/0001</c> item 2), and two types each carrying a
/// root is two places for one answer.
/// </para>
/// <para>
/// Nothing here decides <em>whether</em> replay is live. It names which recording a replay source
/// would read; the selector decides which source is live (B-015), and a recording named and never
/// selected is normal.
/// </para>
/// </remarks>
internal sealed class ReplayOptions
{
    /// <summary>The configuration section this binds under.</summary>
    internal const string Section = "Replay";

    /// <summary>
    /// Gets or sets the aircraft recording's file name. Null until configuration names one, and no
    /// default is compiled in.
    /// </summary>
    public string? Aircraft { get; set; }

    /// <summary>Gets or sets the vessel recording's file name. Null until configuration names one.</summary>
    public string? Vessels { get; set; }
}
