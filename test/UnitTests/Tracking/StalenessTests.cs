using System.Reactive.Subjects;
using AwesomeAssertions;
using DynamicData;
using NSubstitute;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using Transporter.Tracking.Sources;

namespace Transporter.UnitTests.Tracking;

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
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();

        // When
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(6));
        cache.AddOrUpdate(new Aircraft("a1b2c3", LastContact));

        // Then
        rows.Should().ContainSingle().Which.IsStale.Should().BeTrue("six minutes of silence is past the five the tracker allows");
        rows.Should().ContainSingle().Which.Vehicle.Key.Should().Be("a1b2c3", "it is marked, not removed");
    }

    /// <summary>
    /// fleet-pipeline B-017. The threshold is the tracker's own five minutes until a caller says
    /// otherwise, and the cases bracket it: four minutes tolerated, five tolerated because the claim
    /// is "longer than", six and an hour marked. A default held by each caller instead would make an
    /// unconfigured tracker mark everything or nothing, and no test of the pipeline could catch it.
    /// </summary>
    /// <param name="silent">How many minutes the vehicle has been silent.</param>
    /// <param name="marked">Whether the default threshold marks it.</param>
    [Theory]
    [ClassData(typeof(DefaultThresholdCases))]
    public void GivenNoConfiguredThreshold_WhenStalenessIsEvaluated_ThenItIsFiveMinutes(int silent, bool marked)
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        cache.AddOrUpdate(new Aircraft("a1b2c3", LastContact));

        // When
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(silent));

        // Then
        rows.Single().IsStale.Should().Be(marked);
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
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        ((IObservedClockWriter) clock).Observe(LastContact);
        cache.AddOrUpdate(new Aircraft("a1b2c3", LastContact));
        rows.Single().IsStale.Should().BeFalse("the vehicle reported at the instant the clock observed");

        // When
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(6));

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
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock);
        var observed = new List<IChangeSet<TrackedVehicle, string>>();
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe(observed.Add);
        ((IObservedClockWriter) clock).Observe(LastContact);
        cache.AddOrUpdate(new Aircraft("a1b2c3", LastContact));

        // When
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromHours(1));

        // Then
        rows.Should().ContainSingle().Which.IsStale.Should().BeTrue();
        observed.SelectMany(static changes => changes).Should().NotContain(static change => change.Reason == ChangeReason.Remove);
    }

    /// <summary>The instant every vehicle here was last heard from.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}

#pragma warning restore RSA1010
