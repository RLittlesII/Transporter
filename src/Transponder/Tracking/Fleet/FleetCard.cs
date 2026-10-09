using System.Collections.Generic;
using LanguageExt;

namespace Transponder.Tracking.Fleet;

/// <summary>Which of the description's columns fill a card (B-036).</summary>
/// <remarks>A filled role is one of <see cref="FleetSourceDescription.Columns"/>, the instance and not its name.</remarks>
public sealed record FleetCard
{
    /// <summary>Gets the column the card is titled by, absent when the source names none.</summary>
    public Option<FleetColumn> Title { get; init; }

    /// <summary>Gets the column under the title, absent when the source names none.</summary>
    public Option<FleetColumn> Subtitle { get; init; }

    /// <summary>Gets the column naming where the vehicle is, absent when the source names none.</summary>
    public Option<FleetColumn> Place { get; init; }

    /// <summary>Gets the readouts, in the order a card shows them.</summary>
    public IReadOnlyList<FleetReadout> Readouts { get; init; } = [];
}
