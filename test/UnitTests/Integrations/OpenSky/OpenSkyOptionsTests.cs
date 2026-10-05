using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Transponder.Integrations.OpenSky;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class OpenSkyOptionsTests
{
    [Fact]
    public void GivenNoConfiguration_WhenOptionsAreRead_ThenTheIntervalIsFifteenSecondsAndTheBoxHasNoDefault()
    {
        // Given, When
        var options = new OpenSkyOptions();

        // Then. Fifteen seconds is decisions/0001's call, and it sits above the registered tier's
        // five-second resolution limit with room to spare.
        options.PollInterval.Should().Be(TimeSpan.FromSeconds(15));
        options.Box.Should().BeNull("B-050 gives the box no default, because a box nobody chose is open ocean");
    }

    [Fact]
    public void GivenNoBoundingBox_WhenTheConfigurationIsValidated_ThenItFailsNamingTheBoxRatherThanChoosingOne()
    {
        // Given
        var sut = new OpenSkyConfigurationValidator();

        // When
        var result = sut.Validate(Options.DefaultName, new OpenSkyOptions());

        // Then
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain($"{OpenSkyOptions.Section}:{nameof(OpenSkyOptions.Box)}");
    }

    [Fact]
    public void GivenConfigurationSupplyingBoth_WhenOptionsAreBound_ThenTheyOverrideTheDefaults()
    {
        // Given. The shape a host binds from, so this test fails if the member names move.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OpenSky:PollInterval"] = "00:00:20",
                    ["OpenSky:Box:LatitudeMinimum"] = "28.8",
                    ["OpenSky:Box:LongitudeMinimum"] = "-96.0",
                    ["OpenSky:Box:LatitudeMaximum"] = "30.4",
                    ["OpenSky:Box:LongitudeMaximum"] = "-94.2",
                })
            .Build();
        var services = new ServiceCollection();
        services.Configure<OpenSkyOptions>(configuration.GetSection(OpenSkyOptions.Section));

        // When
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<OpenSkyOptions>>().Value;

        // Then
        options.PollInterval.Should().Be(TimeSpan.FromSeconds(20), "configuration supplying a value overrides the default");
        options.Box.Should().NotBeNull();
        options.Box!.LatitudeMinimum.Should().Be(28.8);
        options.Box.LongitudeMinimum.Should().Be(-96.0);
        options.Box.LatitudeMaximum.Should().Be(30.4);
        options.Box.LongitudeMaximum.Should().Be(-94.2);
    }
}
