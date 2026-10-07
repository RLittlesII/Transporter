using System.Reactive.Linq;
using AwesomeAssertions;
using DynamicData;
using Transponder.Model;
using Transponder.Tracking;

namespace Transponder.UnitTests.Tracking;

// RSA1010 asks for ObserveOn before every Bind. These bind without one deliberately: the
// pipeline marshals for nobody (fleet-pipeline B-005) and the user-interface scheduler belongs to
// the consumer (§ 5 row 9). The consumer here is a test, which has no user-interface thread.
#pragma warning disable RSA1010

public class FleetFilterTests
{
    /// <summary>
    /// fleet-pipeline B-006. A predicate handed to the tracker re-evaluates the vehicles already
    /// there: the rows a consumer holds change and the seam is not asked for them again. The
    /// assertion that matters beside the count is the connection count — a pipeline that refetched
    /// would produce the same three rows and spend a poll doing it, which on stage is a visible
    /// pause and off stage is a credit.
    /// </summary>
    /// <remarks>
    /// The predicate reads <see cref="TransportVehicle.Position"/> rather than an aircraft's own
    /// <c>OnGround</c>: B-022 forbids a predicate reaching a concrete subclass, so "airborne" is
    /// modelled here as the base fact the pipeline can filter on — a reported fix.
    /// </remarks>
    [Fact]
    public void GivenABoundFleet_WhenANewPredicateArrives_ThenTheVisibleRowsChangeAndTheSourceIsNotResubscribed()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = new CountingTrackerSource(cache);
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        cache.AddOrUpdate(Airborne("a1b2c3"));
        cache.AddOrUpdate(Airborne("d4e5f6"));
        cache.AddOrUpdate(Airborne("070809"));
        cache.AddOrUpdate(Grounded("b1c2d3"));
        cache.AddOrUpdate(Grounded("e4f5a6"));

        // When
        sut.Filter(static vehicle => vehicle.Position.IsSome);

        // Then
        rows.Should().HaveCount(3, "two of the five report no fix and the predicate excludes them");
        cache.Count.Should().Be(5, "filtering hides rows and removes nothing from the source");
        source.Connections.Should().Be(1, "a new predicate re-evaluates what is held and refetches nothing");
    }

    /// <summary>
    /// fleet-pipeline B-007. Nobody has called <c>Filter</c>, and every vehicle is visible: the
    /// tracker holds that default itself rather than waiting for a caller to supply one. A pipeline
    /// that waited would show an empty grid at startup, which reads as a broken feed — and with the
    /// default held by every caller instead, no test of the pipeline could catch the one that
    /// forgot.
    /// </summary>
    [Fact]
    public void GivenNoPredicateHasArrived_WhenVehiclesAreReported_ThenEveryVehicleIsVisible()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        FleetTracker sut = new FleetTrackerFixture().WithSource(new CountingTrackerSource(cache));
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();

        // When
        cache.AddOrUpdate(Airborne("a1b2c3"));
        cache.AddOrUpdate(Airborne("d4e5f6"));
        cache.AddOrUpdate(Airborne("070809"));
        cache.AddOrUpdate(Grounded("b1c2d3"));
        cache.AddOrUpdate(Grounded("e4f5a6"));
        cache.AddOrUpdate(Grounded("c7d8e9"));

        // Then
        rows.Should().HaveCount(6, "an absent filter is not an empty fleet");
        FleetTracker.DefaultPredicate(Grounded("f1a2b3")).Should().BeTrue("the default the tracker holds is everything");
    }

    /// <summary>One aircraft reporting a fix, which is what the predicate here selects on.</summary>
    /// <param name="key">The vehicle's key.</param>
    /// <returns>The aircraft.</returns>
    private static Aircraft Airborne(string key) =>
        new(key, LastContact) { Callsign = "TRN0001", OriginCountry = "Testland", Position = new GeoPosition(29.76, -95.37) };

    /// <summary>One aircraft reporting no fix.</summary>
    /// <param name="key">The vehicle's key.</param>
    /// <returns>The aircraft.</returns>
    private static Aircraft Grounded(string key) =>
        new(key, LastContact) { Callsign = "TRN0002", OriginCountry = "Testland" };

    /// <summary>The instant every vehicle here was last heard from.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}
