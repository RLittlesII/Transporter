using CommunityToolkit.Maui.Markup;
using Gui.Container;
using Gui.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Transponder.Features.Fleet.ViewModels;

namespace Gui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMarkup()

            // No actor is started until the source wiring lands (spike 0040); the system is where time and failure will live.
            .AddAkkaHost("Transponder", static (_, _) => { })
            .AddUserInterface(static collection => collection.AddTransient<FleetPage>().AddTransient<FleetViewModel>())
            .ConfigureFonts(static fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
