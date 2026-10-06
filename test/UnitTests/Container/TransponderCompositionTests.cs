using System.Collections.Generic;
using System.Reactive.Concurrency;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Transponder.Container;
using Transponder.Features.Fleet.ViewModels;
using Transponder.Integrations.OpenSky;
using Transponder.Tracking;

namespace Transponder.UnitTests.Container;

public class TransponderCompositionTests
{
    /// <summary>
    /// `0047`. The application shipped unable to open its window: the head registered a page and a
    /// view model over a container holding neither the tracker nor a source, and nothing executed
    /// over the registrations to say so. One resolution of the view model the page takes is what
    /// says every service beneath it is registered.
    /// </summary>
    [Fact]
    public void GivenTheApplicationsComposition_WhenTheViewModelIsResolved_ThenEveryServiceBeneathItResolves()
    {
        // Given
        using var host = Host();

        // When
        var resolve = () => host.Services.GetRequiredService<FleetViewModel>();

        // Then
        resolve.Should().NotThrow("the page takes this view model, and the window is built before anything else runs");
    }

    /// <summary>
    /// `fleet-pipeline` B-002 and § 4 row 13, read at the composition rather than at the type. A
    /// tracker built twice is two connections to the seam and two collections of the same vehicles,
    /// with no error to say which one a page is looking at.
    /// </summary>
    [Fact]
    public void GivenTheApplicationsComposition_WhenTheTrackerIsResolvedTwice_ThenItIsOneObject()
    {
        // Given
        using var host = Host();

        // When
        var first = host.Services.GetRequiredService<IFleetTracker>();
        var second = host.Services.GetRequiredService<IFleetTracker>();

        // Then
        first.Should().BeSameAs(second, "the pipeline is assembled once for the application");
    }

    /// <summary>
    /// A host composed the way the application composes it: one call to
    /// <see cref="TransponderRegistration.AddTransponder"/>, over the settings the head ships.
    /// </summary>
    /// <returns>The host, unstarted.</returns>
    /// <remarks>
    /// The configuration carries no credential, which is what the packaged asset carries too: a
    /// poll needing one fails, and nothing in this graph reads one to resolve.
    /// </remarks>
    private static IHost Host() =>
        new HostBuilder()
            .ConfigureServices(static services =>
                services.AddTransponder(Settings(), ImmediateScheduler.Instance))
            .Build();

    /// <summary>The settings the application's `appsettings.json` carries, in configuration's shape.</summary>
    /// <returns>The configuration.</returns>
    private static IConfiguration Settings() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{OpenSkyOptions.Section}:{nameof(OpenSkyOptions.BaseUrl)}"] = OpenSkyOptions.DefaultBaseUrl,
                    [$"{OpenSkyOptions.Section}:Box:LatitudeMinimum"] = "28.8",
                    [$"{OpenSkyOptions.Section}:Box:LongitudeMinimum"] = "-96.0",
                    [$"{OpenSkyOptions.Section}:Box:LatitudeMaximum"] = "30.4",
                    [$"{OpenSkyOptions.Section}:Box:LongitudeMaximum"] = "-94.2",
                })
            .Build();
}
