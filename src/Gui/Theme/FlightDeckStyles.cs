using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Gui.Theme;

/// <summary>The type scale and the implicit control styles, built in C# (B-002 allows no new XAML).</summary>
/// <remarks>
/// The text styles are properties a component assigns rather than keys it looks up, so a misspelt
/// style is a compile error. The implicit styles go into the application's resources once, from
/// <see cref="App"/>.
/// </remarks>
public static class FlightDeckStyles
{
    /// <summary>Gets the page and pane title, one per view.</summary>
    public static Style Title { get; } = Text(FlightDeck.SansSemibold, 24, FlightDeck.Ink);

    /// <summary>Gets group headers and panel headings.</summary>
    public static Style Heading { get; } = Text(FlightDeck.SansSemibold, 18, FlightDeck.Ink);

    /// <summary>Gets default text: place names, notices.</summary>
    public static Style Body { get; } = Text(FlightDeck.Sans, 15, FlightDeck.Ink);

    /// <summary>Gets callsigns and emphasised text.</summary>
    public static Style BodyStrong { get; } = Text(FlightDeck.SansSemibold, 15, FlightDeck.Ink);

    /// <summary>Gets the secondary line under a callsign.</summary>
    public static Style Caption { get; } = Text(FlightDeck.Sans, 13, FlightDeck.InkSecondary);

    /// <summary>Gets the uppercase label above a readout; never below 12.</summary>
    public static Style Overline { get; } = Overlined();

    /// <summary>Gets a card readout's value and the summary totals.</summary>
    public static Style DataLarge { get; } = Text(FlightDeck.Mono, 18, FlightDeck.Ink);

    /// <summary>Gets banner counts and the observed instant.</summary>
    public static Style Data { get; } = Text(FlightDeck.Mono, 15, FlightDeck.Ink);

    /// <summary>Gets coordinates, ages and other small numbers.</summary>
    public static Style DataSmall { get; } = Text(FlightDeck.Mono, 13, FlightDeck.InkSecondary);

    /// <summary>The implicit styles every control takes unless a component says otherwise.</summary>
    /// <returns>The dictionary the application merges.</returns>
    public static ResourceDictionary Implicit()
    {
        var resources = new ResourceDictionary
        {
            Derived<Page>(
                (VisualElement.BackgroundColorProperty, FlightDeck.Canvas)),
            Derived<Shell>(
                (Shell.BackgroundColorProperty, FlightDeck.Surface),
                (Shell.ForegroundColorProperty, FlightDeck.Ink),
                (Shell.TitleColorProperty, FlightDeck.Ink),
                (Shell.UnselectedColorProperty, FlightDeck.InkMuted),
                (Shell.NavBarHasShadowProperty, false)),
            Derived<Label>(
                (Label.TextColorProperty, FlightDeck.Ink),
                (Label.FontFamilyProperty, FlightDeck.Sans),
                (Label.FontSizeProperty, 15d)),
            Derived<Entry>(
                (Entry.TextColorProperty, FlightDeck.Ink),
                (Entry.PlaceholderColorProperty, FlightDeck.InkMuted),
                (VisualElement.BackgroundColorProperty, FlightDeck.SurfaceRaised),
                (Entry.FontFamilyProperty, FlightDeck.Sans),
                (Entry.FontSizeProperty, 15d),
                (VisualElement.MinimumHeightRequestProperty, FlightDeck.Touch)),
            Derived<Picker>(
                (Picker.TextColorProperty, FlightDeck.Ink),
                (Picker.TitleColorProperty, FlightDeck.InkMuted),
                (VisualElement.BackgroundColorProperty, FlightDeck.SurfaceRaised),
                (Picker.FontFamilyProperty, FlightDeck.Sans),
                (Picker.FontSizeProperty, 15d),
                (VisualElement.MinimumHeightRequestProperty, FlightDeck.Touch)),
            Derived<ActivityIndicator>(
                (ActivityIndicator.ColorProperty, FlightDeck.Accent)),
            Buttons(),
        };

        return resources;
    }

    /// <summary>The primary button: accent fill, dark label, and a muted state while disabled.</summary>
    private static Style Buttons()
    {
        var style = Derived<Button>(
            (Button.TextColorProperty, FlightDeck.OnAccent),
            (VisualElement.BackgroundColorProperty, FlightDeck.Accent),
            (Button.FontFamilyProperty, FlightDeck.SansSemibold),
            (Button.FontSizeProperty, 15d),
            (Button.CornerRadiusProperty, (int) FlightDeck.RadiusMedium),
            (Button.PaddingProperty, new Thickness(FlightDeck.Space4, 0)),
            (VisualElement.MinimumHeightRequestProperty, FlightDeck.Touch),
            (VisualElement.MinimumWidthRequestProperty, FlightDeck.Touch));
        style.Setters.Add(new Setter
        {
            Property = VisualStateManager.VisualStateGroupsProperty,
            Value = new VisualStateGroupList
            {
                new VisualStateGroup
                {
                    Name = "CommonStates",
                    States =
                    {
                        new VisualState { Name = "Normal" },
                        new VisualState
                        {
                            Name = "Disabled",
                            Setters =
                            {
                                new Setter { Property = Button.TextColorProperty, Value = FlightDeck.InkMuted },
                                new Setter { Property = VisualElement.BackgroundColorProperty, Value = FlightDeck.SurfaceOverlay },
                            },
                        },
                    },
                },
            },
        });

        return style;
    }

    private static Style Text(string family, double size, Color color) =>
        new(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.FontFamilyProperty, Value = family },
                new Setter { Property = Label.FontSizeProperty, Value = size },
                new Setter { Property = Label.TextColorProperty, Value = color },
            },
        };

    private static Style Overlined()
    {
        var style = Text(FlightDeck.SansSemibold, 12, FlightDeck.InkMuted);
        style.Setters.Add(new Setter { Property = Label.TextTransformProperty, Value = TextTransform.Uppercase });
        style.Setters.Add(new Setter { Property = Label.CharacterSpacingProperty, Value = 0.7 });

        return style;
    }

    private static Style Derived<T>(params (BindableProperty Property, object Value)[] setters)
        where T : BindableObject
    {
        var style = new Style(typeof(T)) { ApplyToDerivedTypes = true };
        foreach (var (property, value) in setters)
        {
            style.Setters.Add(new Setter { Property = property, Value = value });
        }

        return style;
    }
}
