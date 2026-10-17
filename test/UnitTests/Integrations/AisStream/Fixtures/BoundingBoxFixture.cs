using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.OpenSky.Model;

namespace Transporter.UnitTests.Integrations.AisStream.Fixtures;

/// <summary>Builds the shared synthetic geography for both sources.</summary>
internal sealed class BoundingBoxFixture : AutoFixtureBase<BoundingBoxFixture>
{
    public BoundingBoxFixture WithLatitudeMinimum(double value) => With(ref _latitudeMinimum, value);

    public BoundingBoxFixture WithLatitudeMaximum(double value) => With(ref _latitudeMaximum, value);

    public BoundingBoxFixture WithLongitudeMinimum(double value) => With(ref _longitudeMinimum, value);

    public BoundingBoxFixture WithLongitudeMaximum(double value) => With(ref _longitudeMaximum, value);

    public static implicit operator BoundingBox(BoundingBoxFixture fixture) =>
        new()
        {
            LatitudeMinimum = fixture._latitudeMinimum,
            LongitudeMinimum = fixture._longitudeMinimum,
            LatitudeMaximum = fixture._latitudeMaximum,
            LongitudeMaximum = fixture._longitudeMaximum,
        };

    private double _latitudeMinimum = 28.8;

    private double _longitudeMinimum = -95.9;

    private double _latitudeMaximum = 30.4;

    private double _longitudeMaximum = -94.4;
}
