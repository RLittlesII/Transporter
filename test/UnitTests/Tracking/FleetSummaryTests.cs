using System.Reactive.Linq;
using AwesomeAssertions;
using DynamicData;
using Transponder.Model;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;

namespace Transponder.UnitTests.Tracking;

public class FleetSummaryTests
{
    /// <summary>
    /// fleet-pipeline B-015. The summary derives from the stream the fleet derives from, so an add,
    /// an update and a remove each produce the next set of counts — vehicles tracked, vehicles stale
    /// and groups present — and each arrives as the change does rather than on a timer. A summary
    /// computed from a bound collection would be a view's to recount, and two views would disagree
    /// mid-poll.
    /// </summary>
    [Fact]
    public void GivenAFleetThatChanges_WhenTheSummaryIsObserved_ThenEachChangeProducesTheNewCounts()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        FleetTracker sut = new FleetTrackerFixture()
            .WithSource(new CountingTrackerSource(cache))
            .WithClock(clock)
            .WithTicks(clock);
        var reported = new List<FleetSummary>();
        using var subscription = sut.Summary.Subscribe(reported.Add);
        ((IObservedClockWriter) clock).Observe(LastContact);
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland"));
        cache.AddOrUpdate(Reporting("d4e5f6", "Exampleland"));

        // When
        cache.AddOrUpdate(Reporting("070809", "Exampleland"));
        cache.AddOrUpdate(new Aircraft("d4e5f6", LastContact - TimeSpan.FromMinutes(6)) { OriginCountry = "Exampleland" });
        cache.Remove("a1b2c3");

        // Then
        reported.Should().NotBeEmpty("the summary is published as the fleet changes");
        var current = reported[^1];
        current.Tracked.Should().Be(2, "one of the three was removed");
        current.Stale.Should().Be(1, "the updated aircraft reports a last contact six minutes before the observed instant");
        current.Groups.Should().Be(1, "both remaining vehicles answer the same grouping key");
    }

    /// <summary>
    /// fleet-pipeline B-015's second half: the summary changes once per change rather than on a
    /// cadence of its own. Three changes to the fleet, and the counts that reached a consumer are
    /// the three the fleet had — no scheduler ticked, and nothing re-published a value that had not
    /// moved.
    /// <para>
    /// A consumer that subscribes before the first changeset receives nothing until one arrives,
    /// which is what the sequence below starting at one rather than zero says: the summary reports
    /// what the fleet did, and an empty fleet nothing has reported about yet is not a change.
    /// </para>
    /// </summary>
    [Fact]
    public void GivenThreeChangesToTheFleet_WhenTheSummaryIsObserved_ThenItChangedOncePerChangeAndNotOnATimer()
    {
        // Given
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        FleetTracker sut = new FleetTrackerFixture().WithSource(new CountingTrackerSource(cache));
        var reported = new List<FleetSummary>();
        using var subscription = sut.Summary.Subscribe(reported.Add);

        // When
        cache.AddOrUpdate(Reporting("a1b2c3", "Testland"));
        cache.AddOrUpdate(Reporting("d4e5f6", "Exampleland"));
        cache.Remove("a1b2c3");

        // Then
        reported.Select(static summary => summary.Tracked).Should().Equal([1, 2, 1], "one value per change, and none before the first change");
        reported.Select(static summary => summary.Groups).Should().Equal([1, 2, 1]);
    }

    /// <summary>One aircraft, reporting the country the default grouping reads.</summary>
    /// <param name="key">The vehicle's key.</param>
    /// <param name="country">What the grouping reads.</param>
    /// <returns>The aircraft.</returns>
    private static Aircraft Reporting(string key, string country) =>
        new(key, LastContact) { OriginCountry = country };

    /// <summary>The instant every vehicle here was last heard from, and the one the clock is told about.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}
