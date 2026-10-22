using System.Reactive.Subjects;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Transporter.Model;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Model.Fixtures;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Tracking;

// The test binds synchronously because scheduling belongs to the consumer (fleet-pipeline B-005).
#pragma warning disable RSA1010

public class SilenceRemovalTests
{
    /// <summary>Vessel B-021/B-022, fleet-pipeline B-044: only observed time removes a silent row.</summary>
    [Fact]
    public void GivenAVessel_WhenObservedTimePassesTheThreshold_ThenItLeavesEveryBoundFleetProjection()
    {
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock)
            .WithProvider(schedulers)
            .WithObservable(new BehaviorSubject<FleetSourceDescription>(VesselFleetDescription.Offered));
        using var fleetSubscription = sut.Fleet.Bind(out var rows).Subscribe();
        using var groupSubscription = sut.Groups.Bind(out var groups).Subscribe();
        FleetSummary? summary = null;
        using var summarySubscription = sut.Summary.Subscribe(value => summary = value);

        ((IObservedClockWriter) clock).Observe(LastContact);
        cache.AddOrUpdate(new VesselFixture().WithLastContact(LastContact));
        scheduler.AdvanceBy(TimeSpan.FromHours(1).Ticks);
        rows.Should().ContainSingle("scheduler time cannot expire a vessel");
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(10));

        rows.Should().ContainSingle("the threshold is strictly longer than ten minutes");
        groups.Should().ContainSingle();
        summary!.Tracked.Should().Be(1);

        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        rows.Should().BeEmpty();
        groups.Should().BeEmpty();
        summary!.Tracked.Should().Be(0);
    }

    /// <summary>Vessel B-023: the description's configured threshold drives the live filter.</summary>
    [Fact]
    public void GivenAConfiguredTwentyMinuteThreshold_WhenObservedTimeAdvances_ThenRemovalUsesTwentyMinutes()
    {
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock)
            .WithObservable(new BehaviorSubject<FleetSourceDescription>(
                VesselFleetDescription.WithSilentAfter(TimeSpan.FromMinutes(20))));
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        ((IObservedClockWriter) clock).Observe(LastContact);
        cache.AddOrUpdate(new VesselFixture().WithLastContact(LastContact));

        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(15));
        rows.Should().ContainSingle();
        rows.Single().IsStale.Should().BeFalse();
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(20) + TimeSpan.FromSeconds(1));

        rows.Should().BeEmpty();
    }

    /// <summary>Vessel B-021, fleet-pipeline B-044: expiry does not erase source movement history.</summary>
    [Fact]
    public void GivenASilentVesselWithMovement_WhenItReportsAgain_ThenItReturnsAsAnAddWithItsHistory()
    {
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var source = new CountingTrackerSource(cache);
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock)
            .WithObservable(new BehaviorSubject<FleetSourceDescription>(VesselFleetDescription.Offered));
        var reasons = new List<ChangeReason>();
        using var subscription = sut.Fleet.Bind(out var rows)
            .Subscribe(changes => reasons.AddRange(changes.Select(static change => change.Reason)));
        ((IObservedClockWriter) clock).Observe(LastContact);
        cache.AddOrUpdate(new VesselFixture().WithLastContact(LastContact).WithPosition(new GeoPosition(29.7, -95.4)));
        cache.AddOrUpdate(new VesselFixture().WithLastContact(LastContact + TimeSpan.FromMinutes(1))
            .WithPosition(new GeoPosition(29.8, -95.4)));
        var travelledBefore = rows.Single().Travelled;
        rows.Single().Trail.Should().HaveCount(2);

        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(12));
        rows.Should().BeEmpty();
        cache.AddOrUpdate(new VesselFixture().WithLastContact(LastContact + TimeSpan.FromMinutes(12))
            .WithPosition(new GeoPosition(29.9, -95.4)));

        reasons.Should().ContainInOrder(ChangeReason.Add, ChangeReason.Update, ChangeReason.Remove, ChangeReason.Add);
        rows.Should().ContainSingle();
        rows.Single().Travelled.Should().BeGreaterThan(travelledBefore);
        rows.Single().Trail.Should().HaveCount(3);
        source.Connections.Should().Be(1);
    }

    /// <summary>Vessel B-023/B-027, fleet-pipeline B-044: removal never emits a stale mark.</summary>
    [Fact]
    public void GivenVesselTreatment_WhenTimeAdvances_ThenItIsNeverMarkedBeforeRemoval()
    {
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock)
            .WithObservable(new BehaviorSubject<FleetSourceDescription>(VesselFleetDescription.Offered));
        var published = new List<TrackedVehicle>();
        using var subscription = sut.Fleet.Bind(out var rows)
            .Subscribe(changes => published.AddRange(changes.Where(static change => change.Current is not null)
                .Select(static change => change.Current)));
        ((IObservedClockWriter) clock).Observe(LastContact);
        cache.AddOrUpdate(new VesselFixture().WithLastContact(LastContact));

        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(6));
        rows.Single().IsStale.Should().BeFalse();
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(10));
        rows.Single().IsStale.Should().BeFalse();
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        rows.Should().BeEmpty();
        published.Should().OnlyContain(static row => !row.IsStale);
    }

    /// <summary>Fleet-pipeline B-019/B-044: a live description changes treatment on the same tracker.</summary>
    [Fact]
    public void GivenVesselTreatment_WhenDescriptionChangesToAircraft_ThenMarkingReplacesRemovalWithoutReconnecting()
    {
        var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var clock = new ObservedClock();
        var source = new CountingTrackerSource(cache);
        var descriptions = new BehaviorSubject<FleetSourceDescription>(VesselFleetDescription.Offered);
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock)
            .WithObservable(descriptions);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromHours(1));
        cache.AddOrUpdate(new VesselFixture().WithLastContact(LastContact));
        rows.Should().BeEmpty();

        descriptions.OnNext(AircraftFleetDescription.Offered);
        cache.Remove("366999001");
        cache.AddOrUpdate(new Aircraft("a1b2c3", LastContact));

        rows.Should().ContainSingle().Which.IsStale.Should().BeTrue();
        source.Connections.Should().Be(1);
    }

    private static readonly DateTimeOffset LastContact = new(2026, 10, 21, 14, 0, 0, TimeSpan.Zero);
}

#pragma warning restore RSA1010
