using Gui.Theme;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace Gui.Components;

/// <summary>A status as a colour, a glyph and a word together, never one cue alone (B-032).</summary>
/// <remarks>The kind is handed in; the badge never decides a status itself (B-018).</remarks>
public sealed class StatusBadge : ContentView
{
    /// <summary>Initializes a new instance of the <see cref="StatusBadge"/> class.</summary>
    public StatusBadge()
    {
        _glyph = new Label { FontSize = 12, VerticalTextAlignment = TextAlignment.Center };
        _word = new Label { Style = FlightDeckStyles.Overline, VerticalTextAlignment = TextAlignment.Center };
        _frame = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = FlightDeck.RadiusSmall },
            Padding = new Thickness(FlightDeck.Space2, FlightDeck.Space1),
            HeightRequest = 28,
            Content = new HorizontalStackLayout { Spacing = FlightDeck.Space1, Children = { _glyph, _word } },
        };
        Content = _frame;
        HorizontalOptions = LayoutOptions.End;
        VerticalOptions = LayoutOptions.Start;
        Show();
    }

    /// <summary>The status shown.</summary>
    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind),
        typeof(StatusKind),
        typeof(StatusBadge),
        StatusKind.Fresh,
        propertyChanged: static (badge, _, _) => ((StatusBadge) badge).Show());

    /// <summary>Gets or sets the status shown.</summary>
    public StatusKind Kind
    {
        get => (StatusKind) GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private static (Color Ink, Color Fill, string Glyph, string Word) Look(StatusKind kind) => kind switch
    {
        StatusKind.Updated => (FlightDeck.Accent, FlightDeck.AccentSoft, "\u25C6", "Updated"),
        StatusKind.Stale => (FlightDeck.StatusStale, FlightDeck.StatusStaleSoft, "\u25F7", "Stale"),
        StatusKind.NoFix => (FlightDeck.InkSecondary, FlightDeck.SurfaceOverlay, "\u25CB", "No fix"),
        StatusKind.Added => (FlightDeck.StatusAdded, FlightDeck.StatusAddedSoft, "+", "Added"),
        StatusKind.Removed => (FlightDeck.StatusRemoved, FlightDeck.StatusRemovedSoft, "\u2212", "Removed"),
        StatusKind.Throttled => (FlightDeck.StatusThrottled, FlightDeck.StatusThrottledSoft, "\u29D7", "Throttled"),
        StatusKind.Quiet => (FlightDeck.InkMuted, FlightDeck.SurfaceOverlay, "\u2016", "Quiet"),
        _ => (FlightDeck.InkSecondary, FlightDeck.SurfaceOverlay, "\u25CF", "Fresh"),
    };

    private void Show()
    {
        var (ink, fill, glyph, word) = Look(Kind);
        _frame.Background = fill;
        _glyph.Text = glyph;
        _glyph.TextColor = ink;
        _word.Text = word;
        _word.TextColor = ink;
        SemanticProperties.SetDescription(this, word);
    }

    private readonly Border _frame;
    private readonly Label _glyph;
    private readonly Label _word;
}
