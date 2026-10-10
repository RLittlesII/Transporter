using System.Linq;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;

namespace Transporter.Features.Fleet;

/// <summary>The status a card's badge shows: the one decision the badge in the head is not allowed to make (B-032).</summary>
/// <remarks>
/// Read off the element the card is bound to, never off a value the view remembers, so a recycled
/// card shows its new vehicle's status at once. Updated means the last update changed a value a
/// readout shows — the same change the readout prints under its value (B-038) — and lasts until
/// the next update, which is what "updated" means to a person reading one poll at a time.
/// </remarks>
public static class FleetCardStatus
{
    /// <summary>The mark for one element on a card laid out from these roles.</summary>
    /// <param name="card">The roles in force, whose readouts name the changes that count.</param>
    /// <param name="element">The element the card is bound to.</param>
    /// <returns>Stale before no fix before updated before fresh: the first that holds.</returns>
    /// <remarks>
    /// Stale outranks everything because a value that changed and then went silent is old news; no
    /// fix outranks updated because a card that cannot be placed says so before it says it moved.
    /// </remarks>
    public static FleetCardMark Of(FleetCard card, TrackedVehicle element) =>
        element.IsStale ? FleetCardMark.Stale
        : element.Vehicle.Position.IsNone ? FleetCardMark.NoFix
        : card.Readouts.Any(readout => readout.Change(element).IsSome) ? FleetCardMark.Updated
        : FleetCardMark.Fresh;
}
