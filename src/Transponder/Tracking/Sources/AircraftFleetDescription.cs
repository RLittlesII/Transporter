using System;
using System.Collections.Generic;
using System.Globalization;
using LanguageExt;
using Transponder.Model;
using Transponder.Tracking.Fleet;

namespace Transponder.Tracking.Sources;

/// <summary>
/// What the aircraft source offers a view: eight columns, three of them sortable, one grouping,
/// three filter choices, and a card titled by the callsign with four readouts (fleet-pipeline
/// B-020, B-029, B-036, B-042).
/// </summary>
/// <remarks>
/// Every comparer, every grouping key and the first four selectors read <see cref="TransportVehicle"/>
/// members only, which is what keeps a cast out of the pipeline and the grid (B-010, B-022): a value
/// only this source reports reaches a column as an answer on the base —
/// <see cref="TransportVehicle.Label"/> and <see cref="TransportVehicle.GroupKey"/> — wherever the
/// column sorts, groups or filters.
/// <para>
/// B-022's one exception holds the casts in this file, and nothing else does: the filter choices,
/// the four readout cells and the two deltas. <see cref="Aircraft.OnGround"/>, the altitude, the
/// speed, the track and the vertical rate reach no member of the base, and promoting them would
/// invent a semantic every future source has to answer, so what needs them is built here — in the
/// per-source file a swap replaces, where a cast cannot outlive the source that needed it. A consumer
/// still sees a predicate, a cell and a delta over the base, which is why the control and the card
/// are written once (B-021).
/// </para>
/// </remarks>
internal static class AircraftFleetDescription
{
    /// <summary>Gets the description, built once because nothing in it varies at runtime.</summary>
    public static FleetSourceDescription Offered { get; } = Describe();

    /// <summary>Converts metres to feet, the unit the altitude cell shows.</summary>
    /// <param name="metres">A length in metres, as the vehicle keeps it (ADR-0005 item 7).</param>
    /// <returns>The length in feet, unrounded.</returns>
    public static double Feet(double metres) => metres / MetresPerFoot;

    /// <summary>Converts metres per second to knots, the unit the ground-speed cell shows.</summary>
    /// <param name="metresPerSecond">A speed in metres per second.</param>
    /// <returns>The speed in knots, unrounded.</returns>
    public static double Knots(double metresPerSecond) => metresPerSecond * SecondsPerHour / MetresPerNauticalMile;

    /// <summary>Converts metres per second to feet per minute, the unit the vertical-rate cell shows.</summary>
    /// <param name="metresPerSecond">A rate of climb in metres per second, negative descending.</param>
    /// <returns>The rate in feet per minute, unrounded.</returns>
    public static double FeetPerMinute(double metresPerSecond) => metresPerSecond * SecondsPerMinute / MetresPerFoot;

