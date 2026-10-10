using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Configuration;

namespace Transporter.UnitTests.Integrations.OpenSky;

public class OpenSkyOptionsTests
{
    /// <summary>
    /// B-050. Fifteen seconds is decisions/0001's call, and it sits above the registered tier's
    /// five-second resolution limit with room to spare, so a user lowering it cannot out-run the
    /// provider. The box has no default at all: a box nobody chose is a demo pointed at open ocean,
    /// and the credit arithmetic depends on which box it is.
    /// </summary>
    [Fact]
    public void GivenNoConfiguration_WhenOptionsAreRead_ThenTheIntervalIsFifteenSecondsAndTheBoxHasNoDefault()
    {
        // Given, When
        var options = new OpenSkyOptions();

        // Then
        options.PollInterval.Should().Be(TimeSpan.FromSeconds(15));
        options.Box.Should().BeNull();
        options.BaseUrl.Should().Be(OpenSkyOptions.DefaultBaseUrl, "the URL is the provider's, and nobody here chooses it");
    }

    /// <summary>
    /// B-050's validation, read directly. The failure names the setting rather than describing it,
    /// because the message is what a presenter reads when the host refuses to start.
    /// </summary>
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

    /// <summary>
    /// B-029's message, read directly: both credentials absent names both, so a presenter is not
    /// told about one, told to fix it, and then told about the other.
    /// </summary>
    [Fact]
    public void GivenBothCredentialsAbsent_WhenTheConfigurationIsValidated_ThenTheFailureNamesEachOfThem()
    {
        // Given
        var sut = new OpenSkyConfigurationValidator();

        // When
        var result = sut.Validate(Options.DefaultName, new OpenSkyCredentials());

        // Then
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(OpenSkyCredentials.ClientId));
        result.FailureMessage.Should().Contain(nameof(OpenSkyCredentials.ClientSecret));
    }
}
