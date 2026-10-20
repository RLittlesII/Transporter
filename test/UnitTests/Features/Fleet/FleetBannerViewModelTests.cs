using System.Reactive.Linq;
using System.Reactive.Subjects;
using AwesomeAssertions;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Features.Fleet.ViewModels;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Features.Fleet;

public class FleetBannerViewModelTests
{
    /// <summary>
    /// B-023. The banner shows the notice the tracker raised — its instant, the tracked count and the
    /// three counts — as that one value. The hazard is a view model that recounts from the bound
    /// collection, which agrees until a filter hides a vehicle.
    /// </summary>
    [Fact]
    public void GivenANotice_WhenTheBannerIsProjected_ThenItShowsTheInstantAndTheCountsAndComputesNone()
    {
        // Given
        var scheduler = new TestScheduler();
        var notice = Notice(FleetNoticeKind.Updated);
        var tracker = Tracker(Observable.Return(notice), Observable.Never<FleetNotice>());
        using FleetBannerViewModel sut = new FleetBannerViewModelFixture().WithTracker(tracker).WithProvider(Provider(scheduler));

        // When
        scheduler.Start();

        // Then
        sut.Latest.Should().BeSameAs(notice, "the banner shows the tracker's value, not one it built");
        _ = tracker.DidNotReceive().Fleet;
        _ = tracker.DidNotReceive().Summary;
    }

    /// <summary>
    /// B-024. Nothing arriving leaves the banner as it was. The hazard is a banner driven by a timer
    /// or by the clock, which would redraw on every tick with nothing new to say.
    /// </summary>
    [Fact]
    public void GivenNoNoticeArrives_WhenTheBannerIsRead_ThenItIsUnchanged()
    {
        // Given
        var scheduler = new TestScheduler();
        var notice = Notice(FleetNoticeKind.Updated);
        var tracker = Tracker(Observable.Return(notice).Concat(Observable.Never<FleetNotice>()), Observable.Never<FleetNotice>());
        using FleetBannerViewModel sut = new FleetBannerViewModelFixture().WithTracker(tracker).WithProvider(Provider(scheduler));
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        // When
        scheduler.AdvanceBy(TimeSpan.FromMinutes(10).Ticks);

        // Then
        sut.Latest.Should().BeSameAs(notice);
        changed.Should().BeEmpty("no notice arrived, so nothing changed");
    }

    /// <summary>
    /// B-025. Only a quiet and a resumed notice reach the toast, at any interval. The hazard is a
    /// toast per update, which at a fifteen-second poll interrupts the talk a hundred and eighty times.
    /// </summary>
    [Fact]
    public void GivenAnUpdatedAQuietAndAResumedNotice_WhenTheInterruptingOnesAreObserved_ThenOnlyTheQuietAndResumedAppear()
    {
        // Given
        var scheduler = new TestScheduler();
        var toast = new Subject<FleetNotice>();
        var tracker = Tracker(Observable.Never<FleetNotice>(), toast);
        using FleetBannerViewModel sut = new FleetBannerViewModelFixture().WithTracker(tracker).WithProvider(Provider(scheduler));
        var interrupting = new List<FleetNoticeKind>();
        using var subscription = sut.Interrupting.Subscribe(notice => interrupting.Add(notice.Kind));

        // When
        toast.OnNext(Notice(FleetNoticeKind.Updated));
        toast.OnNext(Notice(FleetNoticeKind.Quiet));
        toast.OnNext(Notice(FleetNoticeKind.Resumed));
        scheduler.Start();

        // Then
        interrupting.Should().Equal(FleetNoticeKind.Quiet, FleetNoticeKind.Resumed);
    }

