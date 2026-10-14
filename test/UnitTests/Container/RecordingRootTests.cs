using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using Transporter.Container;
using Transporter.Recording;

namespace Transporter.UnitTests.Container;

public class RecordingRootTests
{
    /// <summary>Replay B-025: packaged settings and user secrets override the platform default in order.</summary>
    /// <param name="packagedRoot">The packaged override, if any.</param>
    /// <param name="storeRoot">The user-secrets override, if any.</param>
    /// <param name="expected">The effective shared root.</param>
    [Theory]
    [ClassData(typeof(RecordingRootCases))]
    public void GivenAPlatformRootAndOptionalOverrides_WhenConfigurationIsComposed_ThenTheHighestPriorityRootIsShared(
        string? packagedRoot,
        string? storeRoot,
        string expected)
    {
        // Given
        using var settings = new MemoryStream(Encoding.UTF8.GetBytes(
            packagedRoot is null ? "{}" : $$$"""{"Recording":{"Root":"{{{packagedRoot}}}"}}"""));
        using var secrets = storeRoot is null ? null : new MemoryStream(Encoding.UTF8.GetBytes(
            $$$"""{"Recording":{"Root":"{{{storeRoot}}}"}}"""));

        // When
        var configuration = TransporterConfiguration.Compose(settings, secrets, "platform/recordings");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransporter(configuration, new TestScheduler());
        using var provider = services.BuildServiceProvider();

        // Then
        configuration["Recording:Root"].Should().Be(expected);
        provider.GetRequiredService<IOptions<RecordingOptions>>().Value.Root.Should().Be(expected);
    }

    /// <summary>Replay B-025: a host without a platform default retains the relative recordings root.</summary>
    [Fact]
    public void GivenNoPlatformRoot_WhenConfigurationIsComposed_ThenTheRelativeDefaultIsRetained()
    {
        // Given
        using var settings = new MemoryStream("{}"u8.ToArray());

        // When
        var configuration = TransporterConfiguration.Compose(settings, secrets: null);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransporter(configuration, new TestScheduler());
        using var provider = services.BuildServiceProvider();

        // Then
        provider.GetRequiredService<IOptions<RecordingOptions>>().Value.Root.Should().Be(RecordingOptions.DefaultRoot);
    }
}
