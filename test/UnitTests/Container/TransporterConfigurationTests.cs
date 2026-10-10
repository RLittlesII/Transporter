using System.IO;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using Transporter.Container;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Configuration;

namespace Transporter.UnitTests.Container;

public class TransporterConfigurationTests
{
    /// <summary>
    /// B-058. The credentials are read where the application reads them — the options its own
    /// composition binds — so a store that is not layered in at all leaves them absent and fails
    /// here. The packaged settings are read back beside them because a store that replaced the
    /// configuration rather than layering over it would pass the first half. Which layer is on
    /// top is not read here, since the two documents share no key: that is the next test's.
    /// </summary>
    [Fact]
    public void GivenAUserSecretsStoreHoldingTheCredentials_WhenTheConfigurationIsComposed_ThenTheCredentialsAreTheStoresAndNoOtherSettingChanges()
    {
        // Given
        using var settings = new MemoryStream(Encoding.UTF8.GetBytes(TransporterSettingsPayloads.Packaged));
        using var secrets = new MemoryStream(Encoding.UTF8.GetBytes(TransporterSettingsPayloads.Store));

        // When
        var configuration = TransporterConfiguration.Compose(settings, secrets);
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddTransporter(configuration, new TestScheduler()))
            .Build();
        var credentials = host.Services.GetRequiredService<IOptions<OpenSkyCredentials>>().Value;
        var options = host.Services.GetRequiredService<IOptions<OpenSkyOptions>>().Value;

        // Then
        credentials.ClientId.Should().Be(TransporterSettingsPayloads.ClientId);
        credentials.ClientSecret.Should().Be(TransporterSettingsPayloads.ClientSecret);
        options.BaseUrl.Should().Be(TransporterSettingsPayloads.BaseUrl);
        options.PollInterval.Should().Be(TransporterSettingsPayloads.PollInterval);
        options.Box.Should().Be(TransporterSettingsPayloads.Box);
    }

    /// <summary>
    /// B-058, the word "over". The store names a setting the packaged document names too, which
    /// is the only arrangement that tells the two orders apart: a store layered under the
    /// settings binds the packaged value and fails here, and passes both other tests.
    /// </summary>
    [Fact]
    public void GivenAUserSecretsStoreNamingASettingThePackagedSettingsName_WhenTheConfigurationIsComposed_ThenTheSettingIsTheStores()
    {
        // Given
        using var settings = new MemoryStream(Encoding.UTF8.GetBytes(TransporterSettingsPayloads.Packaged));
        using var secrets = new MemoryStream(Encoding.UTF8.GetBytes(TransporterSettingsPayloads.StoreNamingAPackagedSetting));

        // When
        var configuration = TransporterConfiguration.Compose(settings, secrets);
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddTransporter(configuration, new TestScheduler()))
            .Build();
        var options = host.Services.GetRequiredService<IOptions<OpenSkyOptions>>().Value;

        // Then
        options.BaseUrl.Should().Be(TransporterSettingsPayloads.StoreBaseUrl);
        options.PollInterval.Should().Be(TransporterSettingsPayloads.PollInterval);
        options.Box.Should().Be(TransporterSettingsPayloads.Box);
    }

    /// <summary>
    /// B-058's second sentence. A clone with no store is the common case and must compose exactly
    /// what it did before the store existed: the packaged settings, and no credential invented in
    /// the absent one's place.
    /// </summary>
    [Fact]
    public void GivenNoUserSecretsStore_WhenTheConfigurationIsComposed_ThenItIsThePackagedSettingsAndNoCredentialIsConfigured()
    {
        // Given
        using var settings = new MemoryStream(Encoding.UTF8.GetBytes(TransporterSettingsPayloads.Packaged));

        // When
        var configuration = TransporterConfiguration.Compose(settings, secrets: null);
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddTransporter(configuration, new TestScheduler()))
            .Build();
        var options = host.Services.GetRequiredService<IOptions<OpenSkyOptions>>().Value;

        // Then
        configuration[$"{OpenSkyOptions.Section}:{nameof(OpenSkyCredentials.ClientId)}"].Should().BeNull();
        configuration[$"{OpenSkyOptions.Section}:{nameof(OpenSkyCredentials.ClientSecret)}"].Should().BeNull();
        options.BaseUrl.Should().Be(TransporterSettingsPayloads.BaseUrl);
        options.PollInterval.Should().Be(TransporterSettingsPayloads.PollInterval);
        options.Box.Should().Be(TransporterSettingsPayloads.Box);
    }
}
