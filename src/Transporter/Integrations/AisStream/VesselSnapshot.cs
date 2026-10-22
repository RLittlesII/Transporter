using LanguageExt;
using Transporter.Integrations.AisStream.Messages;
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

    /// <summary>Merges one provider message without writing a cache or clock.</summary>
    /// <param name="message">The provider message.</param>
    /// <param name="resetReported">Whether a timed message begins a new recording pass for this vessel.</param>
    /// <returns>The merged snapshot.</returns>
    internal VesselSnapshot Apply(VesselMessage message, bool resetReported = false)
    {
        var snapshot = this;
        message.Reported.IfSome(instant =>
        {
            if (resetReported || snapshot.Reported.Match(latest => instant > latest, static () => true))
            {
                snapshot = snapshot with { Reported = instant };
            }
        });

        return message switch
        {
            VesselPositionMessage position => snapshot with
            {
                Position = position.Position,
                SpeedOverGround = position.SpeedOverGround,
                CourseOverGround = position.CourseOverGround,
                TrueHeading = position.TrueHeading,
            },
            VesselStaticMessage staticData => snapshot with
            {
                Name = staticData.Name,
                ShipType = staticData.ShipType,
            },
            _ => snapshot,
        };
    }
}
