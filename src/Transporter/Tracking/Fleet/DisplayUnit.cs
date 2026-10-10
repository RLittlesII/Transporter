using System;
using System.Globalization;
using LanguageExt;

namespace Transporter.Tracking.Fleet;

/// <summary>
/// A unit a cell shows a canonical value in: the conversion from the unit the vehicle keeps, the
/// symbol, and whether the cell carries its sign (B-042).
/// </summary>
/// <remarks>
/// The cell and the change both read through one rounding, so a change subtracts the two numbers
/// the cells show and cannot disagree with them: "nothing changed" is decided at the precision the
/// cell shows. Formatted in the invariant culture, with U+2212 for a minus, so no locale writes a
/// decimal comma into a cell. The vehicle keeps metres (ADR-0005 item 7); the unit is a display
/// choice, which is why it lives beside the description and not on the model (§ 11 row 3).
/// </remarks>
internal sealed class DisplayUnit
{
    private DisplayUnit(string symbol, Func<double, double> convert, bool signed = false)
    {
        _symbol = symbol;
        _convert = convert;
        _signed = signed;
    }

    /// <summary>What a cell reads when the vehicle reported no value.</summary>
    public const string Missing = "\u2014";

    /// <summary>Gets feet, from metres.</summary>
    public static DisplayUnit Feet { get; } = new("ft", static metres => metres / MetresPerFoot);

    /// <summary>Gets knots, from metres per second.</summary>
    public static DisplayUnit Knots { get; } = new("kt", static speed => speed * SecondsPerHour / MetresPerNauticalMile);

    /// <summary>Gets feet per minute, from metres per second, signed because a rate climbs or descends.</summary>
    public static DisplayUnit FeetPerMinute { get; } = new("ft/min", static rate => rate * SecondsPerMinute / MetresPerFoot, signed: true);

    /// <summary>The cell for a value in the vehicle's unit.</summary>
    /// <param name="canonical">The value as the vehicle keeps it, or absent.</param>
    /// <returns>The cell — "29,528 ft" — or <see cref="Missing"/>.</returns>
    public string Cell(Option<double> canonical) =>
        Read(canonical).Match(
            value => (_signed ? Signed(value) : Number(value)) + " " + _symbol,
            static () => Missing);

    /// <summary>The change between two values at the precision the cell shows (B-042).</summary>
    /// <param name="replaced">The value before the update, or absent.</param>
    /// <param name="current">The value now, or absent.</param>
    /// <returns>"▲ +120 ft" or "▼ −120 ft"; none where the cell did not change or either side is absent.</returns>
    public Option<string> Change(Option<double> replaced, Option<double> current) =>
        Read(replaced).Bind(before => Read(current)
            .Filter(after => after != before)
            .Map(after => (after > before ? "\u25B2 " : "\u25BC ") + Signed(after - before) + " " + _symbol));

    private static string Number(long value) =>
        value < 0 ? Minus + (-value).ToString("#,0", CultureInfo.InvariantCulture) : value.ToString("#,0", CultureInfo.InvariantCulture);

    private static string Signed(long value) => value > 0 ? "+" + Number(value) : Number(value);

    private Option<long> Read(Option<double> canonical) =>
        canonical.Map(value => (long) Math.Round(_convert.Invoke(value), MidpointRounding.AwayFromZero));

    private const double MetresPerFoot = 0.3048;
    private const double MetresPerNauticalMile = 1852;
    private const double SecondsPerHour = 3600;
    private const double SecondsPerMinute = 60;
    private const string Minus = "\u2212";

    private readonly string _symbol;
    private readonly Func<double, double> _convert;
    private readonly bool _signed;
}
