using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace Gui;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
