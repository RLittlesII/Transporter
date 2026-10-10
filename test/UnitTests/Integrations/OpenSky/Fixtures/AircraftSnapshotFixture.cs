using LanguageExt;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.OpenSky;

namespace Transporter.UnitTests.Integrations.OpenSky.Fixtures;

/// <summary>
/// Builds an <see cref="AircraftSnapshot"/>. Hand-written rather than generated because the target is a record,
/// and the defaults are a plausible wire row so a test overrides only the member it is about.
/// </summary>
internal sealed class AircraftSnapshotFixture : AutoFixtureBase<AircraftSnapshotFixture>
{
    public AircraftSnapshotFixture WithIcao24(string icao24) => With(ref _icao24, icao24);

    public AircraftSnapshotFixture WithCallsign(Option<string> callsign) => With(ref _callsign, callsign);

    public AircraftSnapshotFixture WithTimePosition(Option<long> timePosition) => With(ref _timePosition, timePosition);

    public AircraftSnapshotFixture WithLastContact(long lastContact) => With(ref _lastContact, lastContact);

    public AircraftSnapshotFixture WithVelocity(Option<double> velocity) => With(ref _velocity, velocity);

    public AircraftSnapshotFixture WithOriginCountry(string originCountry) => With(ref _originCountry, originCountry);

    public AircraftSnapshotFixture WithLongitude(Option<double> longitude) => With(ref _longitude, longitude);

    public AircraftSnapshotFixture WithLatitude(Option<double> latitude) => With(ref _latitude, latitude);

    public AircraftSnapshotFixture WithBarometricAltitude(Option<double> altitude) => With(ref _barometricAltitude, altitude);

    public AircraftSnapshotFixture WithGeometricAltitude(Option<double> altitude) => With(ref _geometricAltitude, altitude);

    public AircraftSnapshotFixture WithTrueTrack(Option<double> trueTrack) => With(ref _trueTrack, trueTrack);

    public AircraftSnapshotFixture WithVerticalRate(Option<double> verticalRate) => With(ref _verticalRate, verticalRate);

    public AircraftSnapshotFixture WithSquawk(Option<string> squawk) => With(ref _squawk, squawk);

    public AircraftSnapshotFixture WithPositionSource(int positionSource) => With(ref _positionSource, positionSource);

    public AircraftSnapshotFixture WithCategory(Option<int> category) => With(ref _category, category);

    public static implicit operator AircraftSnapshot(AircraftSnapshotFixture fixture) => fixture.Build();

    private AircraftSnapshot Build() =>
        new()
        {
            Icao24 = _icao24,
            Callsign = _callsign,
            OriginCountry = _originCountry,
            TimePosition = _timePosition,
            LastContact = _lastContact,
            Longitude = _longitude,
            Latitude = _latitude,
            BarometricAltitude = _barometricAltitude,
            OnGround = _onGround,
            Velocity = _velocity,
            TrueTrack = _trueTrack,
            VerticalRate = _verticalRate,
            GeometricAltitude = _geometricAltitude,
            Squawk = _squawk,
            Spi = _spi,
            PositionSource = _positionSource,
            Category = _category,
        };

    private readonly bool _onGround = false;
    private readonly bool _spi = false;
    private string _originCountry = "Testland";
    private Option<double> _longitude = -95.3698;
    private Option<double> _latitude = 29.7604;
    private Option<double> _barometricAltitude = 1234.5;
    private Option<double> _trueTrack = 91.2;
    private Option<double> _verticalRate = -1.3;
    private Option<double> _geometricAltitude = 1250.0;
    private Option<string> _squawk = "0021";
    private string _icao24 = "a1b2c3";
    private Option<string> _callsign = "TRN0001";
    private Option<long> _timePosition = 1791124315L;
    private long _lastContact = 1791124320L;
    private Option<double> _velocity = 128.6;
    private int _positionSource;
    private Option<int> _category = 1;
}
