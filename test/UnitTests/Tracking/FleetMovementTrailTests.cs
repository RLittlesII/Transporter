using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AwesomeAssertions;
using DynamicData;
using LanguageExt;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Model.Fixtures;
using Transporter.UnitTests.Tracking.Fixtures;

namespace Transporter.UnitTests.Tracking;

// The test consumer has no UI thread to marshal to.
#pragma warning disable RSA1010

/// <summary>Trails published through the tracker.</summary>
public partial class FleetMovementTests
{
    /// <summary>fleet-pipeline B-034: only moves append points; removal and re-entry restart the trail.</summary>
    [Fact]
    public void GivenAMovingVehicle_WhenAltitudeAloneChangesAndItReenters_ThenItsTrailOnlyRecordsMovesAndRestarts()
    {
        // Given
        using var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        var heard = new DateTimeOffset(2026, 10, 16, 14, 0, 0, TimeSpan.Zero);
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard).WithPosition(new GeoPosition(29.70, -95.40)));

        // When
        for (var index = 1; index <= 3; index++)
        {
            cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard.AddSeconds(15 * index))
                .WithPosition(new GeoPosition(29.70 + (index * 0.1), -95.40)).WithBarometricAltitude(1_000 + index));
        }

        var before = rows.Single().Trail;
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard.AddMinutes(1))
            .WithPosition(new GeoPosition(30.00, -95.40)).WithBarometricAltitude(2_000));

        // Then
        var trail = rows.Single().Trail;
        trail.Should().BeSameAs(before);
        trail.Should().HaveCount(4);
        trail.Select(static point => point.Position.Latitude).Should().Equal(29.70, 29.80, 29.90, 30.00);
        trail.Select(static point => point.LastContact).Should().Equal(Enumerable.Range(0, 4).Select(index => heard.AddSeconds(15 * index)));
        trail[0].Distance.IsNone.Should().BeTrue();
        trail.Skip(1).Should().OnlyContain(point => Math.Abs(point.Distance.IfNone(double.NaN) - 11_119.5) < 1);
        trail.Select(static point => point.Measure.IfNone(double.NaN)).Should().Equal(1234.5, 1001, 1002, 1003);
        cache.RemoveKey(rows.Single().Vehicle.Key);
        rows.Should().BeEmpty();
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70, -95.40)));
        rows.Single().Trail.Should().ContainSingle();
        rows.Single().Trail[0].Distance.IsNone.Should().BeTrue();
    }

    /// <summary>fleet-pipeline B-034: point 241 drops the oldest without changing a previously published trail.</summary>
    [Fact]
    public void GivenATrailAtTheBound_WhenAnotherMoveArrives_ThenTheOldestPointGoesAndThePreviousTrailStands()
    {
        // Given
        using var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        for (var index = 0; index < 240; index++)
        {
            cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70 + (index * 0.001), -95.40)));
        }

        var before = rows.Single().Trail;

        // When
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.94, -95.40)));

        // Then
        var after = rows.Single().Trail;
        after.Should().HaveCount(240);
        after.Take(239).Should().Equal(before.Skip(1));
        after[^1].Position.Should().Be(new GeoPosition(29.94, -95.40));
        before.Should().HaveCount(240);
        before[0].Position.Should().Be(new GeoPosition(29.70, -95.40));
    }

    /// <summary>fleet-pipeline B-034: a filter hides the vehicle without dropping its trail or replaced vehicle.</summary>
    [Fact]
    public void GivenAVehicleHiddenByAFilter_WhenItMovesAndTheFilterClears_ThenItsTrailHoldsEveryPoint()
    {
        // Given
        using var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70, -95.40)));
        Aircraft replaced = new AircraftFixture().WithPosition(new GeoPosition(29.80, -95.40));
        cache.AddOrUpdate(replaced);
        sut.Filter(static _ => false);

        // When
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.90, -95.40)));
        rows.Should().BeEmpty();
        sut.Filter(static _ => true);

        // Then
        rows.Single().Trail.Should().HaveCount(3);
        rows.Single().Replaced.IfNone(rows.Single().Vehicle).Should().BeSameAs(replaced);
    }

    /// <summary>fleet-pipeline B-034, B-035: a lost fix spans points without inventing a leg or a silence.</summary>
    [Fact]
    public void GivenALostFix_WhenTheNextPositionArrives_ThenItsPointSpansTheFixWithoutALegOrGap()
    {
        // Given
        using var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        var heard = new DateTimeOffset(2026, 10, 16, 14, 0, 0, TimeSpan.Zero);
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard).WithPosition(new GeoPosition(29.70, -95.40)));
        var first = rows.Single().Trail;
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard.AddSeconds(15)));
        rows.Single().Trail.Should().BeSameAs(first);

        // When
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard.AddSeconds(30)).WithPosition(new GeoPosition(29.90, -95.40)));

        // Then
        var row = rows.Single();
        row.Trail.Should().HaveCount(2);
        row.Trail[^1].Distance.IfNone(double.NaN).Should().BeApproximately(22_239, 1);
        row.Trail[^1].FollowsGap.Should().BeFalse();
        row.Leg.IsNone.Should().BeTrue();
        row.Travelled.Should().Be(0);
    }

    /// <summary>fleet-pipeline B-035: only a report gap strictly exceeding the live threshold breaks a trail.</summary>
    /// <param name="seconds">The time between reports.</param>
    /// <param name="gap">Whether the new point follows a gap.</param>
    [Theory]
    [ClassData(typeof(TrailGapCases))]
    public void GivenTwoReports_WhenTheVehicleMoves_ThenTheGapUsesTheirContacts(double seconds, bool gap)
    {
        // Given
        using var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        var heard = new DateTimeOffset(2026, 10, 16, 14, 0, 0, TimeSpan.Zero);
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard).WithPosition(new GeoPosition(29.70, -95.40)));

        // When
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard.AddSeconds(seconds)).WithPosition(new GeoPosition(29.80, -95.40)));

        // Then
        rows.Single().Trail[^1].FollowsGap.Should().Be(gap);
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard.AddSeconds(seconds + 15)).WithPosition(new GeoPosition(29.90, -95.40)));
        rows.Single().Trail[^1].FollowsGap.Should().BeFalse();
    }

    /// <summary>fleet-pipeline B-035: parked reports refresh the gap baseline even though they append no points.</summary>
    [Fact]
    public void GivenAParkedVehicleReportingThroughout_WhenItMoves_ThenItFollowsNoGap()
    {
        // Given
        using var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        var heard = new DateTimeOffset(2026, 10, 16, 14, 0, 0, TimeSpan.Zero);
        for (var index = 0; index <= 40; index++)
        {
            cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard.AddSeconds(index * 15)).WithPosition(new GeoPosition(29.70, -95.40)));
        }

        // When
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard.AddSeconds(615)).WithPosition(new GeoPosition(29.80, -95.40)));

        // Then
        rows.Single().Trail.Should().HaveCount(2);
        rows.Single().Trail[^1].FollowsGap.Should().BeFalse();
    }

    /// <summary>fleet-pipeline B-035: a first fix has no preceding line to break, even after a silence.</summary>
    [Fact]
    public void GivenAVehicleWithoutAPosition_WhenItsFirstFixFollowsSilence_ThenTheFirstPointHasNoGap()
    {
        // Given
        using var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        var heard = new DateTimeOffset(2026, 10, 16, 14, 0, 0, TimeSpan.Zero);
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard));
        rows.Single().Trail.Should().BeEmpty();

        // When
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard.AddSeconds(450)).WithPosition(new GeoPosition(29.70, -95.40)));

        // Then
        rows.Single().Trail.Should().ContainSingle();
        rows.Single().Trail[0].FollowsGap.Should().BeFalse();
        rows.Single().Trail[0].Distance.IsNone.Should().BeTrue();
    }

    /// <summary>fleet-pipeline B-035: changing the threshold or ticking the clock never rewrites a point.</summary>
    /// <param name="minutes">The threshold when the point is made.</param>
    /// <param name="laterMinutes">The later threshold.</param>
    /// <param name="gap">The point's recorded gap mark.</param>
    [Theory]
    [ClassData(typeof(TrailThresholdCases))]
    public void GivenAPointMadeUnderOneThreshold_WhenTheThresholdChanges_ThenItsGapMarkStands(int minutes, int laterMinutes, bool gap)
    {
        // Given
        using var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        ObservedClock clock = new TrailObservedClockFixture();
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithClock(clock).WithTicks(clock);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        var heard = new DateTimeOffset(2026, 10, 16, 14, 0, 0, TimeSpan.Zero);
        sut.StaleAfter(TimeSpan.FromMinutes(minutes));
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard).WithPosition(new GeoPosition(29.70, -95.40)));
        cache.AddOrUpdate(new AircraftFixture().WithLastContact(heard.AddSeconds(450)).WithPosition(new GeoPosition(29.80, -95.40)));
        var trail = rows.Single().Trail;
        trail[^1].FollowsGap.Should().Be(gap);

        // When
        sut.StaleAfter(TimeSpan.FromMinutes(laterMinutes));
        ((IObservedClockWriter) clock).Observe(heard.AddHours(1));

        // Then
        rows.Single().IsStale.Should().BeTrue();
        rows.Single().Trail.Should().BeSameAs(trail);
        rows.Single().Trail[^1].FollowsGap.Should().Be(gap);
    }

    /// <summary>fleet-pipeline B-037: the aircraft's range and selector use barometric metres, with absence preserved.</summary>
    [Fact]
    public void GivenTheAircraftDescription_WhenItsTrailMeasureIsRead_ThenItUsesBarometricMetresOrNone()
    {
        // Given
        var measure = AircraftFleetDescription.Offered.Trail;
        TransportVehicle aircraft = new AircraftFixture().WithBarometricAltitude(1_234).WithGeometricAltitude(9_000);
        TransportVehicle absent = new AircraftFixture().WithBarometricAltitude(Option<double>.None);

        // When
        var value = measure.Value(aircraft);
        var missing = measure.Value(absent);

        // Then
        measure.Name.Should().Be("Altitude");
        measure.Minimum.Should().Be(0);
        measure.Maximum.Should().Be(12_500);
        value.IfNone(double.NaN).Should().Be(1_234);
        missing.IsNone.Should().BeTrue();
    }

    /// <summary>fleet-pipeline B-037: description changes affect only subsequent points and never replay movement.</summary>
    [Fact]
    public void GivenATrail_WhenTheDescriptionChanges_ThenOnlyTheNextPointUsesItsMeasure()
    {
        // Given
        using var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using var descriptions = new BehaviorSubject<FleetSourceDescription>(AircraftFleetDescription.Offered);
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithObservable(descriptions);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70, -95.40)).WithBarometricAltitude(1_000));
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.80, -95.40)).WithBarometricAltitude(2_000));
        var before = rows.Single();
        FleetTrailMeasure measure = new FleetTrailMeasureFixture().WithValue(static _ => 42);

        // When
        descriptions.OnNext(new FleetSourceDescriptionFixture().WithTrail(measure));

        // Then
        rows.Single().Should().BeSameAs(before);
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.90, -95.40)).WithBarometricAltitude(3_000));
        rows.Single().Trail.Select(static point => point.Measure.IfNone(double.NaN)).Should().Equal(1_000, 2_000, 42);
        rows.Single().Travelled.Should().BeApproximately(22_239, 1);
        before.Trail.Should().HaveCount(2);
    }

    /// <summary>fleet-pipeline B-037: a source reporting before its description loses no vehicle or point.</summary>
    [Fact]
    public void GivenNoDescription_WhenAVehicleArrives_ThenItsFirstPointCarriesNoMeasure()
    {
        // Given
        using var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        using var descriptions = new Subject<FleetSourceDescription>();
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithObservable(descriptions);
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe();

        // When
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70, -95.40)));

        // Then
        rows.Should().ContainSingle();
        rows.Single().Trail.Should().ContainSingle();
        rows.Single().Trail[0].Measure.IsNone.Should().BeTrue();
        descriptions.OnNext(AircraftFleetDescription.Offered);
        rows.Single().Trail[0].Measure.IsNone.Should().BeTrue();
    }

    /// <summary>fleet-pipeline B-028, B-034, B-037: the shared fold releases its description and resets when consumers leave.</summary>
    [Fact]
    public void GivenTwoTrailConsumers_WhenBothLeave_ThenTheDescriptionIsReleasedAndTheNextTrailRestarts()
    {
        // Given
        using var cache = new SourceCache<TransportVehicle, string>(static vehicle => vehicle.Key);
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        var connections = 0;
        var releases = 0;
        var description = Observable.Create<FleetSourceDescription>(observer =>
        {
            connections++;
            observer.OnNext(AircraftFleetDescription.Offered);
            return Disposable.Create(() => releases++);
        });
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source).WithObservable(description);
        var first = sut.Fleet.Bind(out var rows).Subscribe();
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.70, -95.40)));
        cache.AddOrUpdate(new AircraftFixture().WithPosition(new GeoPosition(29.80, -95.40)));
        var second = sut.Fleet.Bind(out var other).Subscribe();

        // When
        connections.Should().Be(1);
        other.Single().Trail.Should().BeSameAs(rows.Single().Trail);
        first.Dispose();
        releases.Should().Be(0);
        second.Dispose();

        // Then
        releases.Should().Be(1);
        using var third = sut.Fleet.Bind(out var restarted).Subscribe();
        connections.Should().Be(2);
        restarted.Single().Trail.Should().ContainSingle();
        restarted.Single().Trail[0].Position.Should().Be(new GeoPosition(29.80, -95.40));
    }
}

internal sealed class TrailObservedClockFixture : AutoFixtureBase<TrailObservedClockFixture>
{
    public static implicit operator ObservedClock(TrailObservedClockFixture fixture) => new();
}

#pragma warning restore RSA1010
