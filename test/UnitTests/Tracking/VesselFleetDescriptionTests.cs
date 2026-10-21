using AwesomeAssertions;
using Transporter.Model;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Model.Fixtures;

namespace Transporter.UnitTests.Tracking;

public class VesselFleetDescriptionTests
{
    /// <summary>B-019. The vessel source supplies the cells, sort choices and card roles the live view reads.</summary>
    [Fact]
    public void GivenTheDescription_WhenItIsRead_ThenItNamesItsColumnsComparersCardTrailAndDetail()
    {
        // Given
        var sut = VesselFleetDescription.Offered;
        TransportVehicle vessel = new VesselFixture()
            .WithName("BAY RUNNER")
            .WithShipType(70)
            .WithSpeedOverGround(12.4)
            .WithCourseOverGround(87.5)
            .WithTrueHeading(90);

        // When
        var cells = sut.Columns.Select(column => (column.Name, Cell: column.Value(vessel))).ToList();
        var detail = sut.Detail.Select(column => (column.Name, Cell: column.Value(vessel))).ToList();
        var card = sut.Card;

        // Then
        cells.Should().Contain(("Name", "BAY RUNNER"))
            .And.Contain(("Ship type", "Cargo"))
            .And.Contain(("Flag state", "United States of America"))
            .And.Contain(("Speed over ground", "12.4 kt"))
            .And.Contain(("Course over ground", "87.5 °"));
        sut.Columns.Where(static column => column.Comparer.IsSome).Select(static column => column.Name)
            .Should().Equal(["Name", "Ship type", "Last contact"]);
        card.Title.IfNoneUnsafe((Transporter.Tracking.Fleet.FleetColumn?) null).Should().BeSameAs(sut.Columns[0]);
        card.Subtitle.IfNoneUnsafe((Transporter.Tracking.Fleet.FleetColumn?) null).Should().BeSameAs(sut.Columns[1]);
        card.Readouts.Select(static readout => readout.Column.Name).Should().Equal(["Speed over ground", "Course over ground"]);
        sut.Trail.Name.Should().Be("Speed over ground");
        sut.Trail.Minimum.Should().Be(0);
        sut.Trail.Maximum.Should().Be(30);
        sut.Trail.Value(vessel).IfNone(-1).Should().Be(12.4);
        detail.Should().Contain(("MMSI", "366999001"))
            .And.Contain(("True heading", "90 °"));
    }

    /// <summary>B-019 and B-020. Both groupings read the base selector and preserve Unknown before static data.</summary>
    [Fact]
    public void GivenVesselsWithAndWithoutStaticData_WhenGrouped_ThenShipTypeAndFlagStateAreIndependent()
    {
        // Given
        var sut = VesselFleetDescription.Offered;
        TransportVehicle named = new VesselFixture().WithMmsi("366999003").WithShipType(70);
        TransportVehicle unknown = new VesselFixture().WithMmsi("999999001");

        // When
        var groupNames = sut.Groupings.Select(static grouping => grouping.Name).ToArray();
        var knownGroups = sut.Groupings.Select(grouping => grouping.Key(named)).ToArray();
        var unknownGroups = sut.Groupings.Select(grouping => grouping.Key(unknown)).ToArray();

        // Then
        groupNames.Should().Equal(["Ship type", "Flag state"]);
        knownGroups.Should().Equal(["Cargo", "United States of America"]);
        unknownGroups.Should().Equal(["Unknown", "Unknown"]);
    }

    /// <summary>B-019. A filter choice admits and rejects vessels on data the source actually reports.</summary>
    [Fact]
    public void GivenTheDescription_WhenItsFiltersAreRead_ThenEachChoiceConstrainsTheFleet()
    {
        // Given
        var sut = VesselFleetDescription.Offered;
        TransportVehicle moving = new VesselFixture().WithName("BAY RUNNER").WithSpeedOverGround(12.4).WithPosition(new GeoPosition(29.4, -94.9));
        TransportVehicle quiet = new VesselFixture().WithMmsi("366999002").WithSpeedOverGround(0);

        // When
        var choices = sut.Filters.ToDictionary(static choice => choice.Name);

        // Then
        choices.Keys.Should().BeEquivalentTo(["Moving", "Named vessel", "Reporting a position"]);
        choices.Values.Should().OnlyContain(choice => choice.Matches(moving) && !choice.Matches(quiet));
    }
}
