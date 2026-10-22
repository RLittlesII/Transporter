using LanguageExt;
using Transporter.Model;

namespace Transporter.Integrations.AisStream;

internal sealed record VesselSnapshot
{
    public required string Mmsi { get; init; }

    public Option<DateTimeOffset> Reported { get; init; }

    public Option<GeoPosition> Position { get; init; }

    public Option<double> SpeedOverGround { get; init; }

    public Option<double> CourseOverGround { get; init; }

    public Option<double> TrueHeading { get; init; }

    public Option<string> Name { get; init; }

    public Option<int> ShipType { get; init; }
}
