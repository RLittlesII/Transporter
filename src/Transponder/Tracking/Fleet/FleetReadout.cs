using LanguageExt;

namespace Transponder.Tracking.Fleet;

/// <summary>One value a card reads out, and the change it shows when it names one (B-036, B-042).</summary>
/// <remarks>The delta lives here rather than on <see cref="FleetColumn"/>, so no sort or filter column can carry one.</remarks>
public sealed record FleetReadout
{
    /// <summary>Gets the column the readout shows.</summary>
    public required FleetColumn Column { get; init; }

    /// <summary>Gets the change the readout shows, absent when it shows none.</summary>
    public Option<FleetDelta> Delta { get; init; }

    /// <summary>The change this readout shows on an element, none when there is none to show (B-042).</summary>
    /// <param name="element">The element the card is bound to.</param>
    /// <returns>The change, or none: no delta named, nothing replaced, or nothing changed.</returns>
    public Option<string> Change(TrackedVehicle element) =>
        Delta.Bind(delta => element.Replaced.Bind(replaced => delta(replaced, element.Vehicle)));
}
