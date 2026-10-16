using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using LanguageExt;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Contracts;
using Transporter.Integrations.OpenSky.Model;
using Transporter.Integrations.OpenSky.Polling;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Integrations.OpenSky;

public class AircraftPollStatusTests
{
    /// <summary>
    /// B-054, the cadence. The poll the loop makes at 14:33:07 reports the next one due at 14:33:22,
    /// read from the scheduler the poll ran on. A due instant computed from the wall clock passes no
    /// test on a virtual scheduler, which is why this one runs on one.
    /// </summary>
    [Fact]
    public void GivenAPollAtAnInstant_WhenItIsPerformed_ThenTheNextIsReportedDueOneIntervalLater()
    {
        // Given
        var scheduler = new TestScheduler();
        scheduler.AdvanceTo(OneTickBefore(At(14, 33, 7)));
        var status = new AircraftPollStatus();
        var reported = Latest(status);
        AircraftSnapshotClient sut = Client(scheduler, status, Answering());

        // When
        using var polling = sut.Poll();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        // Then
        reported().Should().Be(new PollStatus { NextDue = At(14, 33, 22) });
    }

    /// <summary>
    /// B-054, a demand with no cadence running. The demanded poll is performed through the same
    /// <c>Fetch</c> and reports the same way, so a press moves the countdown at once rather than
    /// when a sleeping loop next wakes.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenNoCadenceRunning_WhenAPollIsDemandedAndPerformed_ThenTheNextIsReportedDueOneIntervalLater()
    {
        // Given
        var scheduler = new TestScheduler();
        scheduler.AdvanceTo(At(14, 40, 0).UtcTicks);
        var status = new AircraftPollStatus();
        var reported = Latest(status);
        AircraftSnapshotClient sut = Client(scheduler, status, Answering());

        // When
        await sut.PollNow(CancellationToken.None);

        // Then
        reported().Should().Be(new PollStatus { NextDue = At(14, 40, 15) });
    }

    /// <summary>
    /// B-055. A <c>429</c> asking for 42 seconds is reported as a refusal of 42 seconds, due exactly
    /// 42 seconds after the refused poll — and the cadence keeps that instant: it polls at 14:33:49
    /// and not at the fifteen-second interval, which a loop recomputing its wait from the last poll
    /// would, while reporting 14:33:49.
    /// </summary>
    [Fact]
    public void GivenAThrottledPoll_WhenItIsHandled_ThenARefusalOfItsSecondsIsReportedAndNextDueIsExactlyThatLater()
    {
        // Given
        var scheduler = new TestScheduler();
        scheduler.AdvanceTo(OneTickBefore(At(14, 33, 7)));
        var status = new AircraftPollStatus();
        var reported = Latest(status);
        var api = Answering(throttledFirst: TimeSpan.FromSeconds(42));
        AircraftSnapshotClient sut = Client(scheduler, status, api);
        Exception? errored = null;
        using var subscription = sut.Snapshots.Subscribe(static _ => { }, failure => errored = failure);

        // When
        using var polling = sut.Poll();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        var refusal = reported();
        scheduler.AdvanceTo(At(14, 33, 48).UtcTicks);
        var beforeTheDeferralEnds = api.ReceivedCalls().Count();
        scheduler.AdvanceTo(At(14, 33, 49).UtcTicks);

        // Then
        refusal.Should().Be(new PollStatus { NextDue = At(14, 33, 49), RefusedFor = TimeSpan.FromSeconds(42) });
        errored.Should().BeNull("a refusal is data, not a fault (B-028)");
        beforeTheDeferralEnds.Should().Be(1, "the interval has passed but the deferral has not");
        api.ReceivedCalls().Should().HaveCount(2, "the cadence polls exactly when the refusal said it would");
    }

    /// <summary>
    /// B-055, the clearing. The next poll that is applied reports no refusal, and the next due instant
    /// is one interval after it again.
    /// </summary>
    [Fact]
    public void GivenARefusalInForce_WhenTheNextPollIsApplied_ThenTheRefusalIsCleared()
    {
        // Given
        var scheduler = new TestScheduler();
        scheduler.AdvanceTo(OneTickBefore(At(14, 33, 7)));
        var status = new AircraftPollStatus();
        var reported = Latest(status);
        AircraftSnapshotClient sut = Client(scheduler, status, Answering(throttledFirst: TimeSpan.FromSeconds(42)));
        using var polling = sut.Poll();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        // When
        scheduler.AdvanceTo(At(14, 33, 50).UtcTicks);

        // Then
        reported().Should().Be(new PollStatus { NextDue = At(14, 34, 4) });
    }

