using System.Reactive.Linq;
using System.Text;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using Transporter.Container;
using Transporter.Integrations.AisStream;
using Transporter.Integrations.AisStream.Configuration;
using Transporter.Integrations.AisStream.Container;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Tracking;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Container;

namespace Transporter.UnitTests.Integrations.AisStream;

public class AisStreamRegistrationTests
{
    /// <summary>B-024. The application offers vessels as another strategy while keeping the decorator as the only tracker seam.</summary>
    [Fact]
    public void GivenTheApplicationsGraph_WhenTheEntriesAreRead_ThenVesselsIsAStrategyBehindTheDecoratorAndNotTheSeam()
    {
        // Given
        using var packaged = new MemoryStream(Encoding.UTF8.GetBytes(TransporterSettingsPayloads.Packaged));
        using var secrets = new MemoryStream(Encoding.UTF8.GetBytes(VesselPayloads.UserSecrets));
        var configuration = TransporterConfiguration.Compose(packaged, secrets);
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddTransporter(configuration, new TestScheduler()))
            .Build();

        // When
        var entries = host.Services.GetServices<TrackerSourceEntry>().ToArray();
        var vessel = entries.Single(static entry => entry.Name == "Vessels");

        // Then
        vessel.Source.Should().BeSameAs(host.Services.GetRequiredService<IVesselTrackerSource>());
        host.Services.GetRequiredService<ITrackerSource>().Should().BeSameAs(host.Services.GetRequiredService<SwappingTrackerSource>());
        host.Services.GetServices<ITrackerSource>().Should().ContainSingle();
        entries[0].Name.Should().Be("Live aircraft", "the first registered source remains the startup source");
    }

    /// <summary>B-025. Vessels have no next poll and cannot accept a demanded refresh.</summary>
    [Fact]
    public void GivenTheVesselsEntry_WhenItIsLive_ThenNoPollStatusIsPublished()
    {
        // Given
        using var packaged = new MemoryStream(Encoding.UTF8.GetBytes(TransporterSettingsPayloads.Packaged));
        using var secrets = new MemoryStream(Encoding.UTF8.GetBytes(VesselPayloads.UserSecrets));
        var configuration = TransporterConfiguration.Compose(packaged, secrets);
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddTransporter(configuration, new TestScheduler()))
            .Build();
        var selector = host.Services.GetRequiredService<SwappingTrackerSource>();
        var observed = new List<PollStatus>();
        var capability = new List<bool>();
        using var status = selector.Status.Subscribe(observed.Add);
        using var canDemandPoll = selector.CanDemandPoll.Subscribe(capability.Add);

        // When
        selector.Select(selector.Entries.Single(static entry => entry.Name == "Vessels"));

        // Then
        observed[^1].Should().Be(PollStatus.None);
        capability.Should().Equal(true, false);
    }

    /// <summary>B-005, B-006: the production graph retains the client and cache while issuing fresh connection sockets.</summary>
    /// <returns>The disposal of the unopened socket instances.</returns>
    [Fact]
    public async Task GivenTheProductionRegistrations_WhenTheClientAndSocketsAreResolved_ThenTheClientAndCacheAreRetainedAndSocketsAreFresh()
    {
        // Given
        using var packaged = new MemoryStream(Encoding.UTF8.GetBytes(TransporterSettingsPayloads.Packaged));
        using var secrets = new MemoryStream(Encoding.UTF8.GetBytes(VesselPayloads.UserSecrets));
        var configuration = TransporterConfiguration.Compose(packaged, secrets);
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddOpenSky(configuration).AddAisStream(configuration))
            .Build();

        // When
        var client = host.Services.GetRequiredService<VesselClient>();
        var cache = host.Services.GetRequiredService<SourceCache<VesselSnapshot, string>>();
        var sockets = host.Services.GetRequiredService<Func<IVesselSocket>>();
        await using var first = sockets.Invoke();
        await using var second = sockets.Invoke();

        // Then
        host.Services.GetRequiredService<VesselClient>().Should().BeSameAs(client);
        host.Services.GetRequiredService<SourceCache<VesselSnapshot, string>>().Should().BeSameAs(cache);
        second.Should().NotBeSameAs(first);
    }

    /// <summary>B-003: the production configuration layering binds the user-secrets key.</summary>
    [Fact]
    public void GivenAKeyInUserSecrets_WhenTheOptionsAreComposed_ThenTheKeyIsTheStores()
    {
        // Given
        using var packaged = new MemoryStream(Encoding.UTF8.GetBytes(VesselPayloads.PackagedSettings));
        using var secrets = new MemoryStream(Encoding.UTF8.GetBytes(VesselPayloads.UserSecrets));
        var configuration = TransporterConfiguration.Compose(packaged, secrets);
        var services = new ServiceCollection();
        services.AddOptions();

        // When
        services.AddAisStream(configuration);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AisStreamOptions>>().Value;

        // Then
        options.ApiKey.IsSome.Should().BeTrue();
        options.ApiKey.IfSome(static value => value.Should().Be("synthetic-credential"));
        options.ReconnectAfter.Should().Be(TimeSpan.FromSeconds(9));
        options.SilentAfter.Should().Be(TimeSpan.FromMinutes(10));
    }

    /// <summary>B-023: the vessel threshold comes from the AIS configuration.</summary>
    [Fact]
    public void GivenAConfiguredVesselSilenceThreshold_WhenOptionsAreBound_ThenItReplacesTheDefault()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AisStream:SilentAfter"] = "00:20:00",
        }).Build();
        var services = new ServiceCollection();
        services.AddAisStream(configuration);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<AisStreamOptions>>().Value.SilentAfter
            .Should().Be(TimeSpan.FromMinutes(20));
    }

    /// <summary>B-023, B-024: the selected vessel description uses the configured threshold, not the default.</summary>
    [Fact]
    public void GivenAConfiguredVesselSilenceThreshold_WhenTheVesselEntryIsResolved_ThenItsDescriptionUsesThatThreshold()
    {
        using var packaged = new MemoryStream(Encoding.UTF8.GetBytes(TransporterSettingsPayloads.Packaged));
        using var secrets = new MemoryStream(Encoding.UTF8.GetBytes(VesselPayloads.UserSecrets));
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(TransporterConfiguration.Compose(packaged, secrets))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AisStream:SilentAfter"] = "00:20:00",
            })
            .Build();
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddTransporter(configuration, new TestScheduler()))
            .Build();

        var entry = host.Services.GetServices<TrackerSourceEntry>().Single(static candidate => candidate.Name == "Vessels");

        entry.Description.SilentAfter.IfNone(TimeSpan.Zero).Should().Be(TimeSpan.FromMinutes(20));
    }
}
