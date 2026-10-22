using LanguageExt;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.AisStream;
using Transporter.Model;

namespace Transporter.UnitTests.Tracking.Fixtures;

/// <summary>Builds a synthetic vessel snapshot with an optional feed instant.</summary>
internal sealed class TimedVesselSnapshotFixture : AutoFixtureBase<TimedVesselSnapshotFixture>
{
    public TimedVesselSnapshotFixture WithMmsi(string mmsi) => With(ref _mmsi, mmsi);

    public TimedVesselSnapshotFixture WithReported(Option<DateTimeOffset> reported) => With(ref _reported, reported);

    public TimedVesselSnapshotFixture WithName(Option<string> name) => With(ref _name, name);

    public TimedVesselSnapshotFixture WithShipType(Option<int> shipType) => With(ref _shipType, shipType);

    public TimedVesselSnapshotFixture WithPosition(Option<GeoPosition> position) => With(ref _position, position);

    public TimedVesselSnapshotFixture WithSpeedOverGround(Option<double> speed) => With(ref _speed, speed);

    public TimedVesselSnapshotFixture WithCourseOverGround(Option<double> course) => With(ref _course, course);

    public TimedVesselSnapshotFixture WithTrueHeading(Option<double> heading) => With(ref _heading, heading);

    public static implicit operator VesselSnapshot(TimedVesselSnapshotFixture fixture) => new()
    {
        Mmsi = fixture._mmsi,
        Reported = fixture._reported,
        Name = fixture._name,
        ShipType = fixture._shipType,
        Position = fixture._position,
        SpeedOverGround = fixture._speed,
        CourseOverGround = fixture._course,
        TrueHeading = fixture._heading,
    };

    private string _mmsi = "366999001";
    private Option<DateTimeOffset> _reported = new DateTimeOffset(2026, 10, 21, 14, 32, 10, TimeSpan.Zero);
    private Option<string> _name = Option<string>.None;
    private Option<int> _shipType = Option<int>.None;
    private Option<GeoPosition> _position = Option<GeoPosition>.None;
    private Option<double> _speed = Option<double>.None;
    private Option<double> _course = Option<double>.None;
    private Option<double> _heading = Option<double>.None;
}
