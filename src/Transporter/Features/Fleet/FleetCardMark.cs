namespace Transporter.Features.Fleet;

/// <summary>Which status a card's badge carries, decided from the element alone (B-032).</summary>
public enum FleetCardMark
{
    /// <summary>Reporting, with nothing a readout shows changed by the last update.</summary>
    Fresh,

    /// <summary>The last update changed a value a readout shows.</summary>
    Updated,

    /// <summary>Silent past the threshold, and kept (B-008).</summary>
    Stale,

    /// <summary>Reporting, but with no position to place it by.</summary>
    NoFix,
}
