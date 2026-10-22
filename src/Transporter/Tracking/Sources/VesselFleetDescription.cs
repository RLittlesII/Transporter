using System.Globalization;
using LanguageExt;
using Transporter.Model;
using Transporter.Tracking.Fleet;

namespace Transporter.Tracking.Sources;

/// <summary>Columns, filters, groups, card and detail for the vessel source.</summary>
internal static class VesselFleetDescription
{
    public static FleetSourceDescription Offered { get; } = WithSilentAfter(TimeSpan.FromMinutes(10));

    /// <summary>Builds the vessel offer with the configured removal threshold (B-027).</summary>
    /// <param name="threshold">How long a vessel remains after its last report.</param>
    /// <returns>The vessel description with removal after the threshold.</returns>
    public static FleetSourceDescription WithSilentAfter(TimeSpan threshold) => Describe() with { SilentAfter = threshold };

    private static FleetSourceDescription Describe()
    {
        var name = new FleetColumn
        {
            Name = "Name",
            Value = static vehicle => vehicle.Label,
            Comparer = Comparer<TransportVehicle>.Create(static (left, right) => string.CompareOrdinal(left.Label, right.Label)),
        };
        var shipType = new FleetColumn
        {
            Name = "Ship type",
            Value = static vehicle => vehicle.GroupKey,
            Comparer = Comparer<TransportVehicle>.Create(static (left, right) => string.CompareOrdinal(left.GroupKey, right.GroupKey)),
        };
        var flagState = new FleetColumn { Name = "Flag state", Value = Flag };
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
        var speed = new FleetColumn
        {
            Name = "Speed over ground",
            Value = static vehicle => Speed(vehicle).Match(static value => Unit(value, "kt"), static () => DisplayUnit.Missing),
        };
        var course = new FleetColumn
        {
            Name = "Course over ground",
            Value = static vehicle => Course(vehicle).Match(static value => Unit(value, "°"), static () => DisplayUnit.Missing),
        };
        var heading = new FleetColumn
        {
            Name = "True heading",
            Value = static vehicle => Heading(vehicle).Match(static value => Unit(value, "°"), static () => DisplayUnit.Missing),
        };

        return new FleetSourceDescription
        {
            Columns = [name, shipType, flagState, lastContact, position, speed, course, heading],
            Groupings =
            [
                new FleetGrouping { Name = "Ship type", Key = static vehicle => vehicle.GroupKey },
                new FleetGrouping { Name = "Flag state", Key = Flag },
            ],
            Filters =
            [
                new FleetFilterChoice { Name = "Moving", Matches = static vehicle => Speed(vehicle).Exists(static value => value > 0) },
                new FleetFilterChoice
                {
                    Name = "Named vessel",
                    Matches = static vehicle => vehicle is Vessel vessel && vessel.Name.Exists(static value => value.Trim().TrimEnd('@').Trim().Length > 0),
                },
                new FleetFilterChoice { Name = "Reporting a position", Matches = static vehicle => vehicle.Position.IsSome },
            ],
            Trail = new FleetTrailMeasure { Name = "Speed over ground", Minimum = 0, Maximum = 30, Value = Speed },
            Card = new FleetCard
            {
                Title = name,
                Subtitle = shipType,
                Readouts = [new FleetReadout { Column = speed }, new FleetReadout { Column = course }],
            },
            Detail =
            [
                new FleetColumn { Name = "MMSI", Value = static vehicle => vehicle.Key },
                name,
                shipType,
                flagState,
                speed,
                course,
                heading,
                position,
                lastContact,
            ],
        };
    }

    private static string Flag(TransportVehicle vehicle) => vehicle is Vessel vessel ? vessel.FlagState : "Unknown";

    private static Option<double> Speed(TransportVehicle vehicle) =>
        vehicle is Vessel vessel ? vessel.SpeedOverGround : Option<double>.None;

    private static Option<double> Course(TransportVehicle vehicle) =>
        vehicle is Vessel vessel ? vessel.CourseOverGround : Option<double>.None;

    private static Option<double> Heading(TransportVehicle vehicle) =>
        vehicle is Vessel vessel ? vessel.TrueHeading : Option<double>.None;

    private static string Unit(double value, string symbol) =>
        value.ToString("0.#", CultureInfo.InvariantCulture) + " " + symbol;
}
