using System.Reactive.Disposables;
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
    /// <summary>B-024 and replay-source B-008. A supplied playback lifetime uses the same projection and retains its cache across subscriptions.</summary>
    [Fact]
    public void GivenASuppliedSubscriptionAndCache_WhenTheSourceReconnects_ThenTheCacheRemainsAndTheLifetimeIsReplaced()
    {
        // Given
        using var cache = new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi);
        var firstInstant = new DateTimeOffset(2026, 10, 21, 14, 32, 10, TimeSpan.Zero);
        var latestInstant = firstInstant.AddMinutes(1);
        cache.AddOrUpdate(new VesselSnapshot { Mmsi = "366999006", Reported = firstInstant });
        var started = 0;
        var stopped = 0;
        var source = VesselTrackerSource.FromSubscription(
            () =>
            {
                started++;
                return Disposable.Create(() => stopped++);
            },
            cache,
            new VesselSnapshotMapper());
        var first = new List<IChangeSet<TransportVehicle, string>>();
        var firstSubscription = source.Connect().Subscribe(first.Add);

        // When
        firstSubscription.Dispose();
        cache.AddOrUpdate(new VesselSnapshot { Mmsi = "366999006", Reported = latestInstant });
        var second = new List<IChangeSet<TransportVehicle, string>>();
        using var secondSubscription = source.Connect().Subscribe(second.Add);

        // Then
        started.Should().Be(2);
        stopped.Should().Be(1);
        first.Should().ContainSingle().Which.Should().ContainSingle().Which.Current.LastContact.Should().Be(firstInstant);
        second.Should().ContainSingle().Which.Should().ContainSingle().Which.Current.LastContact.Should().Be(latestInstant);
    }

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
