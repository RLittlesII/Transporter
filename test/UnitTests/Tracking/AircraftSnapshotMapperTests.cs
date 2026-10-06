using AwesomeAssertions;
using LanguageExt;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Integrations.OpenSky;
using Transponder.Model;
using Transponder.Tracking.Sources;
using Transponder.UnitTests.Integrations.OpenSky.Fixtures;

namespace Transponder.UnitTests.Tracking;

public class AircraftSnapshotMapperTests
{
    /// <summary>
    /// B-035. Each value is read back as the wire's own number, so a conversion to feet, knots or a
    /// local time would change the figure rather than merely relabel it. The wire's units are
    /// canonical in the model and display units are the view's (ADR-0005 item 7).
    /// </summary>
    /// <param name="unit">Which reported value the case is about.</param>
    /// <param name="reported">What the wire reported.</param>
    [Theory]
    [ClassData(typeof(ReportedUnitCases))]
    public void GivenMetresMetresPerSecondAndDegrees_WhenProjected_ThenEachReachesTheVehicleUnconverted(
        ReportedUnit unit,
        double reported)
    {
        // Given
        AircraftSnapshotMapper sut = new AircraftSnapshotMapperFixture();
        var snapshot = ReportedUnits.Reporting(unit, reported);

        // When
        var vehicle = sut.Project(snapshot);

        // Then
        Some(ReportedUnits.Stored(vehicle, unit), reported);
    }

    /// <summary>
    /// B-036. The hazard is that both wrong answers render as plausible data: an absent altitude
    /// mapped to <c>0</c> is an aircraft at sea level, and an absent position mapped to <c>0,0</c> is
    /// one in the Gulf of Guinea. The two altitudes are read separately, because a projection that
    /// collapsed them would still pass an assertion naming only one.
    /// </summary>
    [Fact]
    public void GivenAnAbsentAltitudeAndAnAbsentPosition_WhenProjected_ThenNeitherBecomesZeroAndBothAltitudesSurvive()
    {
        // Given
        AircraftSnapshotMapper sut = new AircraftSnapshotMapperFixture();
        AircraftSnapshot snapshot = new AircraftSnapshotFixture()
            .WithLongitude(Option<double>.None)
            .WithLatitude(Option<double>.None)
            .WithBarometricAltitude(Option<double>.None)
            .WithGeometricAltitude(9280.0);

        // When
        var vehicle = sut.Project(snapshot);

        // Then
        None(vehicle.Position);
        NotSome(vehicle.Position, new GeoPosition(0, 0));
        None(vehicle.BarometricAltitude);
        NotSome(vehicle.BarometricAltitude, 0d);
        Some(vehicle.GeometricAltitude, 9280.0);
    }

    /// <summary>
    /// B-037. Uppercase hex is what the hazard turns on: the same aircraft reported as
    /// <c>"A1B2C3"</c> and <c>"a1b2c3"</c> is two cache entries and two rows on the grid, and no
    /// type system catches it. The key is read off a constructed vehicle, so it cannot have been set
    /// after the fact.
    /// </summary>
    [Fact]
    public void GivenAnUppercaseIcao24_WhenProjected_ThenTheKeyIsLowercase()
    {
        // Given
        AircraftSnapshotMapper sut = new AircraftSnapshotMapperFixture();
        AircraftSnapshot snapshot = new AircraftSnapshotFixture().WithIcao24("A1B2C3");

        // When
        var vehicle = sut.Project(snapshot);

        // Then
        vehicle.Key.Should().Be("a1b2c3");
        AircraftSnapshotMapper.ToKey("A1B2C3").Should().Be("a1b2c3");
    }

