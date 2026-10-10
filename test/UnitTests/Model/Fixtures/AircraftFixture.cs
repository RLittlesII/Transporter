using System;
using LanguageExt;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Model;

namespace Transporter.UnitTests.Model.Fixtures;

/// <summary>
/// Builds an <see cref="Aircraft"/>, so a test overrides the one member it is about and nothing
/// hand-constructs the domain type.
/// </summary>
/// <remarks>
/// Hand-written rather than generated for the reason
/// <see cref="Integrations.OpenSky.Fixtures.AircraftSnapshotFixture"/> gives: the generator names a
/// builder after its parameter's type, and the two constructor parameters here are a
/// <see cref="string"/> and a <see cref="DateTimeOffset"/> whose names are what a test wants to say.
/// <para>
/// The callsign defaults to <see cref="Option{A}.None"/>, unlike the snapshot fixture's, because
/// <see cref="Aircraft.Label"/> falls back to the key when it is absent — so a test that cares about
/// the label sets one, and a test that cares about identity reads the key it already gave.
/// </para>
/// </remarks>
internal sealed class AircraftFixture : AutoFixtureBase<AircraftFixture>
{
    /// <summary>Sets the <c>icao24</c> key, which is also the label while the callsign is absent.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithKey(string key) => With(ref _key, key);

    /// <summary>Sets the instant the source last heard from the aircraft.</summary>
    /// <param name="lastContact">The instant.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithLastContact(DateTimeOffset lastContact) => With(ref _lastContact, lastContact);

    /// <summary>Sets the callsign, which becomes the label when present.</summary>
    /// <param name="callsign">The callsign, or absent.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithCallsign(Option<string> callsign) => With(ref _callsign, callsign);

    /// <summary>Sets the country of registration, which is the grouping key.</summary>
    /// <param name="originCountry">The country.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithOriginCountry(string originCountry) => With(ref _originCountry, originCountry);

    /// <summary>Sets the reported position, absent when the source has no fix.</summary>
    /// <param name="position">The fix, or absent.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithPosition(Option<GeoPosition> position) => With(ref _position, position);

    /// <summary>Sets whether the aircraft is on the ground.</summary>
    /// <param name="onGround">Whether it is.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithOnGround(bool onGround) => With(ref _onGround, onGround);

    /// <summary>Sets the transponder code, as four characters.</summary>
    /// <param name="squawk">The code, or absent.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithSquawk(Option<string> squawk) => With(ref _squawk, squawk);

    /// <summary>Sets the wire's category integer, left uninterpreted.</summary>
    /// <param name="category">The category, or absent.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithCategory(Option<int> category) => With(ref _category, category);

    /// <summary>Sets the barometric altitude in metres.</summary>
    /// <param name="altitude">The altitude, or absent.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithBarometricAltitude(Option<double> altitude) => With(ref _barometricAltitude, altitude);

    /// <summary>Sets the geometric altitude in metres.</summary>
    /// <param name="altitude">The altitude, or absent.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithGeometricAltitude(Option<double> altitude) => With(ref _geometricAltitude, altitude);

    /// <summary>Sets the ground speed in metres per second.</summary>
    /// <param name="velocity">The speed, or absent.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithVelocity(Option<double> velocity) => With(ref _velocity, velocity);

    /// <summary>Sets the track in degrees clockwise from true north.</summary>
    /// <param name="trueTrack">The track, or absent.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithTrueTrack(Option<double> trueTrack) => With(ref _trueTrack, trueTrack);

    /// <summary>Sets the vertical rate in metres per second, negative descending.</summary>
    /// <param name="verticalRate">The rate, or absent.</param>
    /// <returns>The fixture, so building chains.</returns>
    public AircraftFixture WithVerticalRate(Option<double> verticalRate) => With(ref _verticalRate, verticalRate);

    /// <summary>Takes the subject, as every fixture here is taken.</summary>
    /// <param name="fixture">The fixture to build.</param>
    public static implicit operator Aircraft(AircraftFixture fixture) => fixture.Build();

    /// <summary>Takes the subject as the base, which is what every consumer above the model sees.</summary>
    /// <param name="fixture">The fixture to build.</param>
    public static implicit operator TransportVehicle(AircraftFixture fixture) => fixture.Build();

    private Aircraft Build() =>
        new(_key, _lastContact)
        {
            Callsign = _callsign,
            OriginCountry = _originCountry,
            Position = _position,
            OnGround = _onGround,
            Squawk = _squawk,
            Category = _category,
            BarometricAltitude = _barometricAltitude,
            GeometricAltitude = _geometricAltitude,
            Velocity = _velocity,
            TrueTrack = _trueTrack,
            VerticalRate = _verticalRate,
        };

    private string _key = "a1b2c3";
    private DateTimeOffset _lastContact = new(2026, 10, 7, 9, 15, 0, TimeSpan.Zero);
    private Option<string> _callsign = Option<string>.None;
    private string _originCountry = "Testland";
    private Option<GeoPosition> _position = Option<GeoPosition>.None;
    private bool _onGround;
    private Option<string> _squawk = "0021";
    private Option<int> _category = 1;
    private Option<double> _barometricAltitude = 1234.5;
    private Option<double> _geometricAltitude = 1250.0;
    private Option<double> _velocity = Option<double>.None;
    private Option<double> _trueTrack = Option<double>.None;
    private Option<double> _verticalRate = Option<double>.None;
}
