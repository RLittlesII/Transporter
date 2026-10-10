using LanguageExt;
using Transporter.Features.Fleet;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.UnitTests.Model.Fixtures;
using Transporter.UnitTests.Tracking.Fixtures;

namespace Transporter.UnitTests.Features.Fleet;

/// <summary>An element and the mark its card's badge carries (B-032).</summary>
/// <remarks>
/// Each case names the hazard it exists for. A climb is the change the altitude readout prints,
/// 9,000 m to 9,036.6 m being +120 ft. Stale is listed with a climb so a precedence that let
/// updated win would fail; no fix likewise. A turn changes a value no readout names a change for,
/// so it stays fresh — updated means a change the card shows, not any field that moved. A vehicle
/// just added replaced nothing and is fresh.
/// </remarks>
public sealed class FleetCardStatusCases : TheoryData<string, TrackedVehicle, FleetCardMark>
{
    public FleetCardStatusCases()
    {
        Add("a climb", Element(Climbed(), stale: false), FleetCardMark.Updated);
        Add("a climb gone silent", Element(Climbed(), stale: true), FleetCardMark.Stale);
        Add("a climb with no position", Element(Climbed().WithPosition(Option<GeoPosition>.None), stale: false), FleetCardMark.NoFix);
        Add("a turn", Turned(), FleetCardMark.Fresh);
        Add("a vehicle just added", new TrackedVehicleFixture().WithVehicle(Placed().WithBarometricAltitude(9_000)), FleetCardMark.Fresh);
    }

    private static AircraftFixture Placed() => new AircraftFixture().WithPosition(new GeoPosition(29.76, -95.37));

    private static AircraftFixture Climbed() => Placed().WithBarometricAltitude(9_036.6);

    private static TrackedVehicle Element(AircraftFixture current, bool stale) =>
        new TrackedVehicleFixture()
            .WithVehicle(current)
            .WithReplaced((TransportVehicle) new AircraftFixture().WithBarometricAltitude(9_000))
            .WithIsStale(stale);

    private static TrackedVehicle Turned() =>
        new TrackedVehicleFixture()
            .WithVehicle(Placed().WithBarometricAltitude(9_000).WithTrueTrack(180))
            .WithReplaced((TransportVehicle) new AircraftFixture().WithBarometricAltitude(9_000).WithTrueTrack(90));
}
