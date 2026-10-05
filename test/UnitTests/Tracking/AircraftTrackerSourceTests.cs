using AwesomeAssertions;
using DynamicData;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Integrations.OpenSky;
using Transponder.Model;
using Transponder.Tracking;
using Transponder.Tracking.Sources;
using Transponder.UnitTests.Integrations.OpenSky.Fixtures;

namespace Transponder.UnitTests.Tracking;

public class AircraftTrackerSourceTests
{
    /// <summary>
    /// B-033. The strategy is held as the seam and never as itself, so a member only the concrete
    /// type declares could not be reached from here; what arrives through it is a changeset of
    /// domain vehicles keyed by the vehicle's own key. The "nothing else" clause is the declaration
    /// half: an empty per-type interface and <c>TRN0013</c> carry it, because § 8 rules out a test
    /// over <c>typeof</c> doing an analyzer's job badly.
    /// </summary>
    [Fact]
    public void GivenTheSeam_WhenItIsInspected_ThenItDeclaresTheTransportVehicleChangesetAndNothingElse()
    {
        // Given
        var cache = Cache();
        AircraftTrackerSource strategy = new AircraftTrackerSourceFixture().WithSnapshots(cache);
        ITrackerSource sut = strategy;
        var observed = new List<IChangeSet<TransportVehicle, string>>();

        // When
        using var subscription = sut.Connect().Subscribe(observed.Add);
        cache.AddOrUpdate((AircraftSnapshot) new AircraftSnapshotFixture().WithIcao24("a1b2c3"));

        // Then
        observed.Should().HaveCount(1);
        observed[0].Should().AllSatisfy(static change =>
        {
            change.Key.Should().Be("a1b2c3");
            change.Current.Key.Should().Be(change.Key);
        });
    }

    /// <summary>
    /// B-034. The snapshot goes in and a domain vehicle comes out, which is the whole of what the
    /// strategy adds: upstream of here the same aircraft is a record of wire values, and this is the
    /// first point at which it is an <see cref="Aircraft"/>. The projection is the strategy's own,
    /// so a second source is a second projection and nothing else.
    /// </summary>
    [Fact]
    public void GivenASnapshotChangeset_WhenItIsProjected_ThenTheStrategyIsWhereAnAircraftFirstExists()
    {
        // Given
        var cache = Cache();
        AircraftTrackerSource strategy = new AircraftTrackerSourceFixture().WithSnapshots(cache);
        ITrackerSource sut = strategy;
        var observed = new List<IChangeSet<TransportVehicle, string>>();
        using var subscription = sut.Connect().Subscribe(observed.Add);

        // When
        cache.AddOrUpdate((AircraftSnapshot) new AircraftSnapshotFixture().WithIcao24("a1b2c3").WithCallsign("FLT0421"));

        // Then
        var change = observed.Should().ContainSingle().Which.Should().ContainSingle().Which;
        change.Reason.Should().Be(ChangeReason.Add);
        change.Key.Should().Be("a1b2c3");
        change.Current.Should().BeOfType<Aircraft>();
        change.Current.Label.Should().Be("FLT0421");
    }

    /// <summary>
    /// B-023 downstream, and the reason the seam is worth having: a second report of the same values
    /// is not a change, so the strategy emits nothing for an aircraft that did not move.
    /// </summary>
    [Fact]
    public void GivenAnUnchangedAircraftReportedTwice_WhenTheSetIsReapplied_ThenTheSeamEmitsNothingForIt()
    {
        // Given
        var cache = Cache();
        AircraftTrackerSource strategy = new AircraftTrackerSourceFixture().WithSnapshots(cache);
        ITrackerSource sut = strategy;
        var observed = new List<IChangeSet<TransportVehicle, string>>();
        using var subscription = sut.Connect().Subscribe(observed.Add);
        AircraftSnapshot reported = new AircraftSnapshotFixture().WithIcao24("a1b2c3");
        cache.EditDiff([reported], EqualityComparer<AircraftSnapshot>.Default);

        // When
        cache.EditDiff([reported], EqualityComparer<AircraftSnapshot>.Default);

        // Then
        observed.Should().ContainSingle();
    }

    /// <summary>A cache keyed the way the registration keys it.</summary>
    /// <returns>The cache a client writes into and the strategy reads from.</returns>
    private static SourceCache<AircraftSnapshot, string> Cache() => new(static snapshot => snapshot.Icao24);
}

/// <summary>Builds the strategy, so a constructor change edits this and not every test.</summary>
[AutoFixture(typeof(AircraftTrackerSource))]
internal partial class AircraftTrackerSourceFixture
{
    public AircraftTrackerSourceFixture()
    {
        WithSnapshots(new SourceCache<AircraftSnapshot, string>(static snapshot => snapshot.Icao24));
        WithMapper(new AircraftSnapshotMapper());
    }
}
