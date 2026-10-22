using AwesomeAssertions;
using LanguageExt;
using Transporter.Integrations.AisStream;
using Transporter.Integrations.AisStream.Messages;

namespace Transporter.UnitTests.Integrations.AisStream;

public class VesselReplayMergeTests
{
    /// <summary>B-008/B-009: only the first timed report of a new pass rebases contact.</summary>
    [Fact]
    public void GivenAHeldContact_WhenANewPassStarts_ThenUntimedFramesKeepItAndTheFirstTimedFrameRebasesIt()
    {
        var later = new DateTimeOffset(2026, 10, 20, 14, 43, 0, TimeSpan.Zero);
        var earlier = later.AddMinutes(-11);
        var snapshot = new VesselSnapshot { Mmsi = "366123456", Reported = later };
        var untimed = new VesselStaticMessage(snapshot.Mmsi, "TEST", 70);
        var timed = new VesselStaticMessage(snapshot.Mmsi, "TEST", 70) { Reported = earlier };

        var afterUntimed = snapshot.Apply(untimed, resetReported: true);
        var afterFirstTimed = afterUntimed.Apply(timed, resetReported: true);
        var afterLate = afterFirstTimed.Apply(timed with { Reported = earlier.AddSeconds(-1) }, resetReported: false);

        afterUntimed.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(later);
        afterFirstTimed.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(earlier);
        afterLate.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(earlier);
    }
}
