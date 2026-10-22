using AwesomeAssertions;
using Transporter.Integrations.AisStream.Messages;

namespace Transporter.UnitTests.Integrations.AisStream;

public class VesselMessageReaderTests
{
    /// <summary>B-008: numeric MMSIs become stable nine-digit keys, including a lost leading zero.</summary>
    /// <param name="frame">A position frame with a numeric MMSI.</param>
    /// <param name="expected">The nine-digit key.</param>
    [Theory]
    [ClassData(typeof(VesselMmsiCases))]
    public void GivenAnMmsi_WhenItIsRead_ThenTheKeyIsNineDigits(string frame, string expected)
    {
        // Given

        // When
        VesselMessage message = VesselMessageReader.Read(frame).IfNone(static () => throw new InvalidOperationException());

        // Then
        message.Mmsi.Should().Be(expected);
    }

    /// <summary>B-011: a message type outside the subscription never becomes a cache write.</summary>
    [Fact]
    public void GivenAnUnrequestedMessageType_WhenItIsRead_ThenItIsIgnored()
    {
        // Given
        var frame = VesselPayloads.BaseStationReport;

        // When
        var message = VesselMessageReader.Read(frame);

        // Then
        message.IsNone.Should().BeTrue();
    }

    /// <summary>B-013: each AIS unavailable value and each half fix is absent at the read boundary.</summary>
    /// <param name="frame">A report carrying one unavailable value.</param>
    /// <param name="field">The field that must be absent.</param>
    [Theory]
    [ClassData(typeof(VesselUnavailableValueCases))]
    public void GivenASentinelOrAMissingCoordinate_WhenItIsRead_ThenTheValueIsAbsent(string frame, string field)
    {
        // Given

        // When
        VesselMessage message = VesselMessageReader.Read(frame).IfNone(static () => throw new InvalidOperationException());

        // Then
        var position = message.Should().BeOfType<VesselPositionMessage>().Which;
        switch (field)
        {
            case "position":
                position.Position.IsNone.Should().BeTrue();
                break;
            case "speed":
                position.SpeedOverGround.IsNone.Should().BeTrue();
                break;
            case "course":
                position.CourseOverGround.IsNone.Should().BeTrue();
                break;
            case "heading":
                position.TrueHeading.IsNone.Should().BeTrue();
                break;
            default:
                throw new InvalidOperationException($"Unexpected test field: {field}.");
        }
    }

    /// <summary>B-017: a static name is normalized at the AIS read boundary, including values that become absent.</summary>
    /// <param name="frame">A static report with a padded, blank or absent name.</param>
    /// <param name="expected">The normalized name, or null when it is absent.</param>
    [Theory]
    [ClassData(typeof(VesselStaticNameCases))]
    public void GivenAStaticName_WhenItIsRead_ThenItsPaddingIsRemovedOrItIsAbsent(string frame, string? expected)
    {
        // Given

        // When
        VesselMessage message = VesselMessageReader.Read(frame).IfNone(static () => throw new InvalidOperationException());

        // Then
        var staticMessage = message.Should().BeOfType<VesselStaticMessage>().Which;
        staticMessage.Name.IfNoneUnsafe((string?) null).Should().Be(expected);
    }

    /// <summary>B-018: ordinary reported speed, course and heading keep the feed's knots and degrees.</summary>
    [Fact]
    public void GivenReportedSpeedCourseAndHeading_WhenRead_ThenTheyKeepTheFeedsValues()
    {
        // Given
        var frame = VesselPayloads.PositionReport
            .Replace("\"Cog\": 86.7", "\"Cog\": 87.5", StringComparison.Ordinal)
            .Replace("\"TrueHeading\": 87", "\"TrueHeading\": 90", StringComparison.Ordinal);

        // When
        VesselMessage message = VesselMessageReader.Read(frame).IfNone(static () => throw new InvalidOperationException());

        // Then
        var position = message.Should().BeOfType<VesselPositionMessage>().Which;
        position.SpeedOverGround.IfNone(-1).Should().Be(12.4);
        position.CourseOverGround.IfNone(-1).Should().Be(87.5);
        position.TrueHeading.IfNone(-1).Should().Be(90);
    }
}
