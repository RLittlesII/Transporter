using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Airframe;
using Transponder.Integrations.OpenSky.Container;
using Transponder.Recording;
using Transponder.Tracking;
using Transponder.Tracking.Sources;

namespace Transponder.UnitTests.Integrations.OpenSky.Replay;

public class ReplayStartupReportTests
{
    /// <summary>
    /// B-026. Two defects a presenter would otherwise meet at the swap: a recording that is there
    /// and too short to show anything going quiet, and one configuration names that is not there at
    /// all. Both are reported when the host starts, each line naming the recording and what is
    /// wrong, and neither source is registered — so the control is absent rather than offering a
    /// recording that fails in front of people.
    /// <para>
    /// A report and not a refusal: the host starts. An unusable recording means the fallback is
    /// gone while the live demo is fine, and stopping the application for it would turn a degraded
    /// talk into no talk.
    /// </para>
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenOneRecordingSpanningNinetySecondsAndOneThatDoesNotExist_WhenTheHostStarts_ThenBothAreReportedAndNeitherSourceIsRegistered()
    {
        // Given
        var scheduler = new TestScheduler();
        var written = new RecordingLogger<ReplayStartupReport>();
        var recordings = new RecordedFiles().Holding(CutShort, ReplayRecordingCases.SpanningNinetySeconds);
        var settings = ReplayComposition.Settings(aircraft: CutShort, vessels: NeverCaptured);
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<ILogger<ReplayStartupReport>>(written);
                services.AddSingleton<ISchedulerProvider>(ReplayComposition.Schedulers(scheduler));
                services.AddAircraftReplay(settings, recordings);
            })
            .Build();

        // When
        await host.StartAsync(CancellationToken.None);

        // Then
        var reported = host.Services.GetRequiredService<ReplayStartupReport>().Recordings;
        reported.Single(static recording => recording.Source == nameof(ReplayOptions.Aircraft))
            .Should()
            .BeEquivalentTo(
                new { Defect = RecordingDefect.TooShort, Span = TimeSpan.FromSeconds(90) },
                static options => options.ExcludingMissingMembers(),
                "ninety seconds of recording cannot outlast a five-minute staleness threshold");
        reported.Single(static recording => recording.Source == nameof(ReplayOptions.Vessels))
            .Defect.Should()
            .Be(RecordingDefect.Absent, "the vessel recording was named and is not under the root");
        written.At(LogLevel.Warning).Should().HaveCount(2, "one line per defect, at startup rather than at the swap");
        written.At(LogLevel.Warning).Should().AllSatisfy(static line => line.Should().Contain(".ndjson", "a line a presenter reads says which recording"));
        host.Services.GetService<IAircraftReplayTrackerSource>().Should().BeNull("a defective recording leaves its source unregistered");
        host.Services.GetServices<ITrackerSourceStrategy>().Should().BeEmpty("so there is nothing for the selector to swap to");

        await host.StopAsync(CancellationToken.None);
    }

    /// <summary>The test capture taken after the rehearsal: real, and ninety seconds long.</summary>
    private const string CutShort = "aircraft-2026-10-04T14-32-11Z.ndjson";

    /// <summary>A recording named in configuration that nobody ever captured.</summary>
    private const string NeverCaptured = "vessels-2026-10-04T14-40-00Z.ndjson";
}
