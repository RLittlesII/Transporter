using System;
using System.IO;
using System.Text;
using Akka.Hosting;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using Transporter.Container;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Configuration;
using Transporter.Integrations.OpenSky.Model;

namespace Transporter.UnitTests.Container;

public class TransporterConfigurationTests
{
    /// <summary>
    /// B-058. The credentials are read where the application reads them — the options its own
    /// composition binds — so a store layered under the settings, or not layered at all, leaves
    /// them absent and fails here. The packaged settings are read back beside them because a
    /// store that replaced the configuration rather than layering over it would pass the first
    /// half.
    /// </summary>
    [Fact]
    public void GivenAUserSecretsStoreHoldingTheCredentials_WhenTheConfigurationIsComposed_ThenTheCredentialsAreTheStoresAndNoOtherSettingChanges()
    {
        // Given
        using var settings = new MemoryStream(Encoding.UTF8.GetBytes(TransporterSettingsPayloads.Packaged));
        using var secrets = new MemoryStream(Encoding.UTF8.GetBytes(TransporterSettingsPayloads.Store));

        // When
        var configuration = TransporterConfiguration.Compose(settings, secrets);

        // Then
        using var host = new HostBuilder()
            .ConfigureServices(services => services
                .AddTransporter(configuration, new TestScheduler())
                .AddAkka("transporter", static _ => { }))
            .Build();
        var credentials = host.Services.GetRequiredService<IOptions<OpenSkyCredentials>>().Value;
        var options = host.Services.GetRequiredService<IOptions<OpenSkyOptions>>().Value;

        credentials.ClientId.Should().Be(TransporterSettingsPayloads.ClientId);
        credentials.ClientSecret.Should().Be(TransporterSettingsPayloads.ClientSecret);
        options.BaseUrl.Should().Be(TransporterSettingsPayloads.BaseUrl);
        options.PollInterval.Should().Be(TimeSpan.FromSeconds(20));
        options.Box.Should().Be(new BoundingBox
        {
            LatitudeMinimum = 10.5,
            LongitudeMinimum = -20.5,
            LatitudeMaximum = 11.5,
            LongitudeMaximum = -19.5,
        });
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

        // Then
        using var host = new HostBuilder()
            .ConfigureServices(services => services
                .AddTransporter(configuration, new TestScheduler())
                .AddAkka("transporter", static _ => { }))
            .Build();
        var options = host.Services.GetRequiredService<IOptions<OpenSkyOptions>>().Value;

        configuration[$"{OpenSkyOptions.Section}:{nameof(OpenSkyCredentials.ClientId)}"].Should().BeNull();
        configuration[$"{OpenSkyOptions.Section}:{nameof(OpenSkyCredentials.ClientSecret)}"].Should().BeNull();
        options.BaseUrl.Should().Be(TransporterSettingsPayloads.BaseUrl);
        options.PollInterval.Should().Be(TimeSpan.FromSeconds(20));
        options.Box.Should().Be(new BoundingBox
        {
            LatitudeMinimum = 10.5,
            LongitudeMinimum = -20.5,
            LatitudeMaximum = 11.5,
            LongitudeMaximum = -19.5,
        });
    }
}