    /// <summary>
    /// B-056's "a poller that has stopped". Disposing the cadence — the swap that stops it — writes
    /// none, so a stopped poller never reports a due instant it will not keep.
    /// </summary>
    [Fact]
    public void GivenARunningCadence_WhenItsSubscriptionIsDisposed_ThenNoneIsReported()
    {
        // Given
        var scheduler = new TestScheduler();
        scheduler.AdvanceTo(OneTickBefore(At(14, 33, 7)));
        var status = new AircraftPollStatus();
        var reported = Latest(status);
        AircraftSnapshotClient sut = Client(scheduler, status, Answering());
        var polling = sut.Poll();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        // When
        polling.Dispose();

        // Then
        reported().Should().Be(PollStatus.None);
    }

    /// <summary>Subscribes to the seam and answers the status in force each time it is read.</summary>
    /// <param name="status">The writer under test.</param>
    /// <returns>What reads the latest status.</returns>
    private static Func<PollStatus> Latest(IPollStatus status)
    {
        var observed = new List<PollStatus>();
        status.Status.Subscribe(observed.Add);

        return () => observed[^1];
    }

    /// <summary>The client over a test scheduler, writing the given status.</summary>
    /// <param name="scheduler">Where polls run and time is read.</param>
    /// <param name="status">The writer the client reports to.</param>
    /// <param name="api">The substituted contract.</param>
    /// <returns>The client.</returns>
    private static AircraftSnapshotClient Client(TestScheduler scheduler, AircraftPollStatus status, IOpenSkyApi api)
    {
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);

        return new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithStatus(status)
            .WithProvider(schedulers)
            .WithOptions(Options.Create(new OpenSkyOptions { Box = Houston, PollInterval = TimeSpan.FromSeconds(15) }));
    }

    /// <summary>A contract answering every poll with three rows, after an optional throttle.</summary>
    /// <param name="throttledFirst">The seconds a first-poll <c>429</c> asks for, or none.</param>
    /// <returns>The substituted contract.</returns>
    /// <remarks>
    /// The transport already turns a <c>429</c> into the exception carrying its header (§ 4 row 15),
    /// so a contract that throws it is the whole arrangement and <c>HttpTest</c> is not needed.
    /// </remarks>
    private static IOpenSkyApi Answering(TimeSpan? throttledFirst = null)
    {
        var polls = 0;
        var api = Substitute.For<IOpenSkyApi>();
        api.GetStates(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ++polls == 1 && throttledFirst is { } retryAfter
                ? throw new OpenSkyThrottledException(retryAfter)
                : Task.FromResult(OpenSkyPayloads.ThreeRows.Response));

        return api;
    }

    /// <summary>An instant on the day the scenarios read.</summary>
    /// <param name="hour">The hour.</param>
    /// <param name="minute">The minute.</param>
    /// <param name="second">The second.</param>
    /// <returns>The instant, in UTC.</returns>
    private static DateTimeOffset At(int hour, int minute, int second) => new(2026, 10, 15, hour, minute, second, TimeSpan.Zero);

    /// <summary>The virtual instant to start a cadence from, so its first poll lands on <paramref name="instant"/>.</summary>
    /// <param name="instant">When the first poll should be made.</param>
    /// <returns>The ticks one before it.</returns>
    /// <remarks>
    /// A virtual-time scheduler runs work scheduled for its current instant one tick later, so a
    /// cadence started at 14:33:07 polls at 14:33:07 and a tenth of a microsecond.
    /// </remarks>
    private static long OneTickBefore(DateTimeOffset instant) => instant.UtcTicks - 1;

    /// <summary>The box decisions/0001 chose.</summary>
    private static readonly BoundingBox Houston = new()
    {
        LatitudeMinimum = 28.8,
        LongitudeMinimum = -96.0,
        LatitudeMaximum = 30.4,
        LongitudeMaximum = -94.2,
    };
}
