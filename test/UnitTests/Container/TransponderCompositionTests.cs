using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Transponder.Container;
using Transponder.Integrations.OpenSky;
using Transponder.Tracking;
using Transponder.Tracking.Sources;

namespace Transponder.UnitTests.Container;

public class TransponderCompositionTests
{
    /// <summary>
    /// `0047`. The application shipped unable to open its window: the head registered a page and a
    /// view model over a container holding neither the tracker nor a source, and nothing executed
    /// over the registrations to say so. Resolving what the window needs from the application's own
    /// composition is what says every one of them is registered.
    /// </summary>
    /// <param name="service">The service the window needs, per <see cref="WindowServiceCases"/>.</param>
    [Theory]
    [ClassData(typeof(WindowServiceCases))]
    public void GivenTheApplicationsComposition_WhenAServiceTheWindowNeedsIsResolved_ThenItResolves(Type service)
    {
        // Given
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpenSkyOptions.Section}:{nameof(OpenSkyOptions.BaseUrl)}"] = OpenSkyOptions.DefaultBaseUrl,
                [$"{OpenSkyOptions.Section}:Box:LatitudeMinimum"] = "28.8",
                [$"{OpenSkyOptions.Section}:Box:LongitudeMinimum"] = "-96.0",
                [$"{OpenSkyOptions.Section}:Box:LatitudeMaximum"] = "30.4",
                [$"{OpenSkyOptions.Section}:Box:LongitudeMaximum"] = "-94.2",
            })
            .Build();
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddTransponder(settings, ImmediateScheduler.Instance))
            .Build();

        // When
        var resolve = () => host.Services.GetRequiredService(service);

        // Then
        resolve.Should().NotThrow("the window is built before anything else runs, and resolves this");
    }

    /// <summary>
    /// `fleet-pipeline` § 4 row 13, read at the composition rather than at the type. A tracker built
    /// twice is two connections to the seam and two collections of the same vehicles, with no error
    /// to say which one a page is looking at.
    /// </summary>
    [Fact]
    public void GivenTheApplicationsComposition_WhenTheTrackerIsResolvedTwice_ThenItIsOneObject()
    {
        // Given
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpenSkyOptions.Section}:{nameof(OpenSkyOptions.BaseUrl)}"] = OpenSkyOptions.DefaultBaseUrl,
                [$"{OpenSkyOptions.Section}:Box:LatitudeMinimum"] = "28.8",
                [$"{OpenSkyOptions.Section}:Box:LongitudeMinimum"] = "-96.0",
                [$"{OpenSkyOptions.Section}:Box:LatitudeMaximum"] = "30.4",
                [$"{OpenSkyOptions.Section}:Box:LongitudeMaximum"] = "-94.2",
            })
            .Build();
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddTransponder(settings, ImmediateScheduler.Instance))
            .Build();

        // When
        var first = host.Services.GetRequiredService<IFleetTracker>();
        var second = host.Services.GetRequiredService<IFleetTracker>();

        // Then
        first.Should().BeSameAs(second, "the pipeline is assembled once for the application");
    }

    /// <summary>
    /// B-052. A chain that compiles and a chain that runs are the same thing only if something
    /// builds it: the tracker resolves with every dependency satisfied, and what it is handed is the
    /// decorator rather than a strategy. The second clause is the one that catches ADR-0011's
    /// remaining trap — a strategy registered as <see cref="ITrackerSource"/> reaches consumers in
    /// place of the selector, and the swap then silently does nothing.
    /// </summary>
    [Fact]
    public void GivenEveryRegistrationTheApplicationMakes_WhenTheContainerIsBuilt_ThenTheFleetTrackerResolvesAndItsSourceIsTheDecorator()
    {
        // Given
        using var host = Application();

        // When
        var tracker = host.Services.GetRequiredService<IFleetTracker>();
        var source = host.Services.GetRequiredService<ITrackerSource>();

        // Then
        tracker.Should().NotBeNull("every dependency below it is registered");
        source.Should().BeOfType<SwappingTrackerSource>("a consumer resolving the seam gets the selector");
    }

    /// <summary>The application's own composition, over settings that carry no credential.</summary>
    /// <returns>The host, which the caller disposes.</returns>
    private static IHost Application()
    {
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpenSkyOptions.Section}:{nameof(OpenSkyOptions.BaseUrl)}"] = OpenSkyOptions.DefaultBaseUrl,
                [$"{OpenSkyOptions.Section}:Box:LatitudeMinimum"] = "28.8",
                [$"{OpenSkyOptions.Section}:Box:LongitudeMinimum"] = "-96.0",
                [$"{OpenSkyOptions.Section}:Box:LatitudeMaximum"] = "30.4",
                [$"{OpenSkyOptions.Section}:Box:LongitudeMaximum"] = "-94.2",
            })
            .Build();

        return new HostBuilder()
            .ConfigureServices(services => services.AddTransponder(settings, ImmediateScheduler.Instance))
            .Build();
    }
}
