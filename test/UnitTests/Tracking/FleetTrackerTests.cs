using System.Reactive.Linq;
using System.Reactive.Subjects;
using AwesomeAssertions;
using DynamicData;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Model;
using Transponder.Tracking;

namespace Transponder.UnitTests.Tracking;

public class FleetTrackerTests
{
    /// <summary>
    /// B-042. The chain is assembled in the constructor and a swap happens below the seam, so two
    /// swaps connect the seam no more often than none do and the published stream is still the one
    /// built at construction. Two subscribers share that one connection, so a tracker that rebuilt
    /// its pipeline — per swap or per reader — would connect again, which is the leak that reads as
    /// a memory bug on a projector.
    /// </summary>
    [Fact]
    public void GivenASwapFollowedByASecondSwap_WhenEachCompletes_ThenThePipelineIsTheOneBuiltAtConstruction()
    {
        // Given
        var live = new BehaviorSubject<SourceCache<TransportVehicle, string>>(Cache());
        var source = Swapping(live);
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        var fleet = sut.Fleet;
        var observed = new List<IChangeSet<TrackedVehicle, string>>();
        using var subscription = fleet.Subscribe(observed.Add);
        using var second = fleet.Subscribe();

        // When
        live.OnNext(Cache());
        var third = Cache();
        live.OnNext(third);
        third.AddOrUpdate(Silent("a1b2c3"));

        // Then
        source.Received(1).Connect();
        sut.Fleet.Should().BeSameAs(fleet, "the stream is built once and handed out, not rebuilt per reader");
        observed.Should().ContainSingle().Which.Should().ContainSingle().Which.Key.Should().Be("a1b2c3");
    }

    /// <summary>
    /// B-043. The same vehicle, silent since the same instant, is not stale while the injected clock
    /// has observed nothing and is stale once that clock reports six minutes later — so the mark is
    /// derived from last contact against the clock it was given. The first half is what an ambient
    /// <c>DateTime.UtcNow</c> could not pass: the wall clock is hours past this vehicle's last
    /// contact, and a tracker reading it would call the first arrival stale.
    /// </summary>
    [Fact]
    public void GivenAnInjectedClockAdvancedPastTheThreshold_WhenStalenessIsRead_ThenItDerivesFromLastContactAndNoAmbientClockIsRead()
    {
        // Given
        var cache = Cache();
        var clock = new ObservedClock();
        FleetTracker sut = new FleetTrackerFixture().WithSource(Over(cache)).WithClock(clock);
        var observed = new List<IChangeSet<TrackedVehicle, string>>();
        using var subscription = sut.Fleet.Subscribe(observed.Add);
        cache.AddOrUpdate(Silent("a1b2c3"));

        // When
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(6));
        cache.AddOrUpdate(Silent("d4e5f6"));

        // Then
        Marks(observed, "a1b2c3").Should().BeFalse("the clock had observed no instant, and a wall-clock read would have made a past contact stale");
        Marks(observed, "d4e5f6").Should().BeTrue("the clock now reports six minutes past a contact the default threshold allows five");
    }

    /// <summary>
    /// B-051. A vehicle past the threshold stays in the stream carrying the mark, and no change
    /// removes it: a row vanishing reads as a bug and a flagged row reads as information. The
    /// threshold is the tracker's own five minutes until a caller says otherwise, and raising it to
    /// ten un-marks the same vehicle rather than rebuilding anything.
    /// </summary>
    [Fact]
    public void GivenAVehiclePastTheConfiguredThreshold_WhenTheCollectionIsRead_ThenItIsPresentAndObservablyStale()
    {
        // Given
        var cache = Cache();
        var clock = new ObservedClock();
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(6));
        FleetTracker sut = new FleetTrackerFixture().WithSource(Over(cache)).WithClock(clock);
        var observed = new List<IChangeSet<TrackedVehicle, string>>();
        using var subscription = sut.Fleet.Subscribe(observed.Add);

        // When
        cache.AddOrUpdate(Silent("a1b2c3"));

        // Then
        Marks(observed, "a1b2c3").Should().BeTrue("five minutes is the default the tracker holds, and the clock is six past");
        observed.SelectMany(static changes => changes).Should().NotContain(static change => change.Reason == ChangeReason.Remove);

        // And the threshold is configurable, which re-derives the mark for what is already there
        sut.StaleAfter(TimeSpan.FromMinutes(10));
        Marks(observed, "a1b2c3").Should().BeFalse("ten minutes tolerates a six-minute silence");
        observed.SelectMany(static changes => changes).Should().NotContain(static change => change.Reason == ChangeReason.Remove);
    }

    /// <summary>A cache of domain vehicles, keyed the way the seam keys its changesets.</summary>
    /// <returns>A store a test edits to make the seam report.</returns>
    private static SourceCache<TransportVehicle, string> Cache() => new(static vehicle => vehicle.Key);

    /// <summary>A seam over one store.</summary>
    /// <param name="cache">The store the seam reports from.</param>
    /// <returns>The substituted seam.</returns>
    private static ITrackerSource Over(SourceCache<TransportVehicle, string> cache)
    {
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        source.ClearReceivedCalls();

        return source;
    }

    /// <summary>A seam whose live store changes, which is what a swap looks like from above it.</summary>
    /// <param name="live">The store that is live, and every store that replaces it.</param>
    /// <returns>The substituted seam.</returns>
    private static ITrackerSource Swapping(IObservable<SourceCache<TransportVehicle, string>> live)
    {
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(live.Select(static cache => cache.Connect()).Switch());
        source.ClearReceivedCalls();

        return source;
    }

    /// <summary>An aircraft that has reported nothing since <see cref="LastContact"/>.</summary>
    /// <param name="key">The <c>icao24</c> in lowercase hex.</param>
    /// <returns>The vehicle a test puts into the store.</returns>
    private static TransportVehicle Silent(string key) => new Aircraft(key, LastContact);

    /// <summary>The mark the pipeline last published for one vehicle.</summary>
    /// <param name="observed">Every changeset the subscription has seen.</param>
    /// <param name="key">The vehicle to read.</param>
    /// <returns>Whether that vehicle was stale when the pipeline last evaluated it.</returns>
    private static bool Marks(IEnumerable<IChangeSet<TrackedVehicle, string>> observed, string key) =>
        observed
            .SelectMany(static changes => changes)
            .Where(change => change.Key == key)
            .Select(static change => change.Current.IsStale)
            .Last();

    /// <summary>The instant every vehicle here was last heard from, and the only one the clock is told about.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}

/// <summary>Builds the tracker, so a constructor change edits this and not every test.</summary>
[AutoFixture(typeof(FleetTracker))]
internal partial class FleetTrackerFixture
{
    public FleetTrackerFixture()
    {
        WithSource(Substitute.For<ITrackerSource>());
        WithClock(new ObservedClock());
    }
}
