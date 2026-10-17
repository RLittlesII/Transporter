using System.IO;
using System.Text;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Transporter.Container;
using Transporter.Integrations.AisStream;
using Transporter.Integrations.AisStream.Configuration;
using Transporter.Integrations.AisStream.Container;
using Transporter.Integrations.OpenSky.Container;
using Transporter.UnitTests.Container;

namespace Transporter.UnitTests.Integrations.AisStream;

public class AisStreamRegistrationTests
{
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
    }
}
