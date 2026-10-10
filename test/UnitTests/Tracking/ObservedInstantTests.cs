using System.Collections.Generic;
using AwesomeAssertions;
using DynamicData;
using NSubstitute;
using Transporter.Model;
using Transporter.Tracking;

namespace Transporter.UnitTests.Tracking;

public class ObservedInstantTests
{
    /// <summary>
    /// fleet-pipeline B-031. A poll whose response was identical changes nothing, so it raises no
    /// notice (B-025) — and it still reports an instant, which the tracker publishes. That is the
    /// whole reason this member exists: `fleet-dashboard` B-028's refresh indicator has to clear on
    /// a poll that moved no aircraft, and the notices cannot say one happened. Nothing is
    /// deduplicated either: the same instant reported twice is two polls, and a
    /// <c>DistinctUntilChanged</c> here would leave the indicator spinning after the second.
    /// </summary>
    [Fact]
    public void GivenAPollThatChangedNothing_WhenItReportsAnInstant_ThenTheTrackerPublishesItAnyway()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock);
        var observed = new List<DateTimeOffset>();
        using var subscription = sut.Observed.Subscribe(observed.Add);
        ((IObservedClockWriter) clock).Observe(Reported);
        cache.AddOrUpdate(new Aircraft("a1b2c3", Reported));

        // When
        ((IObservedClockWriter) clock).Observe(Reported);

        // Then
        observed.Should().HaveCount(3, "the instant in force, the first poll's, and the identical poll's");
        observed[1].Should().Be(Reported);
        observed[2].Should().Be(Reported, "a poll that changed nothing still landed, and says so with the instant it reported");
    }

    /// <summary>
    /// fleet-pipeline B-031. The published value is the instant the provider reported and never a
    /// wall-clock read, which is what makes it usable under replay: a recording from 2021 publishes
    /// 2021. A member that answered <c>DateTimeOffset.UtcNow</c> on each advance would pass a
    /// careless test and be wrong in exactly the replayed demo this repository exists to give
    /// (ADR-0007, `aircraft-source` B-003).
    /// </summary>
    [Fact]
    public void GivenAnInstantFromARecording_WhenItIsPublished_ThenItIsTheProvidersValueAndNotAWallClockRead()
    {
        // Given
        var clock = new ObservedClock();
        var recorded = new DateTimeOffset(2021, 3, 14, 9, 26, 53, TimeSpan.Zero);
        FleetTracker sut = new FleetTrackerFixture().WithClock(clock).WithTicks(clock);
        var observed = new List<DateTimeOffset>();
        using var subscription = sut.Observed.Subscribe(observed.Add);

        // When
        ((IObservedClockWriter) clock).Observe(recorded);

        // Then
        observed.Last().Should().Be(recorded, "the recording's time, to the second the provider reported");
        observed.Last().Should().NotBeCloseTo(
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(1),
            "a wall-clock read would be today's date, and a replayed fleet would be stale on load");
    }

    /// <summary>
    /// fleet-pipeline B-031, and the fact a consumer has to know about. The clock holds the instant
    /// in force, so subscribing is itself an emission — before any poll that is
    /// <see cref="DateTimeOffset.MinValue"/>. A consumer timing something from a gesture, which is
    /// `fleet-dashboard` `0058`, must therefore skip the value it gets on subscription or it will
    /// read the press as the poll it was waiting for.
    /// </summary>
    [Fact]
    public void GivenNoPollHasHappened_WhenAConsumerSubscribes_ThenItReadsTheInstantInForceBeforeAnyAdvance()
    {
        // Given
        var clock = new ObservedClock();
        FleetTracker sut = new FleetTrackerFixture().WithClock(clock).WithTicks(clock);
        var observed = new List<DateTimeOffset>();

        // When
        using var subscription = sut.Observed.Subscribe(observed.Add);

        // Then
        observed.Should().ContainSingle().Which.Should().Be(
            DateTimeOffset.MinValue,
            "nothing has reported, and the in-force instant is published on subscription");
    }

    /// <summary>
    /// fleet-pipeline B-004, which covers this member like every other published stream: disposal
    /// completes it and no later advance reaches a consumer. The clock outlives the tracker — it is
    /// a container singleton of its own — so a stream handed out without the shutdown on it would
    /// keep delivering to a disposed tracker's subscribers.
    /// </summary>
    [Fact]
    public void GivenASubscriberToTheObservedInstant_WhenTheTrackerIsDisposed_ThenTheStreamCompletesAndNoFurtherInstantArrives()
    {
        // Given
        var clock = new ObservedClock();
        FleetTracker sut = new FleetTrackerFixture().WithClock(clock).WithTicks(clock);
        var observed = new List<DateTimeOffset>();
        var completed = false;
        using var subscription = sut.Observed.Subscribe(observed.Add, () => completed = true);

        // When
        sut.Dispose();
        ((IObservedClockWriter) clock).Observe(Reported);

        // Then
        completed.Should().BeTrue("the published stream completes, which is what a consumer can observe");
        observed.Should().ContainSingle("only the instant in force, which arrived before the disposal");
    }

    /// <summary>The instant a provider reported, which is the only clock any of these tests reads.</summary>
    private static readonly DateTimeOffset Reported = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
}