    /// <summary>
    /// B-035 again, from the other side: the country the wire reported survives the projection as
    /// the wire spelled it. The lowercasing belongs to the key alone, and a mapping that applied it
    /// to every string would still have passed the key's own test.
    /// </summary>
    [Fact]
    public void GivenAReportedOriginCountry_WhenProjected_ThenOnlyTheKeyWasLowercased()
    {
        // Given
        AircraftSnapshotMapper sut = new AircraftSnapshotMapperFixture();
        AircraftSnapshot snapshot = new AircraftSnapshotFixture()
            .WithIcao24("A1B2C3")
            .WithOriginCountry("United Kingdom");

        // When
        var vehicle = sut.Project(snapshot);

        // Then
        vehicle.OriginCountry.Should().Be("United Kingdom");
        vehicle.Key.Should().Be("a1b2c3");
    }

    /// <summary>
    /// B-036's enum half. A code this build does not name is absent rather than a named value, which
    /// is a different absence from an unreported field — B-018's warning applied to an enum.
    /// </summary>
    [Fact]
    public void GivenAPositionSourceCodeThisBuildDoesNotName_WhenProjected_ThenItIsAbsentRatherThanNamed()
    {
        // Given
        AircraftSnapshotMapper sut = new AircraftSnapshotMapperFixture();
        AircraftSnapshot reported = new AircraftSnapshotFixture().WithPositionSource(2);
        AircraftSnapshot unknown = new AircraftSnapshotFixture().WithPositionSource(9);

        // When
        var named = sut.Project(reported);
        var unnamed = sut.Project(unknown);

        // Then
        Some(named.PositionSource, Transponder.Model.PositionSource.Mlat);
        None(unnamed.PositionSource);
    }

    /// <summary>
    /// B-034's instant. The wire's Unix seconds are the snapshot's and the conversion is the
    /// mapper's, in a named method a test can call, so the snapshot stays the server's record
    /// (B-013).
    /// </summary>
    [Fact]
    public void GivenUnixSecondsOnTheSnapshot_WhenProjected_ThenTheVehicleCarriesTheInstant()
    {
        // Given
        AircraftSnapshotMapper sut = new AircraftSnapshotMapperFixture();
        AircraftSnapshot snapshot = new AircraftSnapshotFixture()
            .WithLastContact(1791124320L)
            .WithTimePosition(1791124315L);

        // When
        var vehicle = sut.Project(snapshot);

        // Then
        vehicle.LastContact.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1791124320L));
        Some(vehicle.TimePosition, DateTimeOffset.FromUnixTimeSeconds(1791124315L));
    }

    /// <summary>Asserts an optional value is present and is the expected one.</summary>
    /// <typeparam name="T">What the option holds.</typeparam>
    /// <param name="actual">The option read from a vehicle.</param>
    /// <param name="expected">What the wire reported.</param>
    private static void Some<T>(Option<T> actual, T expected) =>
        ((object) actual).Should().Be(Option<T>.Some(expected));

    /// <summary>Asserts an optional value is absent.</summary>
    /// <typeparam name="T">What the option would have held.</typeparam>
    /// <param name="actual">The option read from a vehicle.</param>
    private static void None<T>(Option<T> actual) => ((object) actual).Should().Be(Option<T>.None);

    /// <summary>Asserts an optional value is not one particular present value.</summary>
    /// <typeparam name="T">What the option holds.</typeparam>
    /// <param name="actual">The option read from a vehicle.</param>
    /// <param name="unwanted">The value a defaulting projection would have produced.</param>
    private static void NotSome<T>(Option<T> actual, T unwanted) =>
        ((object) actual).Should().NotBe(Option<T>.Some(unwanted));
}

/// <summary>Builds the projection, so a constructor change edits this and not every test.</summary>
/// <remarks>
/// Hand-written over <see cref="AutoFixtureBase{TFixture}"/> rather than generated: the generator
/// emits no fixture for a type that takes no constructor parameters, and the mapper takes none.
/// </remarks>
internal sealed class AircraftSnapshotMapperFixture : AutoFixtureBase<AircraftSnapshotMapperFixture>
{
    public static implicit operator AircraftSnapshotMapper(AircraftSnapshotMapperFixture fixture) => new();
}
