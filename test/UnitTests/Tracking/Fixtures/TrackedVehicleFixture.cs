using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Model;
using Transponder.Tracking;
using Transponder.UnitTests.Model.Fixtures;

namespace Transponder.UnitTests.Tracking.Fixtures;

/// <summary>Builds a <see cref="TrackedVehicle"/> — the element the pipeline publishes.</summary>
/// <remarks>
/// Hand-written for the reason the other fixtures here are: the generator names a builder after its
/// parameter's type, and <c>Vehicle</c> reads better than <c>WithTransportVehicle</c>. The default
/// vehicle is an <see cref="AircraftFixture"/>'s, so a test that only cares about the mark says only
/// that.
/// </remarks>
internal sealed class TrackedVehicleFixture : AutoFixtureBase<TrackedVehicleFixture>
{
    /// <summary>Sets the vehicle the mark is derived for.</summary>
    /// <param name="vehicle">The vehicle.</param>
    /// <returns>The fixture, so building chains.</returns>
    public TrackedVehicleFixture WithVehicle(TransportVehicle vehicle) => With(ref _vehicle, vehicle);

    /// <summary>Sets whether the vehicle was silent past the threshold when the mark was derived.</summary>
    /// <param name="isStale">Whether it was.</param>
    /// <returns>The fixture, so building chains.</returns>
    public TrackedVehicleFixture WithIsStale(bool isStale) => With(ref _isStale, isStale);

    /// <summary>Takes the subject, as every fixture here is taken.</summary>
    /// <param name="fixture">The fixture to build.</param>
    public static implicit operator TrackedVehicle(TrackedVehicleFixture fixture) => fixture.Build();

    private TrackedVehicle Build() => new() { Vehicle = _vehicle, IsStale = _isStale };

    private TransportVehicle _vehicle = new AircraftFixture();
    private bool _isStale;
}
