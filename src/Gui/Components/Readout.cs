using Gui.Theme;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace Gui.Components;

/// <summary>One value a card reads out: an uppercase label over a number in the monospace face, and the change under it.</summary>
/// <remarks>
/// Its labels are built once and only their text changes, so a poll re-lays nothing. The change line
/// keeps its height when empty, so a change arriving or leaving moves no neighbour (B-006, B-038).
/// </remarks>
public sealed class Readout : ContentView
{
    /// <summary>Initializes a new instance of the <see cref="Readout"/> class.</summary>
    public Readout()
    {
        _caption = new Label { Style = FlightDeckStyles.Overline };
        _value = new Label { Style = FlightDeckStyles.DataLarge, LineBreakMode = LineBreakMode.NoWrap };
        _change = new Label
        {
            Style = FlightDeckStyles.DataSmall,
            TextColor = FlightDeck.Accent,
            LineBreakMode = LineBreakMode.NoWrap,
            HeightRequest = ChangeHeight,
        };
        _cell = new Border
        {
            Background = Colors.Transparent,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = FlightDeck.RadiusSmall },
            Padding = new(FlightDeck.Space1, 2),
            Margin = new(-FlightDeck.Space1, -2),
            Content = new VerticalStackLayout { Spacing = 2, Children = { _caption, _value, _change } },
        };
        Content = _cell;
    }

    /// <summary>The label above the value.</summary>
    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
        nameof(Caption),
        typeof(string),
        typeof(Readout),
        string.Empty,
        propertyChanged: static (readout, _, value) => ((Readout) readout)._caption.Text = (string) value);

    /// <summary>The value, already display-formatted.</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(string),
        typeof(Readout),
        string.Empty,
        propertyChanged: static (readout, _, value) => ((Readout) readout)._value.Text = (string) value);

    /// <summary>The change the description names for the value, such as "▲ +120 ft"; empty when there is none (B-038).</summary>
    public static readonly BindableProperty ChangeProperty = BindableProperty.Create(
        nameof(Change),
        typeof(string),
        typeof(Readout),
        string.Empty,
        propertyChanged: static (readout, _, value) => ((Readout) readout).Show((string) value));

    /// <summary>Gets or sets the label above the value.</summary>
    public string Caption
    {
        get => (string) GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>Gets or sets the value.</summary>
    public string Value
    {
        get => (string) GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Gets or sets the change shown under the value.</summary>
    public string Change
    {
        get => (string) GetValue(ChangeProperty);
        set => SetValue(ChangeProperty, value);
    }

    /// <summary>Dims the value while the vehicle is stale, so the card reads as old at a glance.</summary>
    /// <param name="stale">Whether the vehicle is stale.</param>
    public void Dim(bool stale) => _value.TextColor = stale ? FlightDeck.InkSecondary : FlightDeck.Ink;

    /// <summary>Tints the cell once and fades it back, unless the platform asks for reduced motion (B-038).</summary>
    /// <remarks>
    /// The card calls this when its bound element is a new reading of the same vehicle and this
    /// readout names a change; nothing here keeps a timer or remembers a value. Under reduced motion
    /// the change text still shows: what is removed is the motion, never the information.
    /// </remarks>
    public void Pulse()
    {
        this.AbortAnimation(PulseName);
        if (Motion.IsReduced())
        {
            _cell.Background = Colors.Transparent;
            return;
        }

        var from = FlightDeck.AccentSoft;
        new Animation(fraction => _cell.Background = from.WithAlpha((float) fraction), 1, 0)
            .Commit(this, PulseName, length: PulseLength, easing: Easing.CubicOut, finished: (_, _) => _cell.Background = Colors.Transparent);
    }

    private void Show(string change)
    {
        _change.Text = change;
        SemanticProperties.SetDescription(_change, change.Length == 0 ? null : $"changed {change}");
    }

    private const string PulseName = "readout-pulse";
    private const uint PulseLength = 1200;
    private const double ChangeHeight = 18;

    private readonly Border _cell;
    private readonly Label _caption;
    private readonly Label _value;
    private readonly Label _change;
}
