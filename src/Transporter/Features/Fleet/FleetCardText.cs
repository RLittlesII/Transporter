using System;
using System.Globalization;
using LanguageExt;

namespace Transporter.Features.Fleet;

/// <summary>
/// The two values a card shows that the description does not: a distance, and how long ago the
/// vehicle was last heard from (B-030, B-031).
/// </summary>
/// <remarks>
/// Display formatting under B-019 and nothing more: the metres arrive on the element from the
/// pipeline (`fleet-pipeline` B-032, B-033), and the age subtracts two instants the page was
/// handed. Here rather than in the card, so the view sets text it was given and a test reaches the
/// arithmetic without a page.
/// </remarks>
public static class FleetCardText
{
    /// <summary>What a value reads when there is none to show: the dash the description's cells use.</summary>
    public const string Missing = "\u2014";

    /// <summary>A distance in kilometres, to a tenth.</summary>
    /// <param name="metres">The distance as the pipeline carries it, or absent.</param>
    /// <returns>"12.4 km", or <see cref="Missing"/>.</returns>
    public static string Distance(Option<double> metres) =>
        metres.Match(
            static value => (value / MetresPerKilometre).ToString("#,0.0", CultureInfo.InvariantCulture) + " km",
            static () => Missing);

    /// <summary>How long before the observed instant the vehicle last made contact (B-031).</summary>
    /// <param name="observed">The instant the provider last reported, never the wall clock.</param>
    /// <param name="lastContact">When the vehicle was last heard from.</param>
    /// <returns>"42s", "4m 12s" or "1h 03m"; <see cref="Missing"/> before any poll has landed.</returns>
    /// <remarks>
    /// Measured from the observed instant so a replay reads as it did when it was recorded. A contact
    /// reported after the observed instant reads as none elapsed, not as a negative age.
    /// </remarks>
    public static string Age(DateTimeOffset observed, DateTimeOffset lastContact)
    {
        if (observed == DateTimeOffset.MinValue)
        {
            return Missing;
        }

        var elapsed = observed - lastContact;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return elapsed.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int) elapsed.TotalHours}h {elapsed.Minutes:00}m")
            : elapsed.TotalMinutes >= 1
                ? string.Create(CultureInfo.InvariantCulture, $"{elapsed.Minutes}m {elapsed.Seconds:00}s")
                : string.Create(CultureInfo.InvariantCulture, $"{elapsed.Seconds}s");
    }

    private const double MetresPerKilometre = 1000;
}
