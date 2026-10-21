using LanguageExt;
using Transporter.Model;

namespace Transporter.Integrations.AisStream.Messages;

/// <summary>One supported AIS message keyed by its MMSI.</summary>
internal abstract record VesselMessage(string Mmsi);

/// <summary>A position update and its reported movement values.</summary>
internal sealed record VesselPositionMessage(
    string Mmsi,
    Option<GeoPosition> Position,
    Option<double> SpeedOverGround,
    Option<double> CourseOverGround,
    Option<double> TrueHeading) : VesselMessage(Mmsi);

/// <summary>A ship name and type update.</summary>
internal sealed record VesselStaticMessage(string Mmsi, Option<string> Name, Option<int> ShipType) : VesselMessage(Mmsi);
