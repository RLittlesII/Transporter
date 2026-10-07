using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Recording;
using Transponder.Scheduling;
using Transponder.UnitTests.Integrations.OpenSky;
using Transponder.UnitTests.Scheduling;

namespace Transponder.UnitTests.Recording;

public class RecordingPacerTests
{
    /// <summary>
    /// B-007. The gap between two recorded instants is the only cadence replay has, so a payload
    /// recorded fifteen seconds after the one before it is released fifteen seconds after it — and
    /// the assertion that matters is the negative one, that it is not released at fourteen. A fixed
    /// interval substituted here is a recording visibly not the feed it recorded.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenThreePayloadsFifteenSecondsApart_WhenTheRecordingIsReplayed_ThenEachIsReleasedAtTheRecordedSpacing()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var recording = new MemoryStream(Encoding.UTF8.GetBytes(FifteenSecondsApart));
        RecordingPacer sut = new RecordingPacerFixture().WithRecording(recording).WithProvider(schedulers);

        // When
        var first = await sut.Next(CancellationToken.None);
        var second = sut.Next(CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(14).Ticks);
        var secondAtFourteenSeconds = second.IsCompleted;
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        var third = sut.Next(CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(15).Ticks);

        // Then
        first.Should().Be(First);
        secondAtFourteenSeconds.Should().BeFalse("the recorded gap is fifteen seconds and nothing shortens it");
        (await second).Should().Be(Second);
        (await third).Should().Be(Third);
    }

    /// <summary>
    /// B-008. Past the end the first payload follows the last and the stream carries on: nothing
    /// completes, nothing faults, and the pacer clears nothing on the way round — so a cache above
    /// it sees the changes between the last payload and the first rather than a removal of every
    /// vehicle. The boundary waits the recording's last gap, because there is no recorded interval
    /// between the final payload and the first and inventing a fixed one is what B-007 forbids.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenTheRecordingIsReplayedPastItsEnd_WhenTheBoundaryIsCrossed_ThenTheFirstPayloadFollowsTheLastAndNoChangesetRemovesEveryAircraft()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var recording = new MemoryStream(Encoding.UTF8.GetBytes(FifteenSecondsApart));
        RecordingPacer sut = new RecordingPacerFixture().WithRecording(recording).WithProvider(schedulers);
        var released = new List<string> { await sut.Next(CancellationToken.None) };

        // When
        var second = sut.Next(CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(15).Ticks);
        released.Add(await second);
        var third = sut.Next(CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(15).Ticks);
        released.Add(await third);
        var acrossTheBoundary = sut.Next(CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(14).Ticks);
        var boundaryAtFourteenSeconds = acrossTheBoundary.IsCompleted;
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        // Then
        acrossTheBoundary.IsFaulted.Should().BeFalse("a recording that ran out is a recording that loops");
        boundaryAtFourteenSeconds.Should().BeFalse("the boundary is spaced by the recording's own last gap");
        released.Should().Equal(First, Second, Third);
        (await acrossTheBoundary).Should().Be(First);
    }

    /// <summary>
    /// B-012. A recording truncated mid-write is the normal result of stopping a rehearsal, so the
    /// torn line costs one payload and nothing else: every complete line before it is replayed, the
    /// stream does not fault, and the loop still comes round to the first payload.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenARecordingWhoseFinalLineIsCutMidToken_WhenItIsReplayed_ThenEveryCompleteLineIsReplayedAndTheStreamDoesNotFault()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var logger = new RecordingLogger<RecordingPacer>();
        using var recording = new MemoryStream(Encoding.UTF8.GetBytes(CutMidToken));
        RecordingPacer sut = new RecordingPacerFixture()
            .WithRecording(recording)
            .WithProvider(schedulers)
            .WithLogger(logger);
        var released = new List<string> { await sut.Next(CancellationToken.None) };

        // When
        var second = sut.Next(CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(15).Ticks);
        released.Add(await second);
        var pastTheTornLine = sut.Next(CancellationToken.None);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(15).Ticks);

        // Then
        pastTheTornLine.IsFaulted.Should().BeFalse("a torn line is data about a stopped rehearsal, not a fault");
        released.Should().Equal(First, Second);
        (await pastTheTornLine).Should().Be(First);
        logger.At(LogLevel.Debug).Should().ContainMatch("*discarded*");
    }

    /// <summary>
    /// B-027. The pacer is driven from a stream of synthetic lines in memory: it opens nothing,
    /// resolves nothing, and takes no options — which is what keeps the one component carrying
    /// time-base logic free of a file system. Proven by a test that has none rather than by reading
    /// the constructor.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenSyntheticLinesInAMemoryStream_WhenThePacerIsDriven_ThenItReplaysThemWithNoFileSystemAndReadsNoConfiguration()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        using var recording = new MemoryStream(Encoding.UTF8.GetBytes(FifteenSecondsApart));
        RecordingPacer sut = new RecordingPacerFixture().WithRecording(recording).WithProvider(schedulers);

        // When
        var released = new List<string> { await sut.Next(CancellationToken.None) };
        for (var remaining = 2; remaining > 0; remaining--)
        {
            var next = sut.Next(CancellationToken.None);
            scheduler.AdvanceBy(TimeSpan.FromSeconds(15).Ticks);
            released.Add(await next);
        }

        // Then
        released.Should().Equal(First, Second, Third);
    }

    /// <summary>
    /// The loop seeks to the recording's beginning rather than reopening a file the pacer never
    /// opened, so a stream that cannot seek is refused where it is handed over. Guarded in the
    /// constructor because the alternative is a failure that first appears at the loop boundary,
    /// which is minutes into a talk.
    /// </summary>
    [Fact]
    public void GivenAStreamThatCannotSeek_WhenThePacerIsConstructed_ThenItSaysSoRatherThanFailingAtTheLoopBoundary()
    {
        // Given
        using var unseekable = new UnseekableStream();

        // When
        var construction = () => (RecordingPacer) new RecordingPacerFixture().WithRecording(unseekable);

        // Then
        construction.Should().Throw<ArgumentException>().WithMessage("*seekable*");
    }

    /// <summary>Composes one recorded line, in the shape ADR-0004 fixes and the recorder writes.</summary>
    /// <param name="instant">The instant the payload arrived.</param>
    /// <param name="body">The payload, as the provider sent it.</param>
    /// <returns>The line, newline included.</returns>
    private static string Line(string instant, string body) => $"{{\"receivedAt\":\"{instant}\",\"body\":{body}}}\n";

    /// <summary>A stream that reads but cannot seek, which is what the constructor's guard is for.</summary>
    private sealed class UnseekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }

    /// <summary>The payload the recording opens with, as the provider would have sent it.</summary>
    private const string First = """{"time":1791124331,"states":[["a1b2c3","TRN0001 ","Testland"]]}""";

    /// <summary>The payload fifteen seconds after the first.</summary>
    private const string Second = """{"time":1791124346,"states":[["d4e5f6","TRN0002 ","Testland"]]}""";

    /// <summary>The payload the recording ends with, reporting two aircraft.</summary>
    private const string Third =
        """{"time":1791124361,"states":[["a1b2c3","TRN0001 ","Testland"],["d4e5f6","TRN0002 ","Testland"]]}""";

    /// <summary>Three payloads at 14:32:11, 14:32:26 and 14:32:41 — the spacing B-007 is about.</summary>
    private static readonly string FifteenSecondsApart = string.Concat(
        Line("2026-10-04T14:32:11.113Z", First),
        Line("2026-10-04T14:32:26.113Z", Second),
        Line("2026-10-04T14:32:41.113Z", Third));

    /// <summary>
    /// The same recording stopped part-way through its third line, which is what a rehearsal
    /// stopped with a keystroke leaves behind.
    /// </summary>
    private static readonly string CutMidToken = string.Concat(
        Line("2026-10-04T14:32:11.113Z", First),
        Line("2026-10-04T14:32:26.113Z", Second),
        """{"receivedAt":"2026-10-04T14:32:41.113Z","bo""");
}

/// <summary>Builds the pacer, so a constructor change edits this and not every test.</summary>
[AutoFixture(typeof(RecordingPacer))]
internal partial class RecordingPacerFixture
{
    public RecordingPacerFixture()
    {
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(new TestScheduler());

        WithRecording(new MemoryStream());
        WithProvider(schedulers);
        WithLogger(new RecordingLogger<RecordingPacer>());
    }
}
