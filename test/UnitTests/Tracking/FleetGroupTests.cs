using System.Reactive.Linq;
using AwesomeAssertions;
using DynamicData;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;

namespace Transporter.UnitTests.Tracking;

// RSA1010 asks for ObserveOn before every Bind. These bind without one deliberately: the
// pipeline marshals for nobody (fleet-pipeline B-005) and the user-interface scheduler belongs to
// the consumer (§ 5 row 9). The consumer here is a test, which has no user-interface thread.
#pragma warning disable RSA1010

public class FleetGroupTests
{
    /// <summary>
    /// fleet-pipeline B-012. A grouping handed to the tracker reforms the groups over the vehicles
    /// already there: six aircraft in three countries become two groups under a grouping that reads
    /// something else, with the fleet's own rows untouched and the seam connected once. A pipeline
    /// that rebuilt a stage to regroup would pass the group assertion and fail these two, which is
    /// why they are here.
    /// </summary>
    [Fact]
    public void GivenABoundFleet_WhenTheGroupingChanges_ThenTheGroupsReformWithoutRebuildingThePipeline()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = new CountingTrackerSource(cache);
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var fleet = sut.Fleet.Bind(out var rows).Subscribe();
        using var groups = sut.Groups.Bind(out var grouped).Subscribe();
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland", "ALFA"));
        cache.AddOrUpdate(Reporting("d4e5f6", "Testland", "BRAVO"));
        cache.AddOrUpdate(Reporting("070809", "Exampleland", "ALFA"));
        cache.AddOrUpdate(Reporting("b1c2d3", "Exampleland", "BRAVO"));
        cache.AddOrUpdate(Reporting("e4f5a6", "Pacifica", "ALFA"));
        cache.AddOrUpdate(Reporting("c7d8e9", "Pacifica", "ALFA"));
        var held = rows.ToList();
        var byCountry = grouped.Select(static group => group.Key).Order().ToList();

        // When
        sut.GroupBy(ByCallsign);

        // Then
        byCountry.Should().Equal("Exampleland", "Pacifica", "Testland");
        grouped.Select(static group => group.Key).Order().Should().Equal("ALFA", "BRAVO");
        grouped.Sum(static group => group.Count).Should().Be(6, "regrouping moves vehicles between groups and loses none");
        rows.Should().OnlyContain(
            row => held.Any(instance => ReferenceEquals(instance, row)),
            "the fleet's rows are the instances the consumer already held");
        source.Connections.Should().Be(1, "a new grouping re-evaluates membership and re-subscribes to nothing");
    }

    /// <summary>
    /// fleet-pipeline B-014. A group reports what it holds and how much of it has gone silent, both
    /// derived from the same stream the fleet is: two aircraft in one country, one of them last heard
    /// from six minutes before the observed instant against a five-minute threshold, is two tracked
    /// and one stale. Counting a bound collection instead would make the number a view's to compute,
    /// and two views would compute it twice.
    /// </summary>
    [Fact]
    public void GivenAGroupWithOneSilentVehicle_WhenItsCountsAreRead_ThenTheyReportTwoTrackedAndOneStale()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        FleetTracker sut = new FleetTrackerFixture()
            .WithSource(new CountingTrackerSource(cache))
            .WithClock(clock)
            .WithTicks(clock);
        using var groups = sut.Groups.Bind(out var grouped).Subscribe();

        // When
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(6));
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland", "ALFA"));
        cache.AddOrUpdate(new Aircraft("d4e5f6", LastContact + TimeSpan.FromMinutes(6)) { OriginCountry = "Testland", Callsign = "BRAVO" });

        // Then
        var group = grouped.Should().ContainSingle().Subject;
        group.Key.Should().Be("Testland");
        group.Count.Should().Be(2, "both aircraft are in the group, silent or not");
        group.StaleCount.Should().Be(1, "one of the two has been silent longer than the threshold allows");
    }

    /// <summary>
    /// fleet-pipeline B-014, the half the counts alone do not show: a group carries its rows as a
    /// stream, so a consumer rendering one group binds it and one showing only the counts binds
    /// nothing. A group holding a list instead would be the second store of tracked items B-002
    /// forbids.
    /// </summary>
    [Fact]
    public void GivenAGroupOfTwoCountries_WhenOneGroupsVehiclesAreBound_ThenOnlyThatGroupsRowsArrive()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        FleetTracker sut = new FleetTrackerFixture().WithSource(new CountingTrackerSource(cache));
        using var groups = sut.Groups.Bind(out var grouped).Subscribe();
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland", "ALFA"));
        cache.AddOrUpdate(Reporting("d4e5f6", "Exampleland", "BRAVO"));

        // When
        var testland = grouped.Single(static group => group.Key == "Testland");
        using var vehicles = testland.Vehicles.Bind(out var rows).Subscribe();

        // Then
        rows.Should().ContainSingle().Which.Vehicle.Key.Should().Be("a1b2c3", "the group's stream carries its own rows and no others");
    }

    /// <summary>One aircraft, reporting a country to group by and a callsign to group by instead.</summary>
    /// <param name="key">The vehicle's key.</param>
    /// <param name="country">What the default grouping reads.</param>
    /// <param name="callsign">What <see cref="ByCallsign"/> reads.</param>
    /// <returns>The aircraft.</returns>
    private static Aircraft Reporting(string key, string country, string callsign) =>
        new(key, LastContact) { OriginCountry = country, Callsign = callsign };

    /// <summary>A second grouping, so the regrouping can be observed over one fleet.</summary>
    /// <remarks>
    /// It reads <see cref="TransportVehicle.Label"/> rather than an aircraft's callsign directly,
    /// because B-022 forbids a grouping key reaching a concrete subclass.
    /// </remarks>
    private static readonly FleetGrouping ByCallsign = new()
    {
        Name = "Callsign",
        Key = static vehicle => vehicle.Label,
    };

    /// <summary>The instant every vehicle here was last heard from.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}
