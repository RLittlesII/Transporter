using System.Reactive.Linq;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Reactive.Testing;
using Transporter.Model;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Tracking;

public class FleetNoticeWindowTests
{
    /// <summary>
    /// fleet-pipeline B-039. Twenty-five changes leave the last twenty, oldest first, with their
    /// totals summed in the stage, and a consumer pacing its own notices at a minute thins nothing.
    /// The hazard is a window built over a paced stream, which holds what that consumer saw.
    /// </summary>
    [Fact]
    public void GivenTwentyFiveChanges_WhenTheWindowIsObserved_ThenItHoldsTheLastTwentyOldestFirstWithTheirTotals()
    {
        // Given
        var scheduler = new TestScheduler();
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        FleetTracker sut = Tracker(cache, clock, scheduler);
        var windows = new List<FleetNoticeWindow>();
        using var paced = sut.Notices(Observable.Return(TimeSpan.FromMinutes(1))).Subscribe(static _ => { });
        using var subscription = sut.Recent.Subscribe(windows.Add);

        // When
        for (var poll = 0; poll < 25; poll++)
        {
            cache.AddOrUpdate(Reporting($"a{poll:00000}", "Testland"));
        }

        // Then
        var window = windows[^1];
        window.Notices.Should().HaveCount(FleetTracker.RecentWindowSize, "the oldest five were dropped");
        window.Notices.Select(static notice => notice.Tracked).Should().Equal(Enumerable.Range(6, 20), "oldest first");
        window.Notices.Should().OnlyContain(static notice => notice.Kind == FleetNoticeKind.Updated);
        window.Added.Should().Be(20, "the totals are summed over the twenty the window holds");
        window.Updated.Should().Be(0);
        window.Removed.Should().Be(0);
    }

    /// <summary>
    /// fleet-pipeline B-039. A search hides and restores vehicles, and that is not a change the source
    /// made. The hazard is a window built over the filtered stream, where a search reads as removes
    /// and adds and the banner reports a burst of traffic that never happened.
    /// </summary>
    [Fact]
    public void GivenAWindowOfTwo_WhenASearchHidesHalfAndIsCleared_ThenTheWindowStillHoldsTwo()
    {
        // Given
        var scheduler = new TestScheduler();
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        FleetTracker sut = Tracker(cache, clock, scheduler);
        var windows = new List<FleetNoticeWindow>();
        using var bound = sut.Fleet.Subscribe(static _ => { });
        using var subscription = sut.Recent.Subscribe(windows.Add);
        cache.Edit(static updater =>
        {
            updater.AddOrUpdate(Reporting("a1b2c3", "Testland"));
            updater.AddOrUpdate(Reporting("d4e5f6", "Exampleland"));
        });
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland"));

        // When
        sut.Filter(static vehicle => vehicle.Key == "a1b2c3");
        sut.Filter(FleetTracker.DefaultPredicate);

        // Then
        windows[^1].Notices.Should().HaveCount(2, "a search is not a change the source made");
    }

    /// <summary>
    /// fleet-pipeline B-039. The fleet handed to a stage as it connects is counted and not entered,
    /// so a window opened while the fleet is bound starts empty and the next change carries the
    /// right tracked count. The hazard is three aircraft already on screen announced as arrivals.
    /// </summary>
    [Fact]
    public void GivenABoundFleetOfThree_WhenTheWindowIsOpenedAndAFourthArrives_ThenItStartsEmptyAndHoldsOneNotice()
    {
        // Given
        var scheduler = new TestScheduler();
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        FleetTracker sut = Tracker(cache, clock, scheduler);
        using var bound = sut.Fleet.Subscribe(static _ => { });
        cache.Edit(static updater =>
        {
            updater.AddOrUpdate(Reporting("a1b2c3", "Testland"));
            updater.AddOrUpdate(Reporting("d4e5f6", "Testland"));
            updater.AddOrUpdate(Reporting("070809", "Testland"));
        });
        var windows = new List<FleetNoticeWindow>();

        // When
        using var subscription = sut.Recent.Subscribe(windows.Add);
        var opened = windows.ToArray();
        cache.AddOrUpdate(Reporting("b1c2d3", "Testland"));

        // Then
        opened.Should().ContainSingle("a subscriber reads the window in force at once").Which.Notices.Should().BeEmpty();
        var notice = windows[^1].Notices.Should().ContainSingle().Subject;
        notice.Added.Should().Be(1);
        notice.Tracked.Should().Be(4, "the connecting fleet was counted though not entered");
    }

