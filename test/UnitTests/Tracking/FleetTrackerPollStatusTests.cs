using System.Collections.Generic;
using System.Reactive.Subjects;
using AwesomeAssertions;
using LanguageExt;
using NSubstitute;
using Transporter.Tracking;

namespace Transporter.UnitTests.Tracking;

public class FleetTrackerPollStatusTests
{
    /// <summary>
    /// fleet-pipeline B-040, "nothing older". A consumer subscribing after a status was reported
    /// reads that status at once and only that one. A tracker that buffered its own copy — a
    /// <c>Replay</c> of more than the seam's latest — hands the late subscriber the earlier status
    /// too, which is a refusal the provider already lifted shown as current.
    /// </summary>
    [Fact]
    public void GivenAStatusInForce_WhenAConsumerSubscribesLate_ThenItReadsThatStatusAndNoOlder()
    {
        // Given
        var seam = new BehaviorSubject<PollStatus>(PollStatus.None);
        FleetTracker sut = new FleetTrackerFixture().WithStatus(new PollStatusDouble(seam));
        using var early = sut.PollStatus.Subscribe();
        seam.OnNext(Due(Earlier));
        seam.OnNext(Due(Next));
        var observed = new List<PollStatus>();

        // When
        using var late = sut.PollStatus.Subscribe(observed.Add);

        // Then
        observed.Should().ContainSingle("the status in force, read at once, and nothing reported before it")
            .Which.Should().Be(Due(Next));
    }

    /// <summary>
    /// fleet-pipeline B-040, "re-published, not produced". A refusal of 42 seconds and then the next
    /// applied poll clearing it arrive in that order and nothing arrives between them, and the clock
    /// is never read. A tracker that counted down, or timed the refusal itself, adds values the seam
    /// never wrote and reads a clock to do it.
    /// </summary>
    [Fact]
    public void GivenARefusalThenAnAppliedPoll_WhenTheSeamReportsThem_ThenTheTrackerRepublishesExactlyThoseAndReadsNoClock()
    {
        // Given
        var seam = new BehaviorSubject<PollStatus>(Due(Earlier));
        var clock = Substitute.For<IObservedClock>();
        FleetTracker sut = new FleetTrackerFixture().WithClock(clock).WithStatus(new PollStatusDouble(seam));
        var observed = new List<PollStatus>();
        using var subscription = sut.PollStatus.Subscribe(observed.Add);
        var refused = new PollStatus { NextDue = Earlier + Refusal, RefusedFor = Refusal };
        var applied = Due(Next);

        // When
        seam.OnNext(refused);
        seam.OnNext(applied);

        // Then
        observed.Should().Equal(Due(Earlier), refused, applied);
        (observed[1].RefusedFor == Option<TimeSpan>.Some(Refusal)).Should().BeTrue("the refusal carries the 42 seconds the provider asked for");
        observed[2].RefusedFor.IsNone.Should().BeTrue("the applied poll clears the refusal");
        _ = clock.DidNotReceive().Current;
    }

    /// <summary>
    /// fleet-pipeline B-040, the swap. The decorator swapping a polled source for one that pushes
    /// reports <see cref="PollStatus.None"/> next; the tracker re-publishes it as none, so the
    /// outgoing poller's due instant is not left on the page beside a source that never polls.
    /// </summary>
    [Fact]
    public void GivenAPolledSourceReportingADueInstant_WhenItIsSwappedForOneThatPushes_ThenThePollStatusIsReplacedByNone()
    {
        // Given
        var seam = new BehaviorSubject<PollStatus>(Due(Next));
        FleetTracker sut = new FleetTrackerFixture().WithStatus(new PollStatusDouble(seam));
        var observed = new List<PollStatus>();
        using var subscription = sut.PollStatus.Subscribe(observed.Add);

        // When
        seam.OnNext(PollStatus.None);

        // Then
        observed.Should().Equal(Due(Next), PollStatus.None);
        observed[^1].NextDue.IsNone.Should().BeTrue("a source that does not poll has no next poll");
    }

    /// <summary>
    /// fleet-pipeline B-040 and § 7. The placeholder registered until a source writes a status reads
    /// none at once, and never completes on its own: completion is the tracker's disposal and
    /// nothing else's (B-004), so a placeholder that completed would look like a disposed tracker.
    /// </summary>
    [Fact]
    public void GivenNoPollStatus_WhenItIsRead_ThenItIsNoneAtOnceAndNeverCompletes()
    {
        // Given
        FleetTracker sut = new FleetTrackerFixture().WithStatus(new NoPollStatus());
        var observed = new List<PollStatus>();
        var completed = false;

        // When
        using var subscription = sut.PollStatus.Subscribe(observed.Add, () => completed = true);

        // Then
        observed.Should().Equal(PollStatus.None);
        completed.Should().BeFalse("only the tracker's disposal completes a published stream");
    }

    /// <summary>
    /// fleet-pipeline B-004 for the poll status. Disposing the tracker completes the stream a
    /// consumer is bound to, and a status the seam reports afterwards reaches nobody.
    /// </summary>
    [Fact]
    public void GivenABoundConsumer_WhenTheTrackerIsDisposed_ThenThePollStatusCompletesAndNothingFollows()
    {
        // Given
        var seam = new BehaviorSubject<PollStatus>(Due(Earlier));
        FleetTracker sut = new FleetTrackerFixture().WithStatus(new PollStatusDouble(seam));
        var observed = new List<PollStatus>();
        var completed = false;
        using var subscription = sut.PollStatus.Subscribe(observed.Add, () => completed = true);

        // When
        sut.Dispose();
        seam.OnNext(Due(Next));

        // Then
        completed.Should().BeTrue();
        observed.Should().Equal(Due(Earlier));
    }

    /// <summary>A status whose next poll is due at an instant, with no refusal.</summary>
    /// <param name="instant">When the next poll is due.</param>
    /// <returns>The status a poller reports after an applied poll.</returns>
    private static PollStatus Due(DateTimeOffset instant) => new() { NextDue = instant };

    /// <summary>The interval the provider asked for in the B-040 scenario.</summary>
    private static readonly TimeSpan Refusal = TimeSpan.FromSeconds(42);

    /// <summary>A due instant reported before the one in force.</summary>
    private static readonly DateTimeOffset Earlier = new(2026, 10, 9, 14, 32, 10, TimeSpan.Zero);

    /// <summary>The due instant the B-040 scenario reads.</summary>
    private static readonly DateTimeOffset Next = new(2026, 10, 9, 14, 32, 25, TimeSpan.Zero);
}

/// <summary>The seam as a test writes it, standing in for the poller and the decorator alike.</summary>
/// <param name="status">What the test reports, starting with the status in force.</param>
internal sealed class PollStatusDouble(BehaviorSubject<PollStatus> status) : IPollStatus
{
    /// <inheritdoc/>
    public IObservable<PollStatus> Status => status;
}