    private static FleetSourceDescription Describe()
    {
        var callsign = new FleetColumn
        {
            Name = "Callsign",
            Value = static vehicle => vehicle.Label,
            Comparer = Comparer<TransportVehicle>.Create(static (left, right) => string.CompareOrdinal(left.Label, right.Label)),
        };
        var originCountry = new FleetColumn
        {
            Name = "Origin country",
            Value = static vehicle => vehicle.GroupKey,
            Comparer = Comparer<TransportVehicle>.Create(static (left, right) => string.CompareOrdinal(left.GroupKey, right.GroupKey)),
        };
        var altitude = new FleetColumn
        {
            Name = "Altitude",
            Value = static vehicle => AltitudeInFeet(vehicle).Match(static feet => Number(feet) + " ft", static () => Missing),
        };
        var groundSpeed = new FleetColumn
        {
            Name = "Ground speed",
            Value = static vehicle => SpeedInKnots(vehicle).Match(static knots => Number(knots) + " kt", static () => Missing),
        };
        var heading = new FleetColumn
        {
            Name = "Heading",
            Value = static vehicle => HeadingInDegrees(vehicle).Match(
                static degrees => degrees.ToString("000", CultureInfo.InvariantCulture) + "\u00B0",
                static () => Missing),
        };
        var verticalRate = new FleetColumn
        {
            Name = "Vertical rate",
            Value = static vehicle => RateInFeetPerMinute(vehicle).Match(static rate => Signed(rate) + " ft/min", static () => Missing),
        };

        return new FleetSourceDescription
        {
            Columns =
            [
                callsign,
                originCountry,
                new FleetColumn
                {
                    Name = "Last contact",
                    Value = static vehicle => vehicle.LastContact.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    Comparer = Comparer<TransportVehicle>.Create(static (left, right) => left.LastContact.CompareTo(right.LastContact)),
                },
                new FleetColumn
                {
                    Name = "Position",
                    Value = static vehicle => vehicle.Position.Match(
                        static fix => string.Create(CultureInfo.InvariantCulture, $"{fix.Latitude:0.000}, {fix.Longitude:0.000}"),
                        static () => "no fix"),
                },
                altitude,
                groundSpeed,
                heading,
                verticalRate,
            ],
            Groupings =
            [
                new FleetGrouping { Name = "Origin country", Key = static vehicle => vehicle.GroupKey },
            ],
            Filters =
            [
                new FleetFilterChoice { Name = "On the ground", Matches = static vehicle => vehicle is Aircraft { OnGround: true } },
                new FleetFilterChoice { Name = "Airborne", Matches = static vehicle => vehicle is Aircraft { OnGround: false } },
                new FleetFilterChoice { Name = "Reporting a position", Matches = static vehicle => vehicle.Position.IsSome },
            ],
            Card = new FleetCard
            {
                Title = callsign,
                Subtitle = originCountry,
                Readouts =
                [
                    new FleetReadout
                    {
                        Column = altitude,
                        Delta = new FleetDelta(static (replaced, current) => Change(AltitudeInFeet(replaced), AltitudeInFeet(current), "ft")),
                    },
                    new FleetReadout
                    {
                        Column = groundSpeed,
                        Delta = new FleetDelta(static (replaced, current) => Change(SpeedInKnots(replaced), SpeedInKnots(current), "kt")),
                    },
                    new FleetReadout { Column = heading },
                    new FleetReadout { Column = verticalRate },
                ],
            },
        };
    }

    // Each reader rounds to the unit its cell shows, so a cell and its delta read the same number
    // and "nothing changed" is decided at that precision (B-042).
    private static Option<long> AltitudeInFeet(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.BarometricAltitude.Map(static metres => Whole(Feet(metres))) : Option<long>.None;

    private static Option<long> SpeedInKnots(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.Velocity.Map(static speed => Whole(Knots(speed))) : Option<long>.None;

    private static Option<long> HeadingInDegrees(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.TrueTrack.Map(static track => Whole(track) % (long) DegreesInATurn) : Option<long>.None;

    private static Option<long> RateInFeetPerMinute(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.VerticalRate.Map(static rate => Whole(FeetPerMinute(rate))) : Option<long>.None;

    private static Option<string> Change(Option<long> replaced, Option<long> current, string unit) =>
        from before in replaced
        from after in current
        where after != before
        select (after > before ? "\u25B2 " : "\u25BC ") + Signed(after - before) + " " + unit;

    private static long Whole(double value) => (long) Math.Round(value, MidpointRounding.AwayFromZero);

    private static string Number(long value) =>
        value < 0 ? Minus + (-value).ToString("#,0", CultureInfo.InvariantCulture) : value.ToString("#,0", CultureInfo.InvariantCulture);

    private static string Signed(long value) => value > 0 ? "+" + Number(value) : Number(value);

    private const double MetresPerFoot = 0.3048;
    private const double MetresPerNauticalMile = 1852;
    private const double SecondsPerHour = 3600;
    private const double SecondsPerMinute = 60;
    private const double DegreesInATurn = 360;
    private const string Missing = "\u2014";
    private const string Minus = "\u2212";
}
