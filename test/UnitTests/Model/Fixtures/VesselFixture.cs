using LanguageExt;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Model;

namespace Transporter.UnitTests.Model.Fixtures;

/// <summary>Builds a vessel with synthetic defaults and one override per tested fact.</summary>
internal sealed class VesselFixture : AutoFixtureBase<VesselFixture>
{
    public VesselFixture WithMmsi(string mmsi) => With(ref _mmsi, mmsi);

    public VesselFixture WithLastContact(DateTimeOffset lastContact) => With(ref _lastContact, lastContact);

    public VesselFixture WithName(Option<string> name) => With(ref _name, name);

    public VesselFixture WithShipType(Option<int> shipType) => With(ref _shipType, shipType);

    public VesselFixture WithPosition(Option<GeoPosition> position) => With(ref _position, position);

    public VesselFixture WithSpeedOverGround(Option<double> speed) => With(ref _speed, speed);

    public VesselFixture WithCourseOverGround(Option<double> course) => With(ref _course, course);

    public VesselFixture WithTrueHeading(Option<double> heading) => With(ref _heading, heading);

    public static implicit operator Vessel(VesselFixture fixture) => fixture.Build();

    public static implicit operator TransportVehicle(VesselFixture fixture) => fixture.Build();

    private Vessel Build() => new(_mmsi, _lastContact)
    {
        Name = _name,
        ShipType = _shipType,
        Position = _position,
        SpeedOverGround = _speed,
        CourseOverGround = _course,
        TrueHeading = _heading,
    };

    private string _mmsi = "366999001";
    private DateTimeOffset _lastContact = new(2026, 10, 21, 14, 32, 10, TimeSpan.Zero);
    private Option<string> _name = Option<string>.None;
    private Option<int> _shipType = Option<int>.None;
    private Option<GeoPosition> _position = Option<GeoPosition>.None;
    private Option<double> _speed = Option<double>.None;
    private Option<double> _course = Option<double>.None;
    private Option<double> _heading = Option<double>.None;
}
