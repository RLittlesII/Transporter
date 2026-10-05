using AwesomeAssertions;
using Flurl.Http.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Transponder.Integrations.OpenSky;
using Transponder.Integrations.OpenSky.Container;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class OpenSkyStartupTests
{
    [Fact]
    public async Task GivenAnAbsentCredential_WhenTheApplicationStarts_ThenItFailsNamingWhichOne()
    {
        // Given. A client id is configured and a client secret is not, which is the case a
        // presenter hits on stage: the poll would otherwise fail with a 401 nobody can read.
        using var http = new HttpTest();
        using var host = Host(
            services =>
            {
                services.Configure<OpenSkyCredentials>(credentials => credentials.ClientId = "synthetic-client-id");
                services.Configure<OpenSkyOptions>(options => options.Box = Houston);
            });

        // When
        var start = async () => await host.StartAsync(CancellationToken.None);

        // Then. Startup fails, the message names the absent credential, and it carries no value of
        // the one that was configured (B-027's ban, read against the failure path).
        var failure = await start.Should().ThrowAsync<OptionsValidationException>();
        var message = failure.Which.Message;
        message.Should().Contain($"{OpenSkyOptions.Section}:{nameof(OpenSkyCredentials.ClientSecret)}");
        message.Should().NotContain(nameof(OpenSkyCredentials.ClientId) + "\"");
        message.Should().NotContain("synthetic-client-id", "a message naming a credential's value is the leak it was written to prevent");
        http.CallLog.Should().BeEmpty("no poll is attempted, because the host never started");
    }

    [Fact]
    public async Task GivenEveryCredentialAndABox_WhenTheApplicationStarts_ThenItStarts()
    {
        // Given. The control: the same host, validly configured, starts — so the test above fails
        // for the absent credential rather than for the way the host is built here.
        using var http = new HttpTest();
        using var host = Host(
            services =>
            {
                services.Configure<OpenSkyCredentials>(credentials =>
                {
                    credentials.ClientId = "synthetic-client-id";
                    credentials.ClientSecret = "synthetic-client-secret";
                });
                services.Configure<OpenSkyOptions>(options => options.Box = Houston);
            });

        // When
        var start = async () => await host.StartAsync(CancellationToken.None);

        // Then
        await start.Should().NotThrowAsync();
        await host.StopAsync(CancellationToken.None);
    }

    private static IHost Host(Action<IServiceCollection> configure) =>
        new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddOpenSky("https://opensky.invalid/api");
                configure(services);
            })
            .Build();

    /// <summary>The box decisions/0001 chose, so only the credential under test is absent.</summary>
    private static readonly BoundingBox Houston = new()
    {
        LatitudeMinimum = 28.8,
        LongitudeMinimum = -96.0,
        LatitudeMaximum = 30.4,
        LongitudeMaximum = -94.2,
    };
}
