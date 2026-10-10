using System;
using LanguageExt;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;

namespace Transporter.Features.Fleet;

/// <summary>When a card's readout pulses: the one decision the animation in the head is not allowed to make (B-038).</summary>
/// <remarks>
/// The trigger is the bound element changing, never a timer and never a value the view model
/// remembers — the earlier reading is the element's own <c>Replaced</c> (`fleet-pipeline` B-041).
/// Whether the platform asks for reduced motion is the head's to read; that removes the motion and
/// never the change text, so it is not decided here.
/// </remarks>
public static class FleetCardMotion
{
    /// <summary>Whether a readout pulses as a card moves from one bound element to the next.</summary>
    /// <param name="readout">The readout, which names the change it shows or none.</param>
    /// <param name="shown">The element the card showed until now, absent on its first bind.</param>
    /// <param name="element">The element the card is bound to now.</param>
    /// <returns>
    /// <see langword="true"/> only for a newer reading of the vehicle the card already showed, on a
    /// readout that names a change: a recycled card bound to another vehicle, the same reading
    /// re-published as stale, and a vehicle just added all move nothing.
    /// </returns>
    public static bool Pulses(FleetReadout readout, Option<TrackedVehicle> shown, TrackedVehicle element) =>
        readout.Change(element).IsSome
        && shown.Exists(before => string.Equals(before.Vehicle.Key, element.Vehicle.Key, StringComparison.Ordinal)
            && !ReferenceEquals(before.Vehicle, element.Vehicle));
}
