using AwesomeAssertions;
using LanguageExt;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.AisStream;
using Transporter.Model;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Tracking.Fixtures;

namespace Transporter.UnitTests.Tracking;

public class VesselSnapshotMapperTests
{
    /// <summary>B-017: the projection holds the normalized name and uses MMSI when the name is absent.</summary>
    /// <param name="name">The name held in the snapshot.</param>
    /// <param name="normalized">The name held in the vessel, or absence.</param>
    /// <param name="label">The displayed label.</param>
    [Theory]
    [ClassData(typeof(VesselCachedNameCases))]
    public void GivenACachedName_WhenProjected_ThenVesselNameAndLabelAreNormalized(
        Option<string> name, string? normalized, string label)
    {
        // Given
        VesselSnapshotMapper sut = new VesselSnapshotMapperFixture();
        VesselSnapshot snapshot = new TimedVesselSnapshotFixture().WithMmsi("366999005").WithName(name);

        // When
        var vessel = sut.Project(snapshot);

        // Then
        vessel.Name.IfNoneUnsafe((string?) null).Should().Be(normalized);
        vessel.Label.Should().Be(label);
    }

    /// <summary>B-018. Knots and degrees remain exactly as reported in the domain vehicle.</summary>
    [Fact]
    public void GivenReportedSpeedCourseAndHeading_WhenProjected_ThenTheyAreHeldAsReported()
    {
        // Given
        VesselSnapshotMapper sut = new VesselSnapshotMapperFixture();
        VesselSnapshot snapshot = new TimedVesselSnapshotFixture()
            .WithSpeedOverGround(12.4)
            .WithCourseOverGround(87.5)
            .WithTrueHeading(90);

        // When
        var vessel = sut.Project(snapshot);

        // Then
        vessel.SpeedOverGround.IfNone(-1).Should().Be(12.4);
        vessel.CourseOverGround.IfNone(-1).Should().Be(87.5);
        vessel.TrueHeading.IfNone(-1).Should().Be(90);
        vessel.LastContact.Should().Be(snapshot.Reported.IfNone(DateTimeOffset.MinValue));
    }

    /// <summary>B-016. The projection constructs the direct subclass and preserves the stable MMSI key.</summary>
    [Fact]
    public void GivenATimedSnapshot_WhenProjected_ThenItIsAVesselDirectlyBelowTransportVehicle()
    {
        // Given
        VesselSnapshotMapper sut = new VesselSnapshotMapperFixture();
        VesselSnapshot snapshot = new TimedVesselSnapshotFixture().WithMmsi("366999003").WithName("GULF TRADER").WithShipType(70);

        // When
        TransportVehicle vehicle = sut.Project(snapshot);

        // Then
        vehicle.Should().BeOfType<Vessel>();
        vehicle.Key.Should().Be("366999003");
        vehicle.Label.Should().Be("GULF TRADER");
        vehicle.GroupKey.Should().Be("Cargo");
    }
}

/// <summary>Builds the parameterless Mapperly projection for tests.</summary>
internal sealed class VesselSnapshotMapperFixture : AutoFixtureBase<VesselSnapshotMapperFixture>
{
    public static implicit operator VesselSnapshotMapper(VesselSnapshotMapperFixture fixture) => new();
}
