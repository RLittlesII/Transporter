using System;
using System.Collections.Generic;
using System.Globalization;
using LanguageExt;
using Transponder.Model;
using Transponder.Tracking.Fleet;

namespace Transponder.Tracking.Sources;

/// <summary>
/// What the aircraft source offers a view: eight columns, three of them sortable, one grouping,
/// three filter choices, a card titled by the callsign with four readouts, and fourteen detail lines
/// (fleet-pipeline B-020, B-029, B-036, B-042, B-043).
/// </summary>
/// <remarks>
/// Every comparer, every grouping key and the first four selectors read <see cref="TransportVehicle"/>
/// members only, which is what keeps a cast out of the pipeline and the grid (B-010, B-022): a value
/// only this source reports reaches a column as an answer on the base —
/// <see cref="TransportVehicle.Label"/> and <see cref="TransportVehicle.GroupKey"/> — wherever the
/// column sorts, groups or filters.
/// <para>
/// B-022's one exception holds the casts in this file, and nothing else does: the filter choices,
/// the readout cells, the two deltas and the detail lines. <see cref="Aircraft.OnGround"/>, the altitude, the
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
            Value = static vehicle => DisplayUnit.Feet.Cell(Altitude(vehicle)),
        };
        var groundSpeed = new FleetColumn
        {
            Name = "Ground speed",
            Value = static vehicle => DisplayUnit.Knots.Cell(Speed(vehicle)),
        };
        var heading = new FleetColumn
        {
            Name = "Heading",
            Value = static vehicle => Track(vehicle).Match(
                static track => (Math.Round(track, MidpointRounding.AwayFromZero) % DegreesInATurn).ToString("000", CultureInfo.InvariantCulture) + "\u00B0",
                static () => DisplayUnit.Missing),
        };
        var verticalRate = new FleetColumn
        {
            Name = "Vertical rate",
            Value = static vehicle => DisplayUnit.FeetPerMinute.Cell(Rate(vehicle)),
        };
        var lastContact = new FleetColumn
        {
            Name = "Last contact",
            Value = static vehicle => vehicle.LastContact.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Comparer = Comparer<TransportVehicle>.Create(static (left, right) => left.LastContact.CompareTo(right.LastContact)),
        };
        var position = new FleetColumn
        {
            Name = "Position",
            Value = static vehicle => vehicle.Position.Match(
                static fix => string.Create(CultureInfo.InvariantCulture, $"{fix.Latitude:0.000}, {fix.Longitude:0.000}"),
                static () => "no fix"),
        };

        return new FleetSourceDescription
        {
            Columns =
            [
                callsign,
                originCountry,
                lastContact,
                position,
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
                        Delta = new FleetDelta(static (replaced, current) => DisplayUnit.Feet.Change(Altitude(replaced), Altitude(current))),
                    },
                    new FleetReadout
                    {
                        Column = groundSpeed,
                        Delta = new FleetDelta(static (replaced, current) => DisplayUnit.Knots.Change(Speed(replaced), Speed(current))),
                    },
                    new FleetReadout { Column = heading },
                    new FleetReadout { Column = verticalRate },
                ],
            },
            Detail =
            [
                new FleetColumn { Name = "ICAO24", Value = static vehicle => vehicle.Key },
                callsign,
                originCountry,
                new FleetColumn { Name = "Squawk", Value = static vehicle => Squawk(vehicle).IfNone(DisplayUnit.Missing) },
                new FleetColumn
                {
                    Name = "Category",
                    Value = static vehicle => Category(vehicle).Match(
                        static category => category.ToString(CultureInfo.InvariantCulture),
                        static () => DisplayUnit.Missing),
                },
                altitude,
                new FleetColumn { Name = "GPS altitude", Value = static vehicle => DisplayUnit.Feet.Cell(GeometricAltitude(vehicle)) },
                groundSpeed,
                heading,
                verticalRate,
                new FleetColumn
                {
                    Name = "On the ground",
                    Value = static vehicle => OnGround(vehicle).Match(static grounded => grounded ? "Yes" : "No", static () => DisplayUnit.Missing),
                },
                position,
                new FleetColumn
                {
                    Name = "Position source",
                    Value = static vehicle => Source(vehicle).Match(static source => source.ToString(), static () => DisplayUnit.Missing),
                },
                lastContact,
            ],
        };
    }

    // The readers are B-022's exception: each reads a value only an aircraft reports, in the unit
    // the vehicle keeps, and the display unit converts it.
    private static Option<double> Altitude(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.BarometricAltitude : Option<double>.None;

    private static Option<double> Speed(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.Velocity : Option<double>.None;

    private static Option<double> Track(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.TrueTrack : Option<double>.None;

    private static Option<double> Rate(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.VerticalRate : Option<double>.None;

    private static Option<double> GeometricAltitude(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.GeometricAltitude : Option<double>.None;

    private static Option<string> Squawk(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.Squawk : Option<string>.None;

    private static Option<int> Category(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.Category : Option<int>.None;

    private static Option<bool> OnGround(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.OnGround : Option<bool>.None;

    private static Option<PositionSource> Source(TransportVehicle vehicle) =>
        vehicle is Aircraft aircraft ? aircraft.PositionSource : Option<PositionSource>.None;

    private const double DegreesInATurn = 360;
}
