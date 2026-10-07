using System.Reactive.Linq;
using System.Reactive.Subjects;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Reactive.Testing;
using Transponder.Model;
using Transponder.Scheduling;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;
using Transponder.UnitTests.Scheduling;

namespace Transponder.UnitTests.Tracking;

public class FleetNoticeTests
{
    /// <summary>
    /// fleet-pipeline B-025. A changeset that changed something raises one notice carrying the
    /// observed instant and what moved — five tracked, one added, two updated, one removed. The
    /// instant is the clock's and never a wall-clock read, which is what lets a recorded source
    /// raise the same notices a live one does.
    /// </summary>
    [Fact]
    public void GivenAChangesetThatChangedSomething_WhenTheNoticesAreObserved_ThenOneCarriesTheInstantAndTheCounts()
    {
        // Given
        var scheduler = new TestScheduler();
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        FleetTracker sut = Tracker(cache, clock, scheduler);
        var raised = new List<FleetNotice>();
        using var subscription = sut.Notices(Observable.Never<TimeSpan>()).Subscribe(raised.Add);
        ((IObservedClockWriter) clock).Observe(Observed);
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland"));
        cache.AddOrUpdate(Reporting("d4e5f6", "Testland"));
        cache.AddOrUpdate(Reporting("070809", "Exampleland"));
        cache.AddOrUpdate(Reporting("b1c2d3", "Exampleland"));
        cache.AddOrUpdate(Reporting("e4f5a6", "Pacifica"));
        scheduler.AdvanceBy(FleetTracker.DefaultNoticeInterval.Ticks);
        raised.Clear();

        // When
        cache.Edit(static updater =>
        {
            updater.AddOrUpdate(Reporting("c7d8e9", "Pacifica"));
            updater.AddOrUpdate(Reporting("a1b2c3", "Testland"));
            updater.AddOrUpdate(Reporting("d4e5f6", "Pacifica"));
            updater.Remove("e4f5a6");
        });
        scheduler.AdvanceBy(FleetTracker.DefaultNoticeInterval.Ticks);

        // Then
        var notice = raised.Should().ContainSingle().Subject;
        notice.Kind.Should().Be(FleetNoticeKind.Updated);
        notice.Instant.Should().Be(Observed, "the notice reports the observed instant, not the moment it was raised");
        notice.Tracked.Should().Be(5);
        notice.Added.Should().Be(1);
        notice.Updated.Should().Be(2);
        notice.Removed.Should().Be(1);
    }

    /// <summary>
    /// fleet-pipeline B-025's second half. A poll where nothing moved produces an empty changeset and
    /// no notice, which is what makes silence mean "nothing moved" — a notice per envelope would
    /// announce a hundred and eighty non-events in a three-quarter-hour talk.
    /// </summary>
    [Fact]
    public void GivenAnEmptyChangeset_WhenTheNoticesAreObserved_ThenNoNoticeIsRaised()
    {
        // Given
        var scheduler = new TestScheduler();
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        FleetTracker sut = Tracker(cache, clock, scheduler);
        var raised = new List<FleetNotice>();
        using var subscription = sut.Notices(Observable.Never<TimeSpan>()).Subscribe(raised.Add);
        ((IObservedClockWriter) clock).Observe(Observed);
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland"));
        scheduler.AdvanceBy(FleetTracker.DefaultNoticeInterval.Ticks);
        raised.Clear();

        // When
        cache.Edit(static _ => { });
        scheduler.AdvanceBy(FleetTracker.DefaultNoticeInterval.Ticks);

        // Then
        raised.Should().BeEmpty("a changeset carrying no change is a poll where nothing moved");
    }

    /// <summary>
    /// fleet-pipeline B-026. Two consumers pace the same notices independently: each receives at
    /// most one per its own interval, and the one it receives is the most recent in its window. The
    /// cases bracket the interesting orders — the slow subscriber first, the fast one first, and two
    /// at the same cadence — so an implementation that paced per tracker rather than per consumer
    /// fails whichever subscribed second.
    /// </summary>
    /// <param name="first">The first subscriber's interval, in seconds.</param>
    /// <param name="second">The second subscriber's interval, in seconds.</param>
    /// <param name="expected">How many notices the first subscriber receives over ten seconds of notices.</param>
    [Theory]
    [InlineData(1, 60, 10)]
    [InlineData(60, 1, 0)]
    [InlineData(1, 1, 10)]
    public void GivenTwoSubscribersAtDifferentIntervals_WhenNoticesArriveFaster_ThenEachReceivesTheLatestAtItsOwnCadence(
        int first,
        int second,
        int expected)
    {
        // Given
        var scheduler = new TestScheduler();
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        FleetTracker sut = Tracker(cache, clock, scheduler);
        var toFirst = new List<FleetNotice>();
        var toSecond = new List<FleetNotice>();
        using var fast = sut.Notices(Observable.Return(TimeSpan.FromSeconds(first))).Subscribe(toFirst.Add);
        using var slow = sut.Notices(Observable.Return(TimeSpan.FromSeconds(second))).Subscribe(toSecond.Add);
        ((IObservedClockWriter) clock).Observe(Observed);

        // When
        for (var change = 0; change < 10; change++)
        {
            cache.AddOrUpdate(Reporting($"a1b2c{change}", "Testland"));
            scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
        }

        // Then
        toFirst.Should().HaveCount(expected, "a subscriber receives at most one notice per its own interval");
        toFirst.Concat(toSecond).Should().OnlyContain(
            static notice => notice.Tracked > 0,
            "each notice is one that was actually raised, and the most recent in its window");
    }

