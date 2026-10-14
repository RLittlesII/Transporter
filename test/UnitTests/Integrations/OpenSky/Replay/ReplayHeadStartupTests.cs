using System.Text;
using AwesomeAssertions;
using Flurl.Http.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Recording;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Integrations.OpenSky.Replay;

public class ReplayHeadStartupTests
{
    /// <summary>B-026: a plain provider reports each recording without starting hosted services or a source.</summary>
    /// <param name="lines">The synthetic recording, or an unavailable stream.</param>
    /// <param name="exists">Whether the recording is present.</param>
    /// <param name="usable">Whether its replay source may register.</param>
    /// <param name="diagnosis">The expected startup message.</param>
    [Theory]
    [ClassData(typeof(RecordingStartupCases))]
    public void GivenANamedRecording_WhenTheHeadReportsStartup_ThenItsDistinctDiagnosisIsLoggedWithoutStartingServices(
        string? lines,
        bool exists,
        bool usable,
        string diagnosis)
    {
        // Given
        const string name = "aircraft-synthetic.ndjson";
        var path = Path.Combine("platform", "recordings", name);
        using var payloads = lines is null ? null : new MemoryStream(Encoding.UTF8.GetBytes(lines));
        var recordings = Substitute.For<IRecordingLibrary>();
        recordings.Resolve(name).Returns(path);
        recordings.Holds(name).Returns(exists);
        recordings.Open(name).Returns(payloads);
        var written = new RecordingLogger<ReplayStartupReport>();
        var unrelated = Substitute.For<IHostedService>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Replay:Aircraft"] = name,
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogger<ReplayStartupReport>>(written);
        services.AddSingleton(unrelated);
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(new TestScheduler());
        services.AddSingleton<ISchedulerProvider>(schedulers);
        services.AddAircraftReplay(configuration, recordings);
        using var provider = services.BuildServiceProvider();
        using var http = new HttpTest();

        // When
        provider.ReportReplayStartup();

        // Then
        written.Messages.Should().ContainSingle(static line => line.Contains(name, StringComparison.Ordinal))
            .Which.Should().Contain("Aircraft").And.Contain(path).And.Contain(diagnosis);
        provider.GetServices<ITrackerSourceStrategy>().Should().HaveCount(usable ? 1 : 0);
        _ = unrelated.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
        http.ShouldNotHaveMadeACall();
        if (exists)
        {
            recordings.Received(1).Open(name);
        }

        if (payloads is not null)
        {
            payloads.CanRead.Should().Be(usable);
            if (usable)
            {
                payloads.Position.Should().Be(0, "reporting must not consume a replay payload");
            }
        }
    }

    /// <summary>B-026: an inspection or rewind failure excludes the source, releases its stream, and still reports.</summary>
    /// <param name="failOnRewind">Whether the failure occurs while rewinding rather than reading.</param>
    /// <param name="accessFailure">Whether access is denied rather than an I/O failure.</param>
    [Theory]
    [ClassData(typeof(RecordingInspectionFailureCases))]
    public void GivenAStreamThatFailsInspection_WhenReplayIsRegisteredAndReported_ThenItIsUnreadableAndReleased(
        bool failOnRewind,
        bool accessFailure)
    {
        // Given
        const string name = "aircraft-synthetic.ndjson";
        using var payloads = new FaultingRecordingStream(failOnRewind, accessFailure);
        var recordings = Substitute.For<IRecordingLibrary>();
        recordings.Resolve(name).Returns(Path.Combine("recordings", name));
        recordings.Holds(name).Returns(true);
        recordings.Open(name).Returns(payloads);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Replay:Aircraft"] = name,
        }).Build();
        var written = new RecordingLogger<ReplayStartupReport>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogger<ReplayStartupReport>>(written);
        var startup = () =>
        {
            services.AddAircraftReplay(configuration, recordings);
            using var provider = services.BuildServiceProvider();
            provider.ReportReplayStartup();
            provider.GetServices<ITrackerSourceStrategy>().Should().BeEmpty();
        };

        // When
        startup.Should().NotThrow();

        // Then
        payloads.CanRead.Should().BeFalse("an unusable recording must release its handle");
        written.At(LogLevel.Warning).Should().ContainSingle().Which.Should().Contain(name).And.Contain("could not be opened or read");
    }

    private sealed class FaultingRecordingStream(bool failOnRewind, bool accessFailure)
        : MemoryStream(Encoding.UTF8.GetBytes(ReplayRecordingCases.SpanningSixMinutes))
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            failOnRewind ? base.Read(buffer, offset, count) : throw Failure();

        public override int Read(Span<byte> buffer) =>
            failOnRewind ? base.Read(buffer) : throw Failure();

        public override long Seek(long offset, SeekOrigin loc) =>
            failOnRewind ? throw Failure() : base.Seek(offset, loc);

        private Exception Failure() => accessFailure ? new UnauthorizedAccessException("Synthetic access failure") : new IOException("Synthetic read failure");
    }
}
