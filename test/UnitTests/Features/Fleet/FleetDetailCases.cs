using LanguageExt;
using Transponder.Model;
using Transponder.UnitTests.Model.Fixtures;

namespace Transponder.UnitTests.Features.Fleet;

/// <summary>
/// A detail row's label, the canonical value the aircraft reports for it, and what the pane reads —
/// the unit a person reads, converted by the pane and nowhere else (B-019).
/// </summary>
/// <remarks>
/// Every expected value is computable by hand: 10,668 m is exactly 35,000 ft, 231.5 m/s is 450 kt
/// to a tenth, and 5.08 m/s is exactly 1,000 ft/min, so the descent case is about the sign alone and
/// its minus is U+2212. The heading converts nothing and is here so a conversion added to it fails.
/// An absent altitude reads as the missing mark, never as "0 ft".
/// </remarks>
public sealed class FleetDetailUnitCases : TheoryData<string, double?, string>
{
    public FleetDetailUnitCases()
    {
        Add("Baro altitude", 10_668, "35,000 ft");
        Add("GPS altitude", 10_972.8, "36,000 ft");
        Add("Ground speed", 231.5, "450 kt");
        Add("Vertical rate", -5.08, "−1,000 ft/min");
        Add("Heading", 275.4, "275°");
        Add("Baro altitude", null, "—");
    }
}

/// <summary>Builds an aircraft reporting the value one detail row reads.</summary>
internal static class DetailAircraft
{
    /// <summary>An aircraft reporting the value the named row reads, and absent when it is null.</summary>
    /// <param name="label">The row's label.</param>
    /// <param name="value">The value in the unit the vehicle keeps, or null for none reported.</param>
    /// <returns>The aircraft.</returns>
    public static Aircraft Reporting(string label, double? value)
    {
        var reported = value is { } number ? Option<double>.Some(number) : Option<double>.None;
        var aircraft = new AircraftFixture();

        return label switch
        {
            "Baro altitude" => aircraft.WithBarometricAltitude(reported),
            "GPS altitude" => aircraft.WithGeometricAltitude(reported),
            "Ground speed" => aircraft.WithVelocity(reported),
            "Vertical rate" => aircraft.WithVerticalRate(reported),
            _ => aircraft.WithTrueTrack(reported),
        };
    }

    /// <summary>The canonical value the named row reads off an aircraft, so a test can say it is unchanged.</summary>
    /// <param name="label">The row's label.</param>
    /// <param name="aircraft">The aircraft.</param>
    /// <returns>The value as the vehicle keeps it.</returns>
    public static Option<double> Canonical(string label, Aircraft aircraft) => label switch
    {
        "Baro altitude" => aircraft.BarometricAltitude,
        "GPS altitude" => aircraft.GeometricAltitude,
        "Ground speed" => aircraft.Velocity,
        "Vertical rate" => aircraft.VerticalRate,
        _ => aircraft.TrueTrack,
    };
}
