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
using Transporter.Container;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Tracking.Container;

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

            // Which actors exist is Transporter's to say: they and what they are handed are internal there.
            .AddAkkaHost(
                "Transporter",
                static (system, registry, resolver) => registry
                    .AddFleetTrackingActors(system, resolver)
                    .AddOpenSkyActors(system, resolver))

            // Everything but the page is composed in Transporter, where a test builds the same graph
            // (0047). The scheduler is the head's because only it knows the thread that owns the
            // window: both heads are Apple-only, and the UIKit context is installed on the main
            // thread before this runs.
            .AddUserInterface(collection => collection
                .AddTransporter(configuration, new SynchronizationContextScheduler(SynchronizationContext.Current!))
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

    // The settings packaged beside the application carry no credential (0047). The developer's
    // user-secrets store is packaged beside them only when the build found one (B-058).
    private static IConfiguration Settings()
    {
        using var settings = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
        using var secrets = FileSystem.AppPackageFileExistsAsync("secrets.json").GetAwaiter().GetResult()
            ? FileSystem.OpenAppPackageFileAsync("secrets.json").GetAwaiter().GetResult()
            : null;

        return TransporterConfiguration.Compose(settings, secrets);
    }
}
