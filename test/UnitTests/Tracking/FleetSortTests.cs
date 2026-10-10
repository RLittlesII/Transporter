using System.Collections.Generic;
using System.Reactive.Linq;
using AwesomeAssertions;
using DynamicData;
using LanguageExt;
using NSubstitute;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.Tracking.Sources;

namespace Transporter.UnitTests.Tracking;

public class FleetSortTests
{
    /// <summary>
    /// fleet-pipeline B-009 and B-010. A comparer handed to the tracker reorders the rows a consumer
    /// already holds — the same item instances in the order that column defines, with nothing
    /// re-fetched, no stage rebuilt and the seam still connected once. Every sortable column the
    /// description offers gets a case, and the two aircraft are built so no two columns agree, so a
    /// comparer wired to the wrong column fails rather than passing by coincidence.
    /// </summary>
    /// <remarks>
    /// The bound collection raises one <c>Reset</c> for a comparer change, which is DynamicData's own
    /// notification for a re-sort rather than evidence of a refill — the instances are the ones the
    /// test already held. B-009's wording was corrected on 2026-10-06 to claim what the pipeline owes
    /// rather than which notification the binding adapter picks.
    /// </remarks>
    /// <param name="column">The description column whose comparer is applied.</param>
    /// <param name="expected">The keys, in the order that column puts them, comma separated.</param>
    [Theory]
    [ClassData(typeof(SortedColumnCases))]
    public void GivenABoundFleet_WhenANewComparerArrives_ThenTheRowsReorderWithoutBeingRefetchedOrRebuilt(string column, string expected)
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = new CountingTrackerSource(cache);
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.SortAndBind(out var rows, sut.Order).Subscribe();
        cache.AddOrUpdate(new Aircraft("a1b2c3", LastContact) { Callsign = "ALFA", OriginCountry = "Mexico" });
        cache.AddOrUpdate(new Aircraft("d4e5f6", LastContact.AddMinutes(1)) { Callsign = "ZULU", OriginCountry = "Canada" });
        var held = rows.ToList();

        // When
        sut.SortBy(Offered(column));

        // Then
        rows.Select(static row => row.Vehicle.Key).Should().Equal(expected.Split(','));
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
    /// forbids and the description exists to replace — would throw on the barges below.
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
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.SortAndBind(out var rows, sut.Order).Subscribe();
        cache.AddOrUpdate(new Aircraft("f00002", LastContact) { Callsign = "SAME", OriginCountry = "Brazil" });
        cache.AddOrUpdate(new Aircraft("f00001", LastContact) { Callsign = "SAME", OriginCountry = "Brazil" });
        sut.SortBy(Offered("Origin country"));
        var first = rows.Select(static row => row.Vehicle.Key).ToList();

        // When
        sut.SortBy(Offered("Callsign"));
        sut.SortBy(Offered("Origin country"));

        // Then
        first.Should().Equal(["f00001", "f00002"]);
        rows.Select(static row => row.Vehicle.Key).Should().Equal(first, "the same fleet sorted twice by the same comparer is the same sequence");
    }

    /// <summary>One column's comparer, read off the live source's description rather than written here (B-010).</summary>
    /// <param name="column">The column's display name.</param>
    /// <returns>The comparer that column sorts by.</returns>
    private static IComparer<TransportVehicle> Offered(string column) =>
        AircraftFleetDescription.Offered.Columns
            .Single(name => name.Name == column)
            .Comparer
            .IfNone(static () => Comparer<TransportVehicle>.Default);

    /// <summary>The instant the first vehicle in every case was last heard from.</summary>
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
