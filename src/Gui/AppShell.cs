using Gui.Views;
using Microsoft.Maui.Controls;

namespace Gui;

/// <summary>The shell: one page, the fleet.</summary>
public class AppShell : Shell
{
    /// <summary>Initializes a new instance of the <see cref="AppShell"/> class.</summary>
    /// <remarks>The page is resolved from the container through its type, as the XAML this replaces did.</remarks>
    public AppShell()
    {
        Title = "Transporter";
        Items.Add(new ShellContent
        {
            Title = "Fleet",
            Route = nameof(FleetPage),
            ContentTemplate = new DataTemplate(typeof(FleetPage)),
        });
    }
}
