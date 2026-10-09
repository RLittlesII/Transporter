using AwesomeAssertions;
using LanguageExt;
using Transponder.Features.Fleet;
using Transponder.Model;
using Transponder.Tracking;
using Transponder.UnitTests.Model.Fixtures;
using Transponder.UnitTests.Tracking;
using Transponder.UnitTests.Tracking.Fixtures;

namespace Transponder.UnitTests.Features.Fleet;

public class FleetCardMotionTests
{
    /// <summary>
    /// B-038. A newer reading of the vehicle the card already showed, on a readout whose cell
    /// changed, pulses. This is the one case that moves, so it is the case every other one is
    /// measured against.
    /// </summary>
    [Fact]
    public void GivenANewerReadingOfTheShownVehicle_WhenTheAltitudeChanged_ThenTheReadoutPulses()
    {
        // Given
        var altitude = AircraftReadouts.Named("Altitude");
        TransportVehicle earlier = new AircraftFixture().WithBarometricAltitude(9_000);
        TrackedVehicle shown = new TrackedVehicleFixture().WithVehicle(earlier);
        TrackedVehicle element = new TrackedVehicleFixture()
            .WithVehicle(new AircraftFixture().WithBarometricAltitude(9_036.6))
            .WithReplaced(earlier);

        // When
        var pulses = FleetCardMotion.Pulses(altitude, shown, element);

        // Then
        pulses.Should().BeTrue();
    }

    /// <summary>
    /// B-038. A card's first bind has nothing shown before it, so nothing pulses even where the
    /// element carries a change: the page opening, or scrolling a card into view, is not an update.
    /// </summary>
    [Fact]
    public void GivenACardsFirstBind_WhenTheElementCarriesAChange_ThenNothingPulses()
    {
        // Given
        var altitude = AircraftReadouts.Named("Altitude");
        TrackedVehicle element = new TrackedVehicleFixture()
            .WithVehicle(new AircraftFixture().WithBarometricAltitude(9_036.6))
            .WithReplaced((TransportVehicle) new AircraftFixture().WithBarometricAltitude(9_000));

        // When
        var pulses = FleetCardMotion.Pulses(altitude, Option<TrackedVehicle>.None, element);

        // Then
        pulses.Should().BeFalse();
    }

    /// <summary>
    /// B-038. A collection recycles a card for another vehicle; that vehicle's change is its own
    /// last update, not something this card just watched happen, so nothing pulses.
    /// </summary>
    [Fact]
    public void GivenARecycledCard_WhenItIsBoundToAnotherVehicle_ThenNothingPulses()
    {
        // Given
        var altitude = AircraftReadouts.Named("Altitude");
        TrackedVehicle shown = new TrackedVehicleFixture().WithVehicle(new AircraftFixture().WithKey("ffffff"));
        TrackedVehicle element = new TrackedVehicleFixture()
            .WithVehicle(new AircraftFixture().WithBarometricAltitude(9_036.6))
            .WithReplaced((TransportVehicle) new AircraftFixture().WithBarometricAltitude(9_000));

        // When
        var pulses = FleetCardMotion.Pulses(altitude, shown, element);

        // Then
        pulses.Should().BeFalse();
    }

    /// <summary>
    /// B-038. The same reading re-published — marked stale with nothing new reported — still
    /// carries the change of its last update; pulsing it again would say the value moved twice.
    /// </summary>
    [Fact]
    public void GivenTheSameReadingRepublishedAsStale_WhenItIsBound_ThenNothingPulses()
    {
        // Given
        var altitude = AircraftReadouts.Named("Altitude");
        TransportVehicle earlier = new AircraftFixture().WithBarometricAltitude(9_000);
        TransportVehicle current = new AircraftFixture().WithBarometricAltitude(9_036.6);
        TrackedVehicle shown = new TrackedVehicleFixture().WithVehicle(current).WithReplaced(earlier);
        TrackedVehicle element = new TrackedVehicleFixture().WithVehicle(current).WithReplaced(earlier).WithIsStale(true);

        // When
        var pulses = FleetCardMotion.Pulses(altitude, shown, element);

        // Then
        pulses.Should().BeFalse();
    }

    /// <summary>
    /// B-038. A vehicle just added replaced nothing (`fleet-pipeline` B-041), so it shows no change
    /// and nothing pulses — the arrow and the pulse arrive with the first update, never the first
    /// sighting.
    /// </summary>
    [Fact]
    public void GivenAVehicleJustAdded_WhenItIsBound_ThenNothingPulses()
    {
        // Given
        var altitude = AircraftReadouts.Named("Altitude");
        TrackedVehicle shown = new TrackedVehicleFixture().WithVehicle(new AircraftFixture().WithBarometricAltitude(9_000));
        TrackedVehicle element = new TrackedVehicleFixture().WithVehicle(new AircraftFixture().WithBarometricAltitude(9_036.6));

        // When
        var pulses = FleetCardMotion.Pulses(altitude, shown, element);

        // Then
        pulses.Should().BeFalse();
    }
}
