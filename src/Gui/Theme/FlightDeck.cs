using Microsoft.Maui.Graphics;

namespace Gui.Theme;

/// <summary>The Flight Deck design system's tokens: one dark theme, and the status hues told apart under every colour-vision type.</summary>
/// <remarks>
/// The design system's tokens.json is the source and this class mirrors it. Colour is never the only
/// cue: every status a component shows carries a glyph and a word beside its hue (B-032).
/// </remarks>
public static class FlightDeck
{
    /// <summary>The interface face, regular weight.</summary>
    public const string Sans = "OpenSansRegular";

    /// <summary>The interface face, semibold.</summary>
    public const string SansSemibold = "OpenSansSemibold";

    /// <summary>The face every ticking number uses, so digits keep their width as they change.</summary>
    /// <remarks>Menlo ships with iOS and Mac Catalyst; JetBrains Mono replaces it once its files are bundled.</remarks>
    public const string Mono = "Menlo";

    /// <summary>Icon-to-label gap.</summary>
    public const double Space1 = 4;

    /// <summary>Gap between readouts in a card.</summary>
    public const double Space2 = 8;

    /// <summary>Card padding and the gap between controls.</summary>
    public const double Space3 = 12;

    /// <summary>Gap between cards.</summary>
    public const double Space4 = 16;

    /// <summary>Panel padding; gap between page sections.</summary>
    public const double Space6 = 24;

    /// <summary>Chips, badges, inputs.</summary>
    public const double RadiusSmall = 6;

    /// <summary>Buttons and cards.</summary>
    public const double RadiusMedium = 10;

    /// <summary>Panels.</summary>
    public const double RadiusLarge = 14;

    /// <summary>The least height and width of every control.</summary>
    public const double Touch = 44;

    /// <summary>The least width of a card; the grid adds a column each time another fits.</summary>
    public const double CardMinimumWidth = 320;

    /// <summary>Gets the page background: blue-grey, never pure black, so text does not halo.</summary>
    public static Color Canvas { get; } = Color.FromArgb("#0f1318");

    /// <summary>Gets the panel fill: the top bar, the summary strip, the detail pane.</summary>
    public static Color Surface { get; } = Color.FromArgb("#151b22");

    /// <summary>Gets the fill of anything sitting on a panel: cards, inputs, chips.</summary>
    public static Color SurfaceRaised { get; } = Color.FromArgb("#1c232c");

    /// <summary>Gets the lightest surface: pressed states, toasts, popovers.</summary>
    public static Color SurfaceOverlay { get; } = Color.FromArgb("#242d38");

    /// <summary>Gets the decorative hairline; never a control's only boundary.</summary>
    public static Color Line { get; } = Color.FromArgb("#2a333e");

    /// <summary>Gets a control's border, 3:1 or more on every surface.</summary>
    public static Color LineStrong { get; } = Color.FromArgb("#687587");

    /// <summary>Gets primary text and the live numbers.</summary>
    public static Color Ink { get; } = Color.FromArgb("#e3e8ee");

    /// <summary>Gets secondary text: country, units, captions.</summary>
    public static Color InkSecondary { get; } = Color.FromArgb("#aab4bf");

    /// <summary>Gets labels above readouts and placeholder text.</summary>
    public static Color InkMuted { get; } = Color.FromArgb("#8b96a3");

    /// <summary>Gets the one interactive hue: focus, selection, the primary button.</summary>
    public static Color Accent { get; } = Color.FromArgb("#6cbcf0");

    /// <summary>Gets the selected card's fill and the update tint.</summary>
    public static Color AccentSoft { get; } = Color.FromArgb("#16344a");

    /// <summary>Gets text on an accent fill.</summary>
    public static Color OnAccent { get; } = Color.FromArgb("#0f1318");

    /// <summary>Gets stale: silent past the threshold, marked and kept (B-008).</summary>
    public static Color StatusStale { get; } = Color.FromArgb("#e9a23b");

    /// <summary>Gets the fill behind a stale badge.</summary>
    public static Color StatusStaleSoft { get; } = Color.FromArgb("#3d2b10");

    /// <summary>Gets added in the latest changeset.</summary>
    public static Color StatusAdded { get; } = Color.FromArgb("#3ec29a");

    /// <summary>Gets the fill behind an added badge.</summary>
    public static Color StatusAddedSoft { get; } = Color.FromArgb("#11342b");

    /// <summary>Gets removed in the latest changeset.</summary>
    public static Color StatusRemoved { get; } = Color.FromArgb("#d98bb6");

    /// <summary>Gets the fill behind a removed badge.</summary>
    public static Color StatusRemovedSoft { get; } = Color.FromArgb("#3a2232");

    /// <summary>Gets throttled: the provider refused a poll.</summary>
    public static Color StatusThrottled { get; } = Color.FromArgb("#efe35a");

    /// <summary>Gets the fill behind a throttled badge.</summary>
    public static Color StatusThrottledSoft { get; } = Color.FromArgb("#38351a");
}