    /// <summary>
    /// B-026. Each surface hands the tracker its own interval, and editing one while running reaches
    /// only that surface's pacing. The hazard is one interval shared by both, so slowing the toast
    /// slows the banner too.
    /// </summary>
    [Fact]
    public void GivenTwoIntervalsEditedWhileRunning_WhenNoticesArrive_ThenEachSurfaceIsPacedByItsOwn()
    {
        // Given
        var scheduler = new TestScheduler();
        var paces = new List<IObservable<TimeSpan>>();
        var banner = new Subject<FleetNotice>();
        var toast = new Subject<FleetNotice>();
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Notices(Arg.Do<IObservable<TimeSpan>>(paces.Add)).Returns(banner, toast);
        tracker.Recent.Returns(Observable.Never<FleetNoticeWindow>());
        using FleetBannerViewModel sut = new FleetBannerViewModelFixture().WithTracker(tracker).WithProvider(Provider(scheduler));
        var bannerPace = new List<TimeSpan>();
        var toastPace = new List<TimeSpan>();
        using var first = paces[0].Subscribe(bannerPace.Add);
        using var second = paces[1].Subscribe(toastPace.Add);

        // When
        sut.BannerInterval = TimeSpan.FromSeconds(10);
        sut.ToastInterval = TimeSpan.FromSeconds(1);
        var notice = Notice(FleetNoticeKind.Quiet);
        banner.OnNext(notice);
        scheduler.Start();

        // Then
        bannerPace.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
        toastPace.Should().Equal(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1));
        sut.Latest.Should().BeSameAs(notice, "the banner reads its own subscription, not the toast's");
    }

    /// <summary>
    /// B-037. The banner shows the window's totals as the tracker published them. The hazard is a
    /// banner that sums the window's notices itself, which this window's totals tell apart.
    /// </summary>
    [Fact]
    public void GivenAWindowWhoseTotalsAreNotItsNoticesSum_WhenTheBannerIsProjected_ThenItShowsTheWindowsTotals()
    {
        // Given
        var scheduler = new TestScheduler();
        var window = new FleetNoticeWindow { Notices = [Notice(FleetNoticeKind.Updated)], Added = 7, Updated = 11, Removed = 13 };
        var tracker = Tracker(Observable.Never<FleetNotice>(), Observable.Never<FleetNotice>());
        tracker.Recent.Returns(Observable.Return(window));
        using FleetBannerViewModel sut = new FleetBannerViewModelFixture().WithTracker(tracker).WithProvider(Provider(scheduler));

        // When
        scheduler.Start();

        // Then
        (sut.Window.Added, sut.Window.Updated, sut.Window.Removed).Should().Be((7, 11, 13));
    }

    /// <summary>A tracker whose first notices subscription is the banner's and whose second is the toast's.</summary>
    /// <param name="banner">What the banner's subscription delivers.</param>
    /// <param name="toast">What the toast's subscription delivers.</param>
    /// <returns>The substitute.</returns>
    private static IFleetTracker Tracker(IObservable<FleetNotice> banner, IObservable<FleetNotice> toast)
    {
        var tracker = Substitute.For<IFleetTracker>();
        tracker.Notices(Arg.Any<IObservable<TimeSpan>>()).Returns(banner, toast);
        tracker.Recent.Returns(Observable.Never<FleetNoticeWindow>());

        return tracker;
    }

    /// <summary>One scheduler in both positions.</summary>
    /// <param name="scheduler">The scheduler the test advances.</param>
    /// <returns>The provider.</returns>
    private static SchedulerProvider Provider(TestScheduler scheduler) =>
        new SchedulerProviderFixture().WithTestScheduler(scheduler);

    /// <summary>A notice of a kind, with counts no view model could derive from an empty fleet.</summary>
    /// <param name="kind">What the notice is about.</param>
    /// <returns>The notice.</returns>
    private static FleetNotice Notice(FleetNoticeKind kind) =>
        new()
        {
            Kind = kind,
            Instant = new DateTimeOffset(2026, 10, 5, 14, 32, 10, TimeSpan.Zero),
            Tracked = 37,
            Added = 1,
            Updated = 2,
            Removed = 1,
        };
}

/// <summary>Builds the banner view model, so a constructor change edits this fixture rather than every test.</summary>
[AutoFixture(typeof(FleetBannerViewModel))]
internal partial class FleetBannerViewModelFixture;
