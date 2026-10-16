using LanguageExt;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Model;
using Transporter.Tracking.Fleet;

namespace Transporter.UnitTests.Tracking.Fixtures;

internal sealed class FleetTrailMeasureFixture : AutoFixtureBase<FleetTrailMeasureFixture>
{
    public FleetTrailMeasureFixture WithValue(Func<TransportVehicle, Option<double>> value) => With(ref _value, value);

    public static implicit operator FleetTrailMeasure(FleetTrailMeasureFixture fixture) => new()
    {
        Name = "Test measure",
        Minimum = 0,
        Maximum = 100,
        Value = fixture._value,
    };

    private Func<TransportVehicle, Option<double>> _value = static _ => Option<double>.None;
}
