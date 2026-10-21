using LanguageExt;

namespace Transporter.Model;

/// <summary>ITU-R M.1371 ship type categories for allocated AIS codes.</summary>
internal static class ShipTypes
{
    public static Option<string> Category(int code) => code switch
    {
        >= 20 and <= 29 => "Wing in ground",
        30 => "Fishing",
        31 or 32 => "Towing",
        33 => "Dredging",
        34 => "Diving",
        35 => "Military",
        36 => "Sailing",
        37 => "Pleasure craft",
        >= 40 and <= 49 => "High-speed craft",
        50 => "Pilot",
        51 => "Search and rescue",
        52 => "Tug",
        53 => "Port tender",
        54 => "Anti-pollution",
        55 => "Law enforcement",
        58 => "Medical transport",
        59 => "Noncombatant",
        >= 60 and <= 69 => "Passenger",
        >= 70 and <= 79 => "Cargo",
        >= 80 and <= 89 => "Tanker",
        >= 90 and <= 99 => "Other",
        _ => Option<string>.None,
    };
}
