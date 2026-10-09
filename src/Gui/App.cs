using Gui.Theme;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Gui;

/// <summary>The application: the Flight Deck's styles, one dark theme, and the shell.</summary>
public class App : Application
{
    /// <summary>Initializes a new instance of the <see cref="App"/> class.</summary>
    /// <remarks>Pinned to dark: there is one theme, and § 5 row 5 excludes a switch.</remarks>
    public App()
    {
        Resources.MergedDictionaries.Add(FlightDeckStyles.Implicit());
        UserAppTheme = AppTheme.Dark;
    }

    /// <inheritdoc/>
    protected override Window CreateWindow(IActivationState? activationState) => new(new AppShell());
}
