using System.Reactive.Linq;
using System.Reactive.Subjects;
using AwesomeAssertions;
using DynamicData;
using LanguageExt;
using NSubstitute;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.UnitTests.Model.Fixtures;

namespace Transporter.UnitTests.Tracking;

// RSA1010 asks for ObserveOn before every Bind. These bind without one deliberately: the
// pipeline marshals for nobody (fleet-pipeline B-005) and the user-interface scheduler belongs to
// the consumer (§ 5 row 9). The consumer here is a test, which has no user-interface thread.
#pragma warning disable RSA1010

public class FleetMovementTests
{
    /// <summary>
    /// fleet-pipeline B-032. An update that moves a vehicle a tenth of a degree north puts the leg it
    /// flew on its element, so a card shows the distance without keeping the previous vehicle.
    /// </summary>
    [Fact]
    public void GivenABoundFleet_WhenAnUpdateMovesAVehicle_ThenItsElementCarriesTheLegItFlew()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70, -95.40)));

        // When
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.80, -95.40)));

        // Then
        rows.Single().Leg.IfNone(double.NaN).Should().BeApproximately(11_119.5, 1);
    }

    /// <summary>
    /// fleet-pipeline B-032. A vehicle with no position before an update has flown no leg, and
    /// neither has a vehicle just added in the same changeset: none, not zero, because a zero reads
    /// as an aircraft that stood still and would be summed into its total.
    /// </summary>
    [Fact]
    public void GivenAVehicleWithNoPosition_WhenAnUpdateGivesItOne_ThenItsElementCarriesNoLeg()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        cache.AddOrUpdate(new AircraftFixture().WithKey("SYN102"));

        // When
        cache.Edit(static updater =>
        {
            updater.AddOrUpdate(new AircraftFixture().WithKey("SYN102").WithPosition(new GeoPosition(29.70, -95.40)));
            updater.AddOrUpdate(new AircraftFixture().WithKey("SYN103").WithPosition(new GeoPosition(29.80, -95.40)));
        });

        // Then
        rows.Single(static row => row.Vehicle.Key == "SYN102").Leg.IsNone.Should().BeTrue("there was no position to fly from");
        rows.Single(static row => row.Vehicle.Key == "SYN103").Leg.IsNone.Should().BeTrue("a vehicle just added has flown nothing yet");
    }

    /// <summary>
    /// fleet-pipeline B-032. The test the stage's place in the chain exists for: the clock advances
    /// with no changeset, the stale mark is derived again, and the movement is left alone. Movement
    /// derived in the mark's forced transform would read a zero leg here and a replaced vehicle that
    /// is the vehicle itself, and no other test would notice.
    /// </summary>
    [Fact]
    public void GivenAVehicleThatMoved_WhenTheObservedInstantAdvances_ThenItsLegIsUnchanged()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock);
        var heard = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        ((IObservedClockWriter) clock).Observe(heard);
        Aircraft departed = new AircraftFixture().WithLastContact(heard).WithPosition(new GeoPosition(29.70, -95.40));
        cache.AddOrUpdate(departed);
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard).WithPosition(new GeoPosition(29.80, -95.40)));

        // When
        ((IObservedClockWriter) clock).Observe(heard + TimeSpan.FromMinutes(6));

        // Then
        var row = rows.Single();
        row.IsStale.Should().BeTrue("the tick re-derived the mark");
        row.Leg.IfNone(double.NaN).Should().BeApproximately(11_119.5, 1, "a tick is not a move");
        row.Replaced.IfNone(row.Vehicle).Should().BeSameAs(departed, "the vehicle the update replaced, not the vehicle itself");
        row.Travelled.Should().BeApproximately(11_119.5, 1);
    }

    /// <summary>
    /// fleet-pipeline B-032. Present to absent is no leg, not a leg to 0, 0 — the Gulf of Guinea is
    /// a real place — and the total stands at what the vehicle flew while it had a position.
    /// </summary>
    [Fact]
    public void GivenAVehicleThatLosesItsPosition_WhenItIsUpdated_ThenItsElementCarriesNoLegAndItsTotalStands()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70, -95.40)));
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.80, -95.40)));

        // When
        cache.AddOrUpdate(new AircraftFixture());

        // Then
        rows.Single().Leg.IsNone.Should().BeTrue("there is no position to fly to");
        rows.Single().Travelled.Should().BeApproximately(11_119.5, 1, "the leg it did fly still counts");
    }

    /// <summary>
    /// fleet-pipeline B-033. Three legs north, a tenth of a degree each, add up on the element: the
    /// total is the last element's total plus this leg, which is the fold a transform cannot make.
    /// </summary>
    [Fact]
    public void GivenAVehicleThatFlewThreeLegs_WhenTheFleetIsRead_ThenItsTravelledIsTheirSum()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70, -95.40)));

        // When
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.80, -95.40)));
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.90, -95.40)));
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(30.00, -95.40)));

        // Then
        rows.Single().Travelled.Should().BeApproximately(33_358.5, 1);
    }

    /// <summary>
    /// fleet-pipeline B-033. A vehicle removed and reported again starts a new total, and enters
    /// with no leg and nothing replaced: the claim's own reset.
    /// </summary>
    [Fact]
    public void GivenAVehicleRemovedAndReported_WhenItReenters_ThenItsTravelledStartsAtZero()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        cache.AddOrUpdate(new AircraftFixture().WithKey("SYN101").WithPosition(new GeoPosition(29.70, -95.40)));
        cache.AddOrUpdate(new AircraftFixture().WithKey("SYN101").WithPosition(new GeoPosition(29.80, -95.40)));
        cache.RemoveKey("SYN101");

        // When
        cache.AddOrUpdate(new AircraftFixture().WithKey("SYN101").WithPosition(new GeoPosition(29.90, -95.40)));

        // Then
        var row = rows.Single();
        row.Travelled.Should().Be(0);
        row.Leg.IsNone.Should().BeTrue();
        row.Replaced.IsNone.Should().BeTrue();
    }

    /// <summary>
    /// fleet-pipeline B-033. A vehicle filtered out still moves, and its total kept counting while
    /// it was hidden: the total is a fact about the vehicle, not about the view. Derived below the
    /// filter, it would restart when the predicate let the vehicle back in as an add.
    /// </summary>
    [Fact]
    public void GivenAVehicleFilteredOutWhileItMoves_WhenItIsFilteredBackIn_ThenItsTravelledIncludesTheHiddenLegs()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        cache.AddOrUpdate(new AircraftFixture().WithKey("SYN101").WithPosition(new GeoPosition(29.70, -95.40)));
        cache.AddOrUpdate(new AircraftFixture().WithKey("SYN101").WithPosition(new GeoPosition(29.80, -95.40)));
        sut.Filter(static vehicle => vehicle.Key != "SYN101");
        cache.AddOrUpdate(new AircraftFixture().WithKey("SYN101").WithPosition(new GeoPosition(29.90, -95.40)));
        rows.Should().BeEmpty("the vehicle is hidden while it flies the second leg");

        // When
        sut.Filter(static _ => true);

        // Then
        rows.Single().Travelled.Should().BeApproximately(22_239.0, 1);
    }

    /// <summary>
    /// fleet-pipeline B-033. A swap draws no leg from the outgoing source's position to the
    /// incoming one's. The arrangement is B-001's — a substitute seam whose connection is
    /// DynamicData's <c>Switch</c> over two caches reporting one key at two positions — because the
    /// operator under test is the one the swap decorator uses, and B-023 forbids this Feature's
    /// tests naming a concrete source as much as its code.
    /// </summary>
    [Fact]
    public void GivenTwoStrategies_WhenTheLiveOneIsSwapped_ThenNoLegIsDrawnAcrossTheSwap()
    {
        // Given
        var live = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var replay = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        live.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70, -95.40)));
        live.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.80, -95.40)));
        replay.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(30.50, -95.00)));
        var selected = new BehaviorSubject<SourceCache<TransportVehicle, string>>(live);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(selected.Select(static cache => cache.Connect()).Switch());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        live.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.90, -95.40)));
        rows.Single().Travelled.Should().BeApproximately(11_119.5, 1, "one leg flown on the live source after binding");

        // When
        selected.OnNext(replay);

        // Then
        var row = rows.Single();
        row.Leg.IsNone.Should().BeTrue("no leg is drawn from a live position to a recorded one");
        row.Travelled.Should().Be(0);
    }

    /// <summary>
    /// fleet-pipeline B-041. After two updates the element carries the vehicle the last one
    /// replaced, by reference, so a readout shows what changed without keeping a copy.
    /// </summary>
    [Fact]
    public void GivenAVehicleUpdatedTwice_WhenItsElementIsRead_ThenItCarriesOnlyTheVehicleTheLastUpdateReplaced()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        Aircraft second = new AircraftFixture().WithPosition(new GeoPosition(29.80, -95.40));
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70, -95.40)));
        cache.AddOrUpdate(second);

        // When
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.90, -95.40)));

        // Then
        rows.Single().Replaced.IfNone(rows.Single().Vehicle).Should().BeSameAs(second);
    }

    /// <summary>fleet-pipeline B-041. A vehicle just added replaced nothing.</summary>
    [Fact]
    public void GivenAVehicleJustAdded_WhenItsElementIsRead_ThenItCarriesNoReplacedVehicle()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();

        // When
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70, -95.40)));

        // Then
        rows.Single().Replaced.IsNone.Should().BeTrue();
    }
}
