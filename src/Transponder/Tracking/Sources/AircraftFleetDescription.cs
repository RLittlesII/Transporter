using System;
using System.Collections.Generic;
using System.Globalization;
using LanguageExt;
using Transponder.Model;
using Transponder.Tracking.Fleet;

namespace Transponder.Tracking.Sources;

/// <summary>
/// What the aircraft source offers a view: four columns, three of them sortable, one grouping, and
/// three filter choices (fleet-pipeline B-020, B-029).
/// </summary>
/// <remarks>
/// Every selector and comparer reads <see cref="TransportVehicle"/> members only, which is what
/// keeps a cast out of the pipeline and the grid (B-010, B-022): a value only this source reports
/// reaches a column as an answer on the base — <see cref="TransportVehicle.Label"/> and
/// <see cref="TransportVehicle.GroupKey"/> — never as a downcast in a column.
/// <para>
/// The filter choices are B-022's one exception, and the only casts in this file.
/// <see cref="Aircraft.OnGround"/> reaches no member of the base and promoting it would invent a
/// semantic every future source has to answer, so the choice that needs it is built here — in the
/// per-source file a swap replaces, where a cast cannot outlive the source that needed it. A
/// consumer still sees a predicate over the base, which is why the control is written once (B-021).
/// </para>
/// </remarks>
internal static class AircraftFleetDescription
{
    /// <summary>Gets the description, built once because nothing in it varies at runtime.</summary>
    public static FleetSourceDescription Offered { get; } = new()
    {
        Columns =
        [
            new FleetColumn
            {
                Name = "Callsign",
                Value = static vehicle => vehicle.Label,
                Comparer = Comparer<TransportVehicle>.Create(static (left, right) => string.CompareOrdinal(left.Label, right.Label)),
            },
            new FleetColumn
            {
                Name = "Origin country",
                Value = static vehicle => vehicle.GroupKey,
                Comparer = Comparer<TransportVehicle>.Create(static (left, right) => string.CompareOrdinal(left.GroupKey, right.GroupKey)),
            },
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
    };
}
