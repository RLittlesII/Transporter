using System.Reactive.Concurrency;
using System.Threading;
using CommunityToolkit.Maui.Markup;
using Gui.Container;
using Gui.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using Transponder.Container;
using Transponder.Tracking.Container;

namespace Gui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var configuration = Settings();
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMarkup()

            // The system is where time and failure live. One actor so far: the swap, which is told
            // rather than asked (0006, fleet-dashboard B-016). Which types it reaches is Transponder's
            // to say, because the actor and the decorator it is handed are both internal there.
            .AddAkkaHost(
                "Transponder",
                static (system, registry, resolver) => registry.AddFleetTrackingActors(system, resolver))

            // Everything but the page is composed in Transponder, where a test builds the same graph
            // (0047). The scheduler is the head's because only it knows the thread that owns the
            // window: both heads are Apple-only, and the UIKit context is installed on the main
            // thread before this runs.
            .AddUserInterface(collection => collection
                .AddTransponder(configuration, new SynchronizationContextScheduler(SynchronizationContext.Current!))
                .AddTransient<FleetPage>())
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

    // The settings packaged beside the application: the base URL, the poll interval and the box,
    // and no credential, so the application starts and a poll needing one fails instead (0047).
    private static IConfiguration Settings()
    {
        using var settings = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();

        return new ConfigurationBuilder().AddJsonStream(settings).Build();
    }
}
