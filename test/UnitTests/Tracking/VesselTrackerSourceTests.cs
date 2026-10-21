using AwesomeAssertions;
using DynamicData;
using LanguageExt;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.AisStream;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Integrations.AisStream.Fixtures;
using Transporter.UnitTests.Tracking.Fixtures;

namespace Transporter.UnitTests.Tracking;

public class VesselTrackerSourceTests
{
    /// <summary>B-010. A cache entry with no feed instant stays out of the fleet until a timed update arrives.</summary>
    [Fact]
    public void GivenAVesselNoMessageHasTimed_WhenTheFleetIsRead_ThenItIsAbsentUntilAMessageTimesIt()
    {
        // Given
        using var cache = new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi);
        VesselClient client = new VesselClientFixture().WithVessels(cache);
        VesselTrackerSource strategy = new VesselTrackerSourceFixture().WithClient(client).WithSnapshots(cache);
        ITrackerSource sut = strategy;
        var observed = new List<IChangeSet<TransportVehicle, string>>();
        using var subscription = sut.Connect().Subscribe(observed.Add);
        VesselSnapshot untimed = new TimedVesselSnapshotFixture()
            .WithMmsi("366999006")
            .WithName("BAY RUNNER")
            .WithReported(Option<DateTimeOffset>.None);

        // When
        cache.AddOrUpdate(untimed);
        var beforeTime = observed.Count;
        cache.AddOrUpdate(untimed with { Reported = new DateTimeOffset(2026, 10, 21, 14, 32, 10, TimeSpan.Zero) });

        // Then
        beforeTime.Should().Be(0);
        var change = observed.Should().ContainSingle().Which.Should().ContainSingle().Which;
        change.Reason.Should().Be(ChangeReason.Add);
        change.Key.Should().Be("366999006");
        change.Current.Should().BeOfType<Vessel>();
        change.Current.LastContact.Should().Be(new DateTimeOffset(2026, 10, 21, 14, 32, 10, TimeSpan.Zero));
    }
}

/// <summary>Builds the vessel strategy with a controlled client and a keyed snapshot cache.</summary>
[AutoFixture(typeof(VesselTrackerSource))]
internal partial class VesselTrackerSourceFixture
{
    public VesselTrackerSourceFixture()
    {
        WithClient(new VesselClientFixture());
        WithSnapshots(new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi));
        WithMapper(new VesselSnapshotMapper());
    }
}
