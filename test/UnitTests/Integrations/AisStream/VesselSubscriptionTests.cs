using System.Text.Json;
using AwesomeAssertions;
using Transporter.Integrations.AisStream.Messages;
using Transporter.Integrations.OpenSky.Model;
using Transporter.UnitTests.Integrations.AisStream.Fixtures;

namespace Transporter.UnitTests.Integrations.AisStream;

public class VesselSubscriptionTests
{
    /// <summary>B-001: the shared aircraft box remains a single latitude-first subscription box.</summary>
    [Fact]
    public void GivenTheAircraftBox_WhenTheSubscriptionIsWritten_ThenItNamesThatOneBoxLatitudeFirst()
    {
        // Given
        BoundingBox box = new BoundingBoxFixture();

        // When
        using var subscription = JsonDocument.Parse(VesselSubscription.Write("synthetic-credential", box));

        // Then
        subscription.RootElement.TryGetProperty("BoundingBoxes", out var boxes).Should().BeTrue();
        boxes.GetArrayLength().Should().Be(1);
        var corners = boxes[0];
        corners.GetArrayLength().Should().Be(2);
        corners[0].EnumerateArray().Select(static value => value.GetDouble()).Should()
            .Equal(box.LatitudeMinimum, box.LongitudeMinimum);
        corners[1].EnumerateArray().Select(static value => value.GetDouble()).Should()
            .Equal(box.LatitudeMaximum, box.LongitudeMaximum);
    }

    /// <summary>B-011 subscription half: no unrequested message type spends the stream's bandwidth.</summary>
    [Fact]
    public void GivenTheSubscription_WhenItIsWritten_ThenItFiltersToPositionReportsAndStaticData()
    {
        // Given
        BoundingBox box = new BoundingBoxFixture();

        // When
        using var subscription = JsonDocument.Parse(VesselSubscription.Write("synthetic-credential", box));

        // Then
        subscription.RootElement.TryGetProperty("FilterMessageTypes", out var filters).Should().BeTrue();
        filters.EnumerateArray().Select(static filter => filter.GetString()).Should()
            .BeEquivalentTo(["PositionReport", "ShipStaticData"]);
    }
}
