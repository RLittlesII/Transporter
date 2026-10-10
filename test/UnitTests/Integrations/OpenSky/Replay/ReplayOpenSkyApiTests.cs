using AwesomeAssertions;
using DynamicData;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Integrations.OpenSky.Contracts;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.Tracking.Sources;

namespace Transporter.UnitTests.Integrations.OpenSky.Replay;

public class ReplayOpenSkyApiTests
{
    /// <summary>
    /// B-009. A recorded line carries two instants with one job each, and this is the one that goes
    /// downstream: the payload's own reported time. The recording was taken years before the instant
    /// it reports, so a chain reaching for the nearer value — the arrival instant the pacer waits
    /// on — fails here rather than ageing a replayed fleet against the wrong clock.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenALineWhosePayloadReportsOneInstantAndArrivedAtAnother_WhenItIsReplayed_ThenTheReportedOneIsTheObservedInstant()
    {
        // Given
        var scheduler = new TestScheduler();
        using var recording = ReplayComposition.Recorded(ReplayRecordingCases.ArrivedLongAfterItReports);
        using var composed = ReplayComposition.Over(recording, scheduler);
        var clock = composed.GetRequiredService<IObservedClock>();

        // When
        await Replayed(composed).Fetch(CancellationToken.None);

        // Then
        clock.Current.Should().Be(OpenSkyPayloads.ReportedInstant, "the envelope's own time is the observed instant");
        clock.Current.Should().NotBe(Arrival, "the arrival instant paces playback and never leaves the pacer");
    }

    /// <summary>
    /// B-013. The payload is read by the live reader and projected by the live mapper, so a
    /// replayed aircraft is the aircraft the same payload produced live, member for member. A
    /// converter of replay's own is what this forbids, and it would show here as two aircraft that
    /// differ in whichever member the second reader got wrong.
    /// <para>
    /// Both sides are fetched rather than polled, so the comparison is of two payloads that have
    /// landed rather than of whatever a poll loop had reached.
    /// </para>
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAPayloadThatProducedAnAircraftLive_WhenTheSamePayloadIsReplayed_ThenTheAircraftIsIdenticalAndTheLiveProjectionBuiltIt()
    {
        // Given
        var scheduler = new TestScheduler();
        var live = new SourceCache<AircraftSnapshot, string>(static snapshot => snapshot.Icao24);
        var api = Substitute.For<IOpenSkyApi>();
        api.GetStates(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(OpenSkyPayloads.ThreeRows.Response);
        AircraftSnapshotClient liveClient = new AircraftSnapshotClientFixture().WithApi(api).WithCache(live);
        using var recording = ReplayComposition.Recorded(ReplayRecordingCases.TwoPollsFifteenSecondsApart);
        using var composed = ReplayComposition.Over(recording, scheduler);
        var mapper = composed.GetRequiredService<AircraftSnapshotMapper>();

        // When
        await liveClient.Fetch(CancellationToken.None);
        await Replayed(composed).Fetch(CancellationToken.None);

        // Then
        var replayed = Cache(composed).Items;
        replayed.Should().HaveCount(3, "the recording's first payload is the live payload");
        replayed.Should().BeEquivalentTo(live.Items, "the live reader read both, so the snapshots are the same snapshots");
        replayed.Select(TransportVehicle (snapshot) => mapper.Project(snapshot))
            .Should()
            .BeEquivalentTo(
                live.Items.Select(TransportVehicle (snapshot) => mapper.Project(snapshot)),
                "one projection, whatever fed it");
    }

    /// <summary>
    /// B-014. The recording holds the provider's own text — a callsign still padded to eight
    /// characters and a squawk still quoted — so the reader that runs over it is today's rather
    /// than the one in force when the recording was taken. That is what a converter fix needing no
    /// re-recording means: the padding is trimmed and the leading zero survives because the current
    /// reader says so, from bytes nobody re-captured.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenARecordingTakenBeforeAConverterFix_WhenItIsReplayedAfterTheFix_ThenTheCorrectedValuesAreObservedWithNoRecapture()
    {
        // Given
        var scheduler = new TestScheduler();
        using var recording = ReplayComposition.Recorded(ReplayRecordingCases.ArrivedLongAfterItReports);
        using var composed = ReplayComposition.Over(recording, scheduler);

        // When
        await Replayed(composed).Fetch(CancellationToken.None);

        // Then
        ReplayRecordingCases.ArrivedLongAfterItReports.Should()
            .Contain("\"TRN0001 \"", "the recorded line is what the provider sent, padding included")
            .And.Contain("\"0021\"", "and a squawk the provider sent as text is still text in the recording");
        var snapshot = Cache(composed).Lookup("a1b2c3").Value;
        snapshot.Callsign.IfNone(string.Empty).Should().Be("TRN0001", "the reader trims, and it ran at replay rather than at capture");
        snapshot.Squawk.IfNone(string.Empty).Should().Be("0021", "a squawk read as a number would have lost the leading zero");
    }

    /// <summary>The replay chain's own client, which is the live class over the replay contract.</summary>
    /// <param name="composed">The composed chain.</param>
    /// <returns>The client, as the concrete class, so a test can drive one fetch.</returns>
    private static AircraftSnapshotClient Replayed(IServiceProvider composed) =>
        (AircraftSnapshotClient) composed.GetRequiredKeyedService<IAircraftSnapshotClient>(OpenSkyReplayRegistration.Chain);

    /// <summary>The replay chain's own snapshot cache.</summary>
    /// <param name="composed">The composed chain.</param>
    /// <returns>The cache the replay client writes into.</returns>
    private static SourceCache<AircraftSnapshot, string> Cache(IServiceProvider composed) =>
        composed.GetRequiredKeyedService<SourceCache<AircraftSnapshot, string>>(OpenSkyReplayRegistration.Chain);

    /// <summary>The instant <see cref="ReplayRecordingCases.ArrivedLongAfterItReports"/> arrived at.</summary>
    private static readonly DateTimeOffset Arrival = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
