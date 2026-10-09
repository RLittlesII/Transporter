using Gui.Theme;
using Microsoft.Maui.Controls;

namespace Gui.Components;

/// <summary>One value a card reads out: an uppercase label over a number in the monospace face.</summary>
/// <remarks>Its two labels are built once and only their text changes, so a poll re-lays nothing.</remarks>
public sealed class Readout : ContentView
{
    /// <summary>Initializes a new instance of the <see cref="Readout"/> class.</summary>
    public Readout()
    {
        _caption = new Label { Style = FlightDeckStyles.Overline };
        _value = new Label { Style = FlightDeckStyles.DataLarge, LineBreakMode = LineBreakMode.NoWrap };
        Content = new VerticalStackLayout { Spacing = 2, Children = { _caption, _value } };
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

    /// <summary>Dims the value while the vehicle is stale, so the card reads as old at a glance.</summary>
    /// <param name="stale">Whether the vehicle is stale.</param>
    public void Dim(bool stale) => _value.TextColor = stale ? FlightDeck.InkSecondary : FlightDeck.Ink;

    private readonly Label _caption;
    private readonly Label _value;
}
