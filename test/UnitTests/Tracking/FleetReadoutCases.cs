using LanguageExt;
using Transporter.Model;
using Transporter.Tracking.Fleet;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Model.Fixtures;
using static LanguageExt.Prelude;

namespace Transporter.UnitTests.Tracking;

/// <summary>
/// A readout, the value its aircraft reported before an update and after it, and the change the
/// readout shows — <see langword="null"/> where it shows none (fleet-pipeline B-042).
/// </summary>
/// <remarks>
/// Every expected change is computable by hand from 0.3048 m to the foot and 1,852 m to the nautical
/// mile. The reversed climb catches the delegate's arguments swapped; 9,000.1 m catches a comparison
/// of raw values; 8,999.95 m to 9,000.01 m catches a subtraction before rounding, since the raw
/// difference is a fifth of a foot but the cells read 29,527 and 29,528. The minus is U+2212.
/// </remarks>
public sealed class FleetReadoutCases : TheoryData<string, double?, double?, string?>
{
    public FleetReadoutCases()
    {
        Add("Altitude", 9_000, 9_036.6, "▲ +120 ft");
        Add("Altitude", 9_036.6, 9_000, "▼ −120 ft");
        Add("Altitude", 9_000, 9_000, null);
        Add("Altitude", 9_000, 9_000.1, null);
        Add("Altitude", 8_999.95, 9_000.01, "▲ +1 ft");
        Add("Altitude", null, 9_000, null);
        Add("Altitude", 9_000, null, null);
        Add("Ground speed", 200, 206.2, "▲ +12 kt");
    }
}

/// <summary>
/// A readout, the value its aircraft reported, and the cell it reads — in the unit a card shows,
/// so a delta at that cell's precision is in the same unit (fleet-pipeline B-042).
/// </summary>
/// <remarks>
/// 5.08 m/s is exactly 1,000 ft/min, so the vertical rate's sign is the only thing that case is
/// about; a descent carries U+2212, the minus a delta carries. A missing value is the em dash for
/// every readout, never a zero that reads as an aircraft on the ground.
/// </remarks>
public sealed class ReadoutCellCases : TheoryData<string, double?, string>
{
    public ReadoutCellCases()
    {
        Add("Altitude", 10_000, "32,808 ft");
        Add("Ground speed", 100, "194 kt");
        Add("Heading", 90.4, "090°");
        Add("Vertical rate", 5.08, "+1,000 ft/min");
        Add("Vertical rate", -5.08, "−1,000 ft/min");
        Add("Altitude", null, "—");
        Add("Ground speed", null, "—");
        Add("Heading", null, "—");
        Add("Vertical rate", null, "—");
    }
}

/// <summary>Finds a readout of the aircraft's card by name, and builds an aircraft reporting one value for it.</summary>
internal static class AircraftReadouts
{
    /// <summary>The aircraft card's readout whose column has this name.</summary>
    /// <param name="name">The column's name.</param>
    /// <returns>The readout.</returns>
    public static FleetReadout Named(string name) =>
        AircraftFleetDescription.Offered.Card.Readouts.Single(readout => readout.Column.Name == name);

    /// <summary>An aircraft reporting the value the named readout reads, and absent when it is null.</summary>
    /// <param name="name">The readout's column name.</param>
    /// <param name="value">The value in the unit the vehicle keeps, or null for none reported.</param>
    /// <returns>The aircraft.</returns>
    public static Aircraft Reporting(string name, double? value)
    {
        var reported = Optional(value);
        return name switch
        {
            "Altitude" => new AircraftFixture().WithBarometricAltitude(reported),
            "Ground speed" => new AircraftFixture().WithVelocity(reported),
            "Heading" => new AircraftFixture().WithTrueTrack(reported),
            "Vertical rate" => new AircraftFixture().WithVerticalRate(reported),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No readout of that name."),
        };
    }
}
