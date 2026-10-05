using LanguageExt;
using Transponder.Integrations.OpenSky;
using Transponder.Model;
using Transponder.UnitTests.Integrations.OpenSky.Fixtures;

namespace Transponder.UnitTests.Tracking;

/// <summary>Which reported value a case is about.</summary>
public enum ReportedUnit
{
    /// <summary>Index 7, metres.</summary>
    BarometricAltitude,

    /// <summary>Index 13, metres.</summary>
    GeometricAltitude,

    /// <summary>Index 9, metres per second.</summary>
    Velocity,

    /// <summary>Index 11, metres per second.</summary>
    VerticalRate,

    /// <summary>Index 10, degrees clockwise from north.</summary>
    TrueTrack,
}

/// <summary>
/// The five reported values against what the vehicle stores: each arrives in the unit the wire used,
/// and feet, knots and a local time appear nowhere (B-035).
/// </summary>
public sealed class ReportedUnitCases : TheoryData<ReportedUnit, double>
{
    public ReportedUnitCases()
    {
        Add(ReportedUnit.BarometricAltitude, 9144.0);
        Add(ReportedUnit.GeometricAltitude, 9280.0);
        Add(ReportedUnit.Velocity, 231.5);
        Add(ReportedUnit.VerticalRate, -2.6);
        Add(ReportedUnit.TrueTrack, 287.4);
    }
}

/// <summary>Builds a snapshot reporting one value, and reads back the member a case is about.</summary>
internal static class ReportedUnits
{
    /// <summary>A snapshot reporting one value.</summary>
    /// <param name="unit">Which member the case is about.</param>
    /// <param name="value">What the wire reported.</param>
    /// <returns>The snapshot.</returns>
    internal static AircraftSnapshot Reporting(ReportedUnit unit, double value) =>
        unit switch
        {
            ReportedUnit.BarometricAltitude => new AircraftSnapshotFixture().WithBarometricAltitude(value),
            ReportedUnit.GeometricAltitude => new AircraftSnapshotFixture().WithGeometricAltitude(value),
            ReportedUnit.Velocity => new AircraftSnapshotFixture().WithVelocity(value),
            ReportedUnit.VerticalRate => new AircraftSnapshotFixture().WithVerticalRate(value),
            ReportedUnit.TrueTrack => new AircraftSnapshotFixture().WithTrueTrack(value),
            _ => throw new ArgumentOutOfRangeException(nameof(unit)),
        };

    /// <summary>What the vehicle stores for the member a case is about.</summary>
    /// <param name="vehicle">The projected aircraft.</param>
    /// <param name="unit">Which member the case is about.</param>
    /// <returns>The stored value.</returns>
    internal static Option<double> Stored(Aircraft vehicle, ReportedUnit unit) =>
        unit switch
        {
            ReportedUnit.BarometricAltitude => vehicle.BarometricAltitude,
            ReportedUnit.GeometricAltitude => vehicle.GeometricAltitude,
            ReportedUnit.Velocity => vehicle.Velocity,
            ReportedUnit.VerticalRate => vehicle.VerticalRate,
            ReportedUnit.TrueTrack => vehicle.TrueTrack,
            _ => throw new ArgumentOutOfRangeException(nameof(unit)),
        };
}
