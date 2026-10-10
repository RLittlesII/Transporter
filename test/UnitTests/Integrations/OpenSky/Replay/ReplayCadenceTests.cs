using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Reactive.Testing;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Tracking;

namespace Transporter.UnitTests.Integrations.OpenSky.Replay;

public class ReplayCadenceTests
{
    /// <summary>
    /// B-007, through the client rather than at the pacer. The pacer spaces payloads by the
    /// recorded gap, and the client above it owns a loop that sleeps its configured interval after
    /// every fetch — so a replay chain left on the live interval would space a payload by the
    /// recorded gap plus fifteen seconds, which is the failure nobody notices on stage. Two
    /// assertions say it: the interval this client answers with is zero, and the payload after it
    /// is due at the recorded fifteen seconds rather than at thirty.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenTheLiveClientOverTheReplayContract_WhenPayloadsArrive_ThenNoIntervalOfItsOwnIsAdded()
    {
        // Given
        var scheduler = new TestScheduler();
        using var recording = ReplayComposition.Recorded(ReplayRecordingCases.TwoPollsFifteenSecondsApart);
        using var composed = ReplayComposition.Over(recording, scheduler);
        var clock = composed.GetRequiredService<IObservedClock>();
        var client = (AircraftSnapshotClient) composed.GetRequiredKeyedService<IAircraftSnapshotClient>(OpenSkyReplayRegistration.Chain);

        // When
        var afterTheFirstPayload = await client.Fetch(CancellationToken.None);
        var atTheFirstPayload = clock.Current;
        var second = client.Fetch(CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(14).Ticks);
        var atFourteenSeconds = second.IsCompleted;
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        await second;

        // Then
        afterTheFirstPayload.Should().Be(TimeSpan.Zero, "the client's own interval is zero, so the recorded gap is the only wait");
        atTheFirstPayload.Should().Be(OpenSkyPayloads.ReportedInstant, "the first payload is due as soon as the recording is read");
        atFourteenSeconds.Should().BeFalse("fourteen seconds is less than the recorded gap, and nothing shortens it");
        clock.Current.Should().Be(
            DateTimeOffset.FromUnixTimeSeconds(OpenSkyPayloads.SecondPoll.Response.Time),
            "the second payload is due at the recorded fifteen seconds — a client adding its own interval would need thirty");
    }
}
