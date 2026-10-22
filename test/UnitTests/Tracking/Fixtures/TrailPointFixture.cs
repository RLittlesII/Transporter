using LanguageExt;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Model;
using Transporter.Tracking;

namespace Transporter.UnitTests.Tracking.Fixtures;

/// <summary>Builds one synthetic point in a vehicle trail.</summary>
internal sealed class TrailPointFixture : AutoFixtureBase<TrailPointFixture>
{
    public TrailPointFixture WithPosition(GeoPosition position) => With(ref _position, position);

    public TrailPointFixture WithLastContact(DateTimeOffset lastContact) => With(ref _lastContact, lastContact);

    public TrailPointFixture WithMeasure(Option<double> measure) => With(ref _measure, measure);

    public TrailPointFixture WithDistance(Option<double> distance) => With(ref _distance, distance);

    public TrailPointFixture WithFollowsGap(bool followsGap) => With(ref _followsGap, followsGap);

    public static implicit operator TrailPoint(TrailPointFixture fixture) => new()
    {
        Position = fixture._position,
        LastContact = fixture._lastContact,
        Measure = fixture._measure,
        Distance = fixture._distance,
        FollowsGap = fixture._followsGap,
    };

    private GeoPosition _position = new(40, -90);
    private DateTimeOffset _lastContact = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private Option<double> _measure = Option<double>.None;
    private Option<double> _distance = Option<double>.None;
    private bool _followsGap;
}