    /// <summary>
    /// fleet-pipeline B-026's last clause: an interval changed while the application runs takes
    /// effect without rebuilding anything. The cap starts at a minute, nothing arrives inside it,
    /// and a second later it is a second — so the notices a consumer was not receiving start
    /// arriving, over the same subscription and the same state.
    /// </summary>
    [Fact]
    public void GivenASubscriberPacedByAMinute_WhenItAsksForASecondInstead_ThenNoticesArriveWithoutResubscribing()
    {
        // Given
        var scheduler = new TestScheduler();
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var source = new CountingTrackerSource(cache);
        FleetTracker sut = new FleetTrackerFixture()
            .WithSource(source)
            .WithClock(clock)
            .WithTicks(clock)
            .WithProvider(Provider(scheduler));
        var paced = new BehaviorSubject<TimeSpan>(TimeSpan.FromMinutes(1));
        var raised = new List<FleetNotice>();
        using var subscription = sut.Notices(paced).Subscribe(raised.Add);
        ((IObservedClockWriter) clock).Observe(Observed);
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland"));
        scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks);
        var underTheMinute = raised.Count;

        // When
        paced.OnNext(TimeSpan.FromSeconds(1));
        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        // Then
        underTheMinute.Should().Be(0, "five seconds is inside the minute the consumer asked for");
        raised.Should().ContainSingle("the notice held in the window arrives once the window shortens");
        source.Connections.Should().Be(1, "the cadence changed and nothing was rebuilt or re-subscribed");
    }

    /// <summary>
    /// fleet-pipeline B-027. Silence is reported once: the observed instant runs six minutes past
    /// the last changeset against a five-minute threshold, one quiet notice is raised, advancing
    /// further raises no second one, and the next changeset that changes something is a resumed
    /// notice. Without the quiet notice a stopped feed and a quiet one are the same absence of
    /// notices, which is the one case where nothing arriving is not information.
    /// </summary>
    [Fact]
    public void GivenNoChangesetForLongerThanTheThreshold_WhenTheClockAdvances_ThenAQuietNoticeIsRaisedOnceAndTheNextChangesetResumes()
    {
        // Given
        var scheduler = new TestScheduler();
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        FleetTracker sut = Tracker(cache, clock, scheduler);
        var raised = new List<FleetNotice>();
        using var subscription = sut.Notices(Observable.Never<TimeSpan>()).Subscribe(raised.Add);
        ((IObservedClockWriter) clock).Observe(Observed);
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland"));
        scheduler.AdvanceBy(FleetTracker.DefaultNoticeInterval.Ticks);
        raised.Clear();

        // When
        ((IObservedClockWriter) clock).Observe(Observed + TimeSpan.FromMinutes(6));
        scheduler.AdvanceBy(FleetTracker.DefaultNoticeInterval.Ticks);
        var afterSixMinutes = raised.ToList();
        ((IObservedClockWriter) clock).Observe(Observed + TimeSpan.FromMinutes(7));
        scheduler.AdvanceBy(FleetTracker.DefaultNoticeInterval.Ticks);
        var afterSeven = raised.ToList();
        cache.AddOrUpdate(Reporting("d4e5f6", "Testland"));
        scheduler.AdvanceBy(FleetTracker.DefaultNoticeInterval.Ticks);

        // Then
        afterSixMinutes.Should().ContainSingle().Which.Kind.Should().Be(FleetNoticeKind.Quiet);
        afterSixMinutes[0].Instant.Should().Be(Observed + TimeSpan.FromMinutes(6), "the quiet notice reports the instant it was noticed at");
        afterSeven.Should().HaveCount(1, "silence is reported once rather than on every advance");
        raised[^1].Kind.Should().Be(FleetNoticeKind.Resumed, "the first changeset after a quiet spell says the feed is back");
        raised[^1].Added.Should().Be(1, "a resumed notice still reports what moved");
    }

    /// <summary>Builds the tracker over a cache, a clock and one scheduler in every position.</summary>
    /// <param name="cache">The store the seam reports from.</param>
    /// <param name="clock">The observed clock the notices read.</param>
    /// <param name="scheduler">Where the rate cap is timed.</param>
    /// <returns>The tracker.</returns>
    private static FleetTracker Tracker(SourceCache<TransportVehicle, string> cache, ObservedClock clock, TestScheduler scheduler) =>
        new FleetTrackerFixture()
            .WithSource(new CountingTrackerSource(cache))
            .WithClock(clock)
            .WithTicks(clock)
            .WithProvider(Provider(scheduler));

    /// <summary>One scheduler in both positions, which is what advancing time once requires.</summary>
    /// <param name="scheduler">The scheduler the test advances.</param>
    /// <returns>The provider.</returns>
    private static SchedulerProvider Provider(TestScheduler scheduler) =>
        new SchedulerProviderFixture().WithTestScheduler(scheduler);

    /// <summary>One aircraft, reporting the country the default grouping reads.</summary>
    /// <param name="key">The vehicle's key.</param>
    /// <param name="country">What the grouping reads.</param>
    /// <returns>The aircraft.</returns>
    private static Aircraft Reporting(string key, string country) =>
        new(key, Observed) { OriginCountry = country };

    /// <summary>The observed instant every notice here reports.</summary>
    private static readonly DateTimeOffset Observed = new(2026, 10, 5, 14, 32, 10, TimeSpan.Zero);
}
