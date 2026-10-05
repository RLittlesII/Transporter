using CommunityToolkit.Maui.Markup;
using Gui.Container;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Transponder.Features.Demo.Actors;
using Transponder.Features.Demo.ViewModels;

namespace Gui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMarkup()
            .AddAkkaHost("Transponder", static (system, registry) => registry.Register<ClickActor>(system.ActorOf(ClickActor.Props)))
            .AddUserInterface(static collection => collection.AddTransient<MainPage>().AddTransient<DemoViewModel>())
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
