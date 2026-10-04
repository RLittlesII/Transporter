using AwesomeAssertions;
using LanguageExt;
using Transponder.Integrations.OpenSky;
using Transponder.UnitTests.Integrations.OpenSky.Fixtures;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class AircraftSnapshotTests
{
    [Fact]
    public void GivenTwoSnapshotsReportingIdenticalValues_WhenCompared_ThenTheyAreEqual()
    {
        // Given
        AircraftSnapshot first = new AircraftSnapshotFixture();
        AircraftSnapshot second = new AircraftSnapshotFixture();

        // When
        var equal = first == second;

        // Then
        equal.Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void GivenTwoSnapshotsDifferingInOneReportedValue_WhenCompared_ThenTheyAreNotEqual()
    {
        // Given
        AircraftSnapshot reported = new AircraftSnapshotFixture().WithVelocity(128.6);
        AircraftSnapshot moved = new AircraftSnapshotFixture().WithVelocity(131.2);

        // When
        var equal = reported == moved;

        // Then
        equal.Should().BeFalse();
    }

    [Fact]
    public void GivenASnapshot_WhenItsKeyIsRead_ThenItIsTheNonOptionalIcao24()
    {
        // Given
        AircraftSnapshot snapshot = new AircraftSnapshotFixture().WithIcao24("4ca7b3");

        // When
        var key = snapshot.Icao24;

        // Then
        key.Should().Be("4ca7b3");
    }

    [Fact]
    public void GivenAWireRow_WhenTheSnapshotIsBuilt_ThenEveryValueIsTheWiresAndNoneIsConverted()
    {
        // Given
        const long timePosition = 1791124315L;
        const long lastContact = 1791124320L;

        // When
        AircraftSnapshot snapshot = new AircraftSnapshotFixture()
            .WithTimePosition(timePosition)
            .WithLastContact(lastContact)
            .WithVelocity(128.6)
            .WithPositionSource(2)
            .WithCategory(1);

        // Then
        snapshot.LastContact.Should().Be(lastContact);
        snapshot.TimePosition.IfNone(0L).Should().Be(timePosition);
        snapshot.Velocity.IfNone(0d).Should().Be(128.6);
        snapshot.PositionSource.Should().Be(2);
        snapshot.Category.IfNone(-1).Should().Be(1);
    }

    [Fact]
    public void GivenASnapshotWhoseWireValuesAreAbsent_WhenItsMembersAreRead_ThenNoneBecameADefault()
    {
        // Given
        AircraftSnapshot snapshot = new AircraftSnapshotFixture()
            .WithVelocity(Option<double>.None)
            .WithCategory(Option<int>.None)
            .WithCallsign(Option<string>.None);

        // Then
        snapshot.Velocity.IsNone.Should().BeTrue();
        snapshot.Category.IsNone.Should().BeTrue();
        snapshot.Callsign.IsNone.Should().BeTrue();
    }
}
