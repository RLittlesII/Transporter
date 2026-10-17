using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Contracts;
using Transporter.Integrations.OpenSky.Polling;
using Transporter.Scheduling;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Integrations.OpenSky;

public class AircraftPollInstantTests
{
    /// <summary>B-054: delayed success, refusal and failure keep request start for cadence and demand.</summary>
    /// <param name="demanded">Whether a demand performs the poll.</param>
    /// <param name="responseCode">The simulated outcome.</param>
    /// <returns>The completed regression check.</returns>
    [Theory(Timeout = 10000)]
    [ClassData(typeof(PollInstantCases))]
    public async Task GivenADelayedOutcome_WhenAPollCompletes_ThenItsStartAndDueInstantsUseThePollersClock(bool demanded, int responseCode)
    {
        // Given
        var started = new DateTimeOffset(2026, 10, 15, 14, 32, 10, TimeSpan.Zero);
        var scheduler = new TestScheduler();
        scheduler.AdvanceTo(started.UtcTicks - 1);
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        AircraftPollStatus status = new AircraftPollStatusFixture();
        var completion = new TaskCompletionSource<OpenSkyStatesResponse>();
        var api = Substitute.For<IOpenSkyApi>();
        api.GetStates(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(completion.Task);
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(api).WithStatus(status).WithProvider(schedulers);
        var reported = status.Status.Skip(1).FirstAsync().ToTask();
        using var cadence = demanded ? Disposable.Empty : sut.Poll();
        scheduler.AdvanceTo(started.UtcTicks);
        var performed = demanded ? sut.PollNow(CancellationToken.None) : Task.CompletedTask;
        api.ReceivedCalls().Should().ContainSingle("the request must be pending before time advances");

        // When
        scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);
        if (responseCode == 200)
        {
            completion.SetResult(OpenSkyPayloads.ThreeRows.Response with { Time = started.AddSeconds(-2).ToUnixTimeSeconds() });
        }
        else
        {
            completion.SetException(responseCode == 429
                ? new OpenSkyThrottledException(TimeSpan.FromSeconds(42))
                : new InvalidOperationException("Synthetic failed poll"));
        }

        await performed;
        var observed = await reported;

        // Then
        observed.PolledAt.IfNone(DateTimeOffset.MinValue).Should().Be(started);
        observed.NextDue.IfNone(DateTimeOffset.MinValue).Should().Be(started.AddSeconds(responseCode == 429 ? 42 : 15));
        observed.RefusedFor.IsSome.Should().Be(responseCode == 429);
        if (responseCode == 429)
        {
            observed.RefusedFor.IfNone(TimeSpan.Zero).Should().Be(TimeSpan.FromSeconds(42));
        }
    }
}

/// <summary>Builds the poll-status writer used by a client under test.</summary>
internal sealed class AircraftPollStatusFixture : AutoFixtureBase<AircraftPollStatusFixture>
{
    public static implicit operator AircraftPollStatus(AircraftPollStatusFixture fixture) => new();
}
