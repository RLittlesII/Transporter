using System.Reactive.Subjects;
using AwesomeAssertions;
using DynamicData;
using NSubstitute;
using Transponder.Model;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;
using Transponder.Tracking.Sources;

namespace Transponder.UnitTests.Tracking;

// RSA1010 asks for ObserveOn before every Bind. These bind without one deliberately: the
// pipeline marshals for nobody (fleet-pipeline B-005) and the user-interface scheduler belongs to
// the consumer (§ 5 row 9). The consumer here is a test, which has no user-interface thread.
#pragma warning disable RSA1010

public class StalenessTests
{
    /// <summary>
    /// fleet-pipeline B-016. A vehicle silent past the threshold is reported stale and stays in the
    /// fleet, keyed and present in a collection bound from it. A row that vanished would read as a
    /// bug to the audience; a row marked stale reads as information, which is the demo beat.
    /// </summary>
    [Fact]
    public void GivenAVehicleSilentPastTheThreshold_WhenTheFleetIsRead_ThenItIsMarkedStaleAndStillPresent()
    {
        // Given
        var cache = Cache();
        var clock = new ObservedClock();
        var sut = Tracker(cache, clock);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();

        // When
        Observe(clock, LastContact + TimeSpan.FromMinutes(6));
        cache.AddOrUpdate(Silent("a1b2c3"));

        // Then
        rows.Should().ContainSingle().Which.IsStale.Should().BeTrue("six minutes of silence is past the five the tracker allows");
        rows.Should().ContainSingle().Which.Vehicle.Key.Should().Be("a1b2c3", "it is marked, not removed");
    }

    /// <summary>
    /// fleet-pipeline B-017. The threshold is the tracker's own five minutes until a caller says
    /// otherwise: four minutes of silence is tolerated and six is not, with nothing configured. A
    /// default held by each caller instead would make an unconfigured tracker mark everything or
    /// nothing, and no test of the pipeline could catch it.
    /// </summary>
    [Fact]
    public void GivenNoConfiguredThreshold_WhenStalenessIsEvaluated_ThenItIsFiveMinutes()
    {
        // Given
        var cache = Cache();
        var clock = new ObservedClock();
        var sut = Tracker(cache, clock);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        Observe(clock, LastContact + TimeSpan.FromMinutes(4));
        cache.AddOrUpdate(Silent("a1b2c3"));

        // When
        var tolerated = rows.Single().IsStale;
        Observe(clock, LastContact + TimeSpan.FromMinutes(6));

        // Then
        tolerated.Should().BeFalse("four minutes is inside the default five");
        rows.Single().IsStale.Should().BeTrue("six minutes is past it");
    }

    /// <summary>
    /// fleet-pipeline B-018. Time passing is enough: the instant advances past the threshold, nothing
    /// new arrives for the vehicle, and its mark changes. Without the clock's own stream a mark can
    /// only change when a changeset arrives, so a feed that stopped would leave every row looking
    /// live — which is the one case the absence of data is not information.
    /// </summary>
    [Fact]
    public void GivenNoNewDataForAVehicle_WhenTheObservedInstantAdvancesPastTheThreshold_ThenItBecomesStale()
    {
        // Given
        var cache = Cache();
        var clock = new ObservedClock();
        var sut = Tracker(cache, clock);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        Observe(clock, LastContact);
        cache.AddOrUpdate(Silent("a1b2c3"));
        rows.Single().IsStale.Should().BeFalse("the vehicle reported at the instant the clock observed");

        // When
        Observe(clock, LastContact + TimeSpan.FromMinutes(6));

        // Then
        rows.Single().IsStale.Should().BeTrue("no changeset arrived; the clock moved, and that is enough");
        rows.Should().ContainSingle("a re-derivation is not a remove and an add");
    }

    /// <summary>
    /// fleet-pipeline B-019. An hour of silence removes nothing: the fleet still carries the vehicle
    /// and no change of any kind says Remove. Expiry is the vessel treatment and belongs to the
    /// closing act's specification, where going silent means gone rather than quiet (§ 4 row 6).
    /// </summary>
    [Fact]
    public void GivenAVehicleSilentForAnHour_WhenTheFleetIsRead_ThenNothingWasRemoved()
    {
        // Given
        var cache = Cache();
        var clock = new ObservedClock();
        var sut = Tracker(cache, clock);
        var observed = new List<IChangeSet<TrackedVehicle, string>>();
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe(observed.Add);
        Observe(clock, LastContact);
        cache.AddOrUpdate(Silent("a1b2c3"));

        // When
        Observe(clock, LastContact + TimeSpan.FromHours(1));

        // Then
        rows.Should().ContainSingle().Which.IsStale.Should().BeTrue();
        observed.SelectMany(static changes => changes).Should().NotContain(static change => change.Reason == ChangeReason.Remove);
    }

    /// <summary>A cache of domain vehicles, keyed the way the seam keys its changesets.</summary>
    /// <returns>A store a test edits to make the seam report.</returns>
    private static SourceCache<TransportVehicle, string> Cache() => new(static vehicle => vehicle.Key);

    /// <summary>A tracker whose clock and whose ticks are the one clock object, as the container registers them.</summary>
    /// <param name="cache">The store the seam reports from.</param>
    /// <param name="clock">The clock the test advances.</param>
    /// <returns>The system under test.</returns>
    private static FleetTracker Tracker(SourceCache<TransportVehicle, string> cache, ObservedClock clock)
    {
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());

        return new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock);
    }

    /// <summary>Reports an instant the way an envelope does, which is the only way time moves here.</summary>
    /// <param name="clock">The clock to advance.</param>
    /// <param name="instant">The instant the provider reported.</param>
    private static void Observe(ObservedClock clock, DateTimeOffset instant) =>
        ((IObservedClockWriter) clock).Observe(instant);

    /// <summary>An aircraft that has reported nothing since <see cref="LastContact"/>.</summary>
    /// <param name="key">The <c>icao24</c> in lowercase hex.</param>
    /// <returns>The vehicle a test puts into the store.</returns>
    private static TransportVehicle Silent(string key) => new Aircraft(key, LastContact);

    /// <summary>The instant every vehicle here was last heard from.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}

#pragma warning restore RSA1010
