using AwesomeAssertions;
using LanguageExt;
using Transporter.Model;
using Transporter.UnitTests.Model.Fixtures;

namespace Transporter.UnitTests.Model;

public class VesselTests
{
    /// <summary>B-017. AIS name padding and empty names fall back to the unchanged MMSI.</summary>
    /// <param name="name">The reported name or absence.</param>
    /// <param name="expected">The label to show.</param>
    [Theory]
    [ClassData(typeof(VesselNameCases))]
    public void GivenAName_WhenTheLabelIsRead_ThenItIsTheTrimmedNameOrTheMmsi(Option<string> name, string expected)
    {
        // Given
        Vessel sut = new VesselFixture().WithMmsi("366999005").WithName(name);

        // When
        var label = sut.Label;

        // Then
        label.Should().Be(expected);
        sut.Key.Should().Be("366999005");
    }

    /// <summary>B-020. A reported type code resolves to its allocated category or Unknown.</summary>
    /// <param name="code">The reported type code.</param>
    /// <param name="expected">The grouping label.</param>
    [Theory]
    [ClassData(typeof(VesselShipTypeCases))]
    public void GivenATypeCode_WhenTheShipTypeIsRead_ThenItIsTheM1371CategoryOrUnknown(int code, string expected)
    {
        // Given
        Vessel sut = new VesselFixture().WithShipType(code);

        // When
        var category = sut.ShipTypeName;

        // Then
        category.Should().Be(expected);
        sut.GroupKey.Should().Be(expected);
    }

    /// <summary>B-020. The MID resolves from the nine-digit MMSI without static data.</summary>
    /// <param name="mmsi">The nine-digit identifier.</param>
    /// <param name="expected">The allocated flag state or Unknown.</param>
    [Theory]
    [ClassData(typeof(VesselMidCases))]
    public void GivenAnMmsi_WhenTheFlagStateIsRead_ThenItIsTheItuStateOrUnknown(string mmsi, string expected)
    {
        // Given
        Vessel sut = new VesselFixture().WithMmsi(mmsi);

        // When
        var state = sut.FlagState;

        // Then
        state.Should().Be(expected);
        sut.ShipTypeName.Should().Be("Unknown");
    }
}
