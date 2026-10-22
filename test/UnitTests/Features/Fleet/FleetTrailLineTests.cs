using AwesomeAssertions;
using LanguageExt;
using Transporter.Features.Fleet;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using Transporter.UnitTests.Tracking.Fixtures;

namespace Transporter.UnitTests.Features.Fleet;

public class FleetTrailLineTests
{
    /// <summary>B-034. A gap starts a new run; joining it would draw travel during silence.</summary>
    [Fact]
    public void GivenATrailWithAGap_WhenItsSegmentsAreRead_ThenNoSegmentJoinsTheGap()
    {
        // Given
        TrailPoint first = new TrailPointFixture().WithPosition(new GeoPosition(40, -90));
        TrailPoint second = new TrailPointFixture().WithPosition(new GeoPosition(41, -90)).WithFollowsGap(true);
        TrailPoint third = new TrailPointFixture().WithPosition(new GeoPosition(42, -90));
        FleetTrailMeasure measure = new FleetTrailMeasureFixture();

        // When
        var segments = FleetTrailLine.Segments([first, second, third], measure);

        // Then
        segments.Should().ContainSingle().Which.From.Should().BeSameAs(second);
        segments[0].To.Should().BeSameAs(third);
    }

    /// <summary>B-034. The 240-point bound removes the old tail while a new head arrives; shared edges stay mounted.</summary>
    [Fact]
    public void GivenAFullTrailThatTookAPoint_WhenFollowed_ThenOneSegmentLeavesTheTailAndOneJoinsTheHead()
    {
        // Given
        var original = Enumerable.Range(0, 240)
            .Select(static index => (TrailPoint) new TrailPointFixture().WithPosition(new GeoPosition(40 + (index * 0.001), -90)))
            .ToArray();
        TrailPoint newest = new TrailPointFixture().WithPosition(new GeoPosition(40.24, -90));
        TrailPoint[] current = [.. original.Skip(1), newest];
        FleetTrailMeasure measure = new FleetTrailMeasureFixture();

        // When
        var change = FleetTrailLine.Follow(original, current, measure);

        // Then
        change.Redraw.Should().BeFalse();
        change.Removed.Should().Be(1);
        change.Added.Should().ContainSingle().Which.To.Should().BeSameAs(newest);
        FleetTrailLine.Segments(current, measure).Should().HaveCount(239);
    }

    /// <summary>B-034. A different selection shares no point objects and needs its own line.</summary>
    [Fact]
    public void GivenAnotherVehiclesTrail_WhenFollowed_ThenItAnswersRedraw()
    {
        // Given
        TrailPoint[] first = [new TrailPointFixture(), new TrailPointFixture().WithPosition(new GeoPosition(41, -90))];
        TrailPoint[] second = [new TrailPointFixture(), new TrailPointFixture().WithPosition(new GeoPosition(42, -90))];
        FleetTrailMeasure measure = new FleetTrailMeasureFixture();

        // When
        var change = FleetTrailLine.Follow(first, second, measure);

        // Then
        change.Redraw.Should().BeTrue();
        change.Added.Should().ContainSingle();
    }

    /// <summary>B-034. Out-of-range measures use the endpoint shade instead of overflowing the ramp.</summary>
    [Fact]
    public void GivenAMeasureOutsideTheRange_WhenShaded_ThenTheShadeIsClamped()
    {
        // Given
        FleetTrailMeasure measure = new FleetTrailMeasureFixture();

        // When
        var below = FleetTrailLine.Shade(Option<double>.Some(-20), measure);
        var above = FleetTrailLine.Shade(Option<double>.Some(120), measure);

        // Then
        (below == Option<double>.Some(0)).Should().BeTrue();
        (above == Option<double>.Some(1)).Should().BeTrue();
    }
}
