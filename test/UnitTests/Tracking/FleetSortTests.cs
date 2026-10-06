using System.Collections.Generic;
using System.Reactive.Linq;
using AwesomeAssertions;
using DynamicData;
using LanguageExt;
using NSubstitute;
using Transponder.Model;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;
using Transponder.Tracking.Sources;

namespace Transponder.UnitTests.Tracking;

public class FleetSortTests
{
    /// <summary>
    /// fleet-pipeline B-009. A comparer handed to the tracker reorders the rows a consumer already
    /// holds: the same item instances in a new order, nothing re-fetched, no stage rebuilt and the
    /// seam still connected once. A pipeline that rebuilt its chain for a new comparer would connect
    /// again and project new instances, which is what the instance assertion catches.
    /// </summary>
    /// <remarks>
    /// The bound collection raises one <c>Reset</c> for a comparer change, which is DynamicData's own
    /// notification for a re-sort rather than evidence of a refill — the instances are the ones the
    /// test already held. B-009's wording was corrected on 2026-10-06 to claim what the pipeline owes
    /// rather than which notification the binding adapter picks.
    /// </remarks>
    [Fact]
    public void GivenABoundFleet_WhenANewComparerArrives_ThenTheRowsReorderWithoutBeingRefetchedOrRebuilt()
    {
        // Given
        var cache = Cache();
        var source = new CountingTrackerSource(cache);
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.SortAndBind(out var rows, sut.Order).Subscribe();
        cache.AddOrUpdate(Aircraft("a1b2c3", "ALFA", "Mexico"));
        cache.AddOrUpdate(Aircraft("d4e5f6", "ZULU", "Canada"));
        rows.Select(static row => row.Vehicle.Key).Should().Equal(["a1b2c3", "d4e5f6"]);
        var held = rows.ToList();

        // When
        sut.SortBy(ByCountry);

        // Then
        rows.Select(static row => row.Vehicle.Key).Should().Equal(["d4e5f6", "a1b2c3"]);
        rows.Should().HaveCount(2, "nothing was added or dropped by the sort");
        rows.Should().OnlyContain(
            row => held.Any(instance => ReferenceEquals(instance, row)),
            "the rows are the instances the consumer already held, reordered");
        source.Connections.Should().Be(1, "a new comparer re-sorts what is bound and re-subscribes to nothing");
    }

    /// <summary>
    /// fleet-pipeline B-010. Every comparer the description offers compares members the abstract
    /// vehicle carries, so handing it a vehicle of another source's type orders it rather than
    /// throwing. A comparer that reached for an <c>Aircraft</c> member — the downcast ADR-0005 item 6
    /// forbids and the description exists to replace — would throw on the vessel below.
    /// </summary>
    [Fact]
    public void GivenEveryComparerInTheDescription_WhenEachIsApplied_ThenItReadsOnlyBaseMembers()
    {
        // Given
        var offered = AircraftFleetDescription.Offered;
        var comparers = offered.Columns.Select(static column => column.Comparer).Somes().ToList();
        TransportVehicle first = new Barge("imo1", "Tug One", "Panama", LastContact);
        TransportVehicle second = new Barge("imo2", "Tug Two", "Liberia", LastContact.AddMinutes(1));

        // When
        var orders = comparers.Select(comparer => comparer.Compare(first, second)).ToList();

        // Then
        comparers.Should().HaveCount(3, "three of the four columns are sortable; the position column is not");
        orders.Should().OnlyContain(static order => order != 0, "two different vehicles are ordered, and none of this was a downcast");
        offered.Columns.Should().Contain(static column => column.Comparer.IsNone, "an unsortable column says so with an absent comparer rather than a null");
    }

    /// <summary>
    /// fleet-pipeline B-011. Two vehicles a column cannot separate are separated by their keys, so
    /// the order is total and two sorts of an unchanged fleet produce the same sequence. Without the
    /// tie-break the pair's order is whatever the sort algorithm last did with it, which on stage is
    /// two rows swapping places on a poll that changed nothing.
    /// </summary>
    [Fact]
    public void GivenTwoVehiclesThatCompareEqual_WhenSortedTwice_ThenBothSortsOrderThemByKey()
    {
        // Given
        var cache = Cache();
        FleetTracker sut = Tracker(cache);
        using var subscription = sut.Fleet.SortAndBind(out var rows, sut.Order).Subscribe();
        cache.AddOrUpdate(Aircraft("f00002", "SAME", "Brazil"));
        cache.AddOrUpdate(Aircraft("f00001", "SAME", "Brazil"));
        sut.SortBy(ByCountry);
        var first = rows.Select(static row => row.Vehicle.Key).ToList();

        // When
        sut.SortBy(ByCallsign);
        sut.SortBy(ByCountry);

        // Then
        first.Should().Equal(["f00001", "f00002"]);
        rows.Select(static row => row.Vehicle.Key).Should().Equal(first, "the same fleet sorted twice by the same comparer is the same sequence");
    }

    /// <summary>A cache of domain vehicles, keyed the way the seam keys its changesets.</summary>
    /// <returns>A store a test edits to make the seam report.</returns>
    private static SourceCache<TransportVehicle, string> Cache() => new(static vehicle => vehicle.Key);

    /// <summary>A tracker over one store, described by the aircraft source's own description.</summary>
    /// <param name="cache">The store the seam reports from.</param>
    /// <returns>The system under test.</returns>
    private static FleetTracker Tracker(SourceCache<TransportVehicle, string> cache)
    {
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());

        return new FleetTrackerFixture().WithSource(source);
    }

    /// <summary>An aircraft with the three base answers a column reads.</summary>
    /// <param name="key">The <c>icao24</c> in lowercase hex.</param>
    /// <param name="callsign">What the identity column shows.</param>
    /// <param name="country">The country it is registered in, which is also its grouping key.</param>
    /// <returns>The vehicle a test puts into the store.</returns>
    private static TransportVehicle Aircraft(string key, string callsign, string country) =>
        new Aircraft(key, LastContact) { Callsign = callsign, OriginCountry = country };

    /// <summary>The description's country comparer, which is what a test hands to <c>SortBy</c>.</summary>
    private static readonly IComparer<TransportVehicle> ByCountry =
        AircraftFleetDescription.Offered.Columns
            .Single(static column => column.Name == "Origin country")
            .Comparer
            .IfNone(static () => Comparer<TransportVehicle>.Default);

    /// <summary>The description's callsign comparer, the one the tracker falls back to.</summary>
    private static readonly IComparer<TransportVehicle> ByCallsign =
        AircraftFleetDescription.Offered.Columns
            .Single(static column => column.Name == "Callsign")
            .Comparer
            .IfNone(static () => Comparer<TransportVehicle>.Default);

    /// <summary>The instant every vehicle here was last heard from.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}

/// <summary>
/// A vehicle no source in this repository reports, which is how B-010 shows that a comparer reads the
/// base and not an <c>Aircraft</c>.
/// </summary>
/// <param name="key">The cache key.</param>
/// <param name="label">What the identity column shows.</param>
/// <param name="flag">The country of registry, which is this type's grouping answer.</param>
/// <param name="lastContact">The instant the source last heard from it.</param>
internal sealed class Barge(string key, string label, string flag, DateTimeOffset lastContact) : TransportVehicle(key, lastContact)
{
    /// <inheritdoc/>
    public override string Label { get; } = label;

    /// <inheritdoc/>
    public override string GroupKey { get; } = flag;
}
