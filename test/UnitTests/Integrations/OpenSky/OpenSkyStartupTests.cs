using AwesomeAssertions;
using Flurl.Http.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Transponder.Integrations.OpenSky;
using Transponder.Integrations.OpenSky.Configuration;
using Transponder.Integrations.OpenSky.Container;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class OpenSkyStartupTests
{
    /// <summary>
    /// B-029, first half. A client id is configured and a client secret is not, which is the case a
    /// presenter hits on stage: without validation the poll fails later with a <c>401</c> nobody can
    /// read. Startup fails instead, the message names the credential that is absent, and it carries
    /// no value of the one that was configured — B-027's ban read against the failure path.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAnAbsentCredential_WhenTheApplicationStarts_ThenItFailsNamingWhichOne()
    {
        // Given
        using var http = new HttpTest();
        using var host = Host(Configured([("ClientId", "synthetic-client-id"), .. Box]));

        // When
        var start = async () => await host.StartAsync(CancellationToken.None);

        // Then
        var failure = await start.Should().ThrowAsync<OptionsValidationException>();
        var message = failure.Which.Message;
        message.Should().Contain($"{OpenSkyOptions.Section}:{nameof(OpenSkyCredentials.ClientSecret)}");
        message.Should().NotContain("synthetic-client-id", "a message naming a credential's value is the leak it was written to prevent");
        http.CallLog.Should().BeEmpty("no poll is attempted, because the host never started");
    }

    /// <summary>
    /// B-050's other half, at startup. An absent bounding box stops the host the same way an absent
    /// credential does, rather than the client choosing a box nobody asked for.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenNoBoundingBox_WhenTheApplicationStarts_ThenItFailsNamingTheBox()
    {
        // Given
        using var http = new HttpTest();
        using var host = Host(Configured(Credentials));

        // When
        var start = async () => await host.StartAsync(CancellationToken.None);

        // Then
        var failure = await start.Should().ThrowAsync<OptionsValidationException>();
        failure.Which.Message.Should().Contain($"{OpenSkyOptions.Section}:{nameof(OpenSkyOptions.Box)}");
    }

    /// <summary>
    /// The control. The same host, configured the way an application configures it, starts — so the
    /// two tests above fail for the setting they name rather than for the way this host is built.
    /// The box arrives through configuration binding, which is the path an application takes.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenEveryCredentialAndABox_WhenTheApplicationStarts_ThenItStartsAndTheOptionsCarryTheBox()
    {
        // Given
        using var http = new HttpTest();
        using var host = Host(Configured([.. Credentials, .. Box]));

        // When
        var start = async () => await host.StartAsync(CancellationToken.None);

        // Then
        await start.Should().NotThrowAsync();
        var options = host.Services.GetRequiredService<IOptions<OpenSkyOptions>>().Value;
        options.Box.Should().NotBeNull();
        options.Box!.LatitudeMinimum.Should().Be(28.8);
        options.PollInterval.Should().Be(OpenSkyOptions.DefaultPollInterval);
        await host.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// A host wired the way an application wires it: one call to
    /// <see cref="OpenSkyRegistration.AddOpenSky"/>, with configuration the only thing a test
    /// varies.
    /// </summary>
    /// <param name="configuration">What the host reads its settings from.</param>
    /// <returns>The host, unstarted.</returns>
    /// <remarks>
    /// The composition is <c>AddOpenSky</c>'s and nothing here adds to it. A test that assembled the
    /// same graph by hand would be a second composition to keep in step, and the first production
    /// scenario it missed would pass.
    /// </remarks>
    private static IHost Host(IConfiguration configuration) =>
        new HostBuilder()
            .ConfigureServices(services => services.AddOpenSky(configuration))
            .Build();

    /// <summary>Configuration carrying the settings named, under the section the options bind to.</summary>
    /// <param name="settings">Key and value pairs, relative to the <c>OpenSky</c> section.</param>
    /// <returns>The configuration.</returns>
    private static IConfiguration Configured(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                settings.ToDictionary<(string Key, string Value), string, string?>(
                    static setting => $"{OpenSkyOptions.Section}:{setting.Key}",
                    static setting => setting.Value))
            .Build();

    /// <summary>Both credentials, as user secrets or the environment would supply them.</summary>
    private static readonly (string Key, string Value)[] Credentials =
    [
        ("ClientId", "synthetic-client-id"),
        ("ClientSecret", "synthetic-client-secret"),
    ];

    /// <summary>The box decisions/0001 chose, in the shape configuration binds it from.</summary>
    private static readonly (string Key, string Value)[] Box =
    [
        ("Box:LatitudeMinimum", "28.8"),
        ("Box:LongitudeMinimum", "-96.0"),
        ("Box:LatitudeMaximum", "30.4"),
        ("Box:LongitudeMaximum", "-94.2"),
    ];
}