    /// <summary>
    /// fleet-pipeline B-039. On a source with nothing yet, the first poll is a change. The hazard is
    /// DynamicData's <c>SkipInitial()</c>, which skips the first changeset that is not empty — here
    /// the first poll — and so loses the arrival the banner exists to show.
    /// </summary>
    [Fact]
    public void GivenAnEmptySource_WhenTheFirstPollAddsThree_ThenTheWindowHoldsOneNoticeAddingThree()
    {
        // Given
        var scheduler = new TestScheduler();
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        FleetTracker sut = Tracker(cache, clock, scheduler);
        var windows = new List<FleetNoticeWindow>();
        using var subscription = sut.Recent.Subscribe(windows.Add);

        // When
        cache.Edit(static updater =>
        {
            updater.AddOrUpdate(Reporting("a1b2c3", "Testland"));
            updater.AddOrUpdate(Reporting("d4e5f6", "Testland"));
            updater.AddOrUpdate(Reporting("070809", "Testland"));
        });

        // Then
        var notice = windows[^1].Notices.Should().ContainSingle().Subject;
        notice.Added.Should().Be(3);
        windows[^1].Added.Should().Be(3);
    }

    /// <summary>
    /// fleet-pipeline B-039. Every consumer subscribed at once reads one window, a later one reads it
    /// at once, and it is emptied when the last leaves. The hazard is Rx's <c>Replay(1)</c>, which
    /// hands the next first subscriber the window the last ones left.
    /// </summary>
    [Fact]
    public void GivenAConsumerAndThreeChanges_WhenASecondSubscribesAndBothLeave_ThenTheSecondReadsTheSameAndAThirdReadsEmpty()
    {
        // Given
        var scheduler = new TestScheduler();
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        FleetTracker sut = Tracker(cache, clock, scheduler);
        var first = new List<FleetNoticeWindow>();
        var firstSubscription = sut.Recent.Subscribe(first.Add);
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland"));
        cache.AddOrUpdate(Reporting("d4e5f6", "Testland"));
        cache.AddOrUpdate(Reporting("070809", "Testland"));
        var second = new List<FleetNoticeWindow>();

        // When
        var secondSubscription = sut.Recent.Subscribe(second.Add);
        var joined = second.ToArray();
        firstSubscription.Dispose();
        secondSubscription.Dispose();
        var third = new List<FleetNoticeWindow>();
        using var thirdSubscription = sut.Recent.Subscribe(third.Add);

        // Then
        joined.Should().ContainSingle().Which.Should().BeSameAs(first[^1], "every consumer reads the one window");
        joined[0].Notices.Should().HaveCount(3);
        third.Should().ContainSingle("the source's fleet is counted as it connects and not entered")
            .Which.Notices.Should().BeEmpty("the window went with its last consumer");
    }

    /// <summary>
    /// fleet-pipeline B-039. A silence enters nothing, and the changeset that ends it enters as an
    /// <see cref="FleetNoticeKind.Updated"/> with its own counts, though a consumer's notices call it
    /// resumed. The hazard is a window built from the notices, which would hold a quiet entry.
    /// </summary>
    [Fact]
    public void GivenAWindowOfTwo_WhenTheFeedFallsQuietAndResumes_ThenItHoldsThreeUpdatedNotices()
    {
        // Given
        var scheduler = new TestScheduler();
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var writer = (IObservedClockWriter) clock;
        FleetTracker sut = Tracker(cache, clock, scheduler);
        var raised = new List<FleetNotice>();
        using var notices = sut.Notices(Observable.Never<TimeSpan>()).Subscribe(raised.Add);
        var windows = new List<FleetNoticeWindow>();
        using var subscription = sut.Recent.Subscribe(windows.Add);
        writer.Observe(Observed);
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland"));
        cache.AddOrUpdate(Reporting("d4e5f6", "Testland"));

        // When
        writer.Observe(Observed + FleetTracker.DefaultStaleAfter + TimeSpan.FromSeconds(1));
        scheduler.AdvanceBy(FleetTracker.DefaultNoticeInterval.Ticks);
        cache.Edit(static updater =>
        {
            updater.AddOrUpdate(Reporting("a1b2c3", "Testland"));
            updater.Remove("d4e5f6");
        });
        scheduler.AdvanceBy(FleetTracker.DefaultNoticeInterval.Ticks);

        // Then
        var window = windows[^1];
        window.Notices.Should().HaveCount(3).And.OnlyContain(static notice => notice.Kind == FleetNoticeKind.Updated, "no quiet notice enters");
        window.Notices[2].Updated.Should().Be(1, "the resuming changeset enters with its counts");
        window.Notices[2].Removed.Should().Be(1);
        raised.Should().Contain(static notice => notice.Kind == FleetNoticeKind.Quiet, "the consumer's own notices did see the silence");
    }

    /// <summary>Builds the tracker over a cache, a clock and one scheduler in every position.</summary>
    /// <param name="cache">The store the seam reports from.</param>
    /// <param name="clock">The observed clock the window reads.</param>
    /// <param name="scheduler">Where a consumer's notices are paced.</param>
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

    /// <summary>One aircraft, reporting a country.</summary>
    /// <param name="key">The vehicle's key.</param>
    /// <param name="country">Its origin country.</param>
    /// <returns>The aircraft.</returns>
    private static Aircraft Reporting(string key, string country) =>
        new(key, Observed) { OriginCountry = country };

    /// <summary>The observed instant the vehicles report.</summary>
    private static readonly DateTimeOffset Observed = new(2026, 10, 5, 14, 32, 10, TimeSpan.Zero);
}
