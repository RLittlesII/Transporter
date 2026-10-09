using System;
using System.Collections.Generic;
using System.Globalization;
using LanguageExt;
using ReactiveMarbles.Mvvm;
using Transponder.Model;

namespace Transponder.Features.Fleet.ViewModels;

/// <summary>The detail pane: the one surface that learns which kind of vehicle it has (B-013, B-019).</summary>
/// <remarks>
/// Everything else in the application reads a <see cref="TransportVehicle"/> through its
/// description; this type alone matches on the subclass, because a pane worth opening shows what
/// only that kind of vehicle reports (ADR-0005 item 6). Each canonical unit is converted by a member
/// named for the unit it produces, never inside a binding or a format string (B-019), and the
/// vehicle it reads is never written.
/// </remarks>
public sealed class FleetDetailViewModel : RxObject
{
    /// <summary>Gets or sets the vehicle the pane shows, absent when nothing is selected (B-014).</summary>
    /// <remarks>Set by <see cref="FleetViewModel.Selected"/> and by nothing else; not a binding target, so it stays an <c>Option</c>.</remarks>
    public Option<TransportVehicle> Vehicle
    {
        get;
        set
        {
            RaiseAndSetIfChanged(ref field, value);
            RaisePropertyChanged(nameof(IsEmpty));
            RaisePropertyChanged(nameof(Title));
            RaisePropertyChanged(nameof(Rows));
        }
    }

    /// <summary>Gets a value indicating whether the pane has nothing to show (B-014).</summary>
    public bool IsEmpty => Vehicle.IsNone;

    /// <summary>Gets the pane's heading: the vehicle's label, empty when there is none.</summary>
    public string Title => Vehicle.Match(static vehicle => vehicle.Label, static () => string.Empty);

    /// <summary>Gets the pane's lines, derived from <see cref="Vehicle"/> each time it is read and empty when there is none (B-013).</summary>
    public IReadOnlyList<FleetDetailRow> Rows => Vehicle.Match(Project, static () => []);

    /// <summary>The lines for one vehicle: the subclass's own fields where it is one this pane knows.</summary>
    /// <param name="vehicle">The vehicle.</param>
    /// <returns>Its lines, in reading order.</returns>
    private static IReadOnlyList<FleetDetailRow> Project(TransportVehicle vehicle) => vehicle switch
    {
        Aircraft aircraft =>
        [
            new("ICAO24", aircraft.Key),
            new("Callsign", aircraft.Callsign.IfNone(FleetCardText.Missing)),
            new("Country", aircraft.OriginCountry),
            new("Squawk", aircraft.Squawk.IfNone(FleetCardText.Missing)),
            new("Category", aircraft.Category.Match(static category => category.ToString(CultureInfo.InvariantCulture), static () => FleetCardText.Missing)),
            new("Baro altitude", Feet(aircraft.BarometricAltitude)),
            new("GPS altitude", Feet(aircraft.GeometricAltitude)),
            new("Ground speed", Knots(aircraft.Velocity)),
            new("Heading", Degrees(aircraft.TrueTrack)),
            new("Vertical rate", FeetPerMinute(aircraft.VerticalRate)),
            new("On ground", aircraft.OnGround ? "Yes" : "No"),
            new("Position", Position(aircraft.Position)),
            new("Position source", aircraft.PositionSource.Match(static source => source.ToString(), static () => FleetCardText.Missing)),
            new("Last contact", Instant(aircraft)),
        ],
        _ =>
        [
            new("Key", vehicle.Key),
            new("Position", Position(vehicle.Position)),
            new("Last contact", Instant(vehicle)),
        ],
    };

    /// <summary>Metres to feet, for an altitude (B-019).</summary>
    /// <param name="metres">The canonical altitude.</param>
    /// <returns>"35,000 ft", or the missing mark.</returns>
    private static string Feet(Option<double> metres) => Whole(metres.Map(static value => value / MetresPerFoot), "ft");

    /// <summary>Metres per second to knots, for a ground speed (B-019).</summary>
    /// <param name="metresPerSecond">The canonical speed.</param>
    /// <returns>"452 kt", or the missing mark.</returns>
    private static string Knots(Option<double> metresPerSecond) =>
        Whole(metresPerSecond.Map(static value => value * SecondsPerHour / MetresPerNauticalMile), "kt");

    /// <summary>Metres per second to feet per minute, for a vertical rate (B-019).</summary>
    /// <param name="metresPerSecond">The canonical rate, negative when descending.</param>
    /// <returns>"−1,200 ft/min", or the missing mark.</returns>
    private static string FeetPerMinute(Option<double> metresPerSecond) =>
        Whole(metresPerSecond.Map(static value => value * SecondsPerMinute / MetresPerFoot), "ft/min");

    /// <summary>A track in whole degrees; no conversion, only display.</summary>
    /// <param name="degrees">The track clockwise from north.</param>
    /// <returns>"275°", or the missing mark.</returns>
    private static string Degrees(Option<double> degrees) =>
        degrees.Match(
            static value => Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "°",
            static () => FleetCardText.Missing);

    private static string Position(Option<GeoPosition> position) =>
        position.Match(
            static fix => string.Create(CultureInfo.InvariantCulture, $"{fix.Latitude:0.0000}, {fix.Longitude:0.0000}"),
            static () => FleetCardText.Missing);

    private static string Instant(TransportVehicle vehicle) =>
        vehicle.LastContact.UtcDateTime.ToString("HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

    private static string Whole(Option<double> value, string unit) =>
        value.Match(
            number => Math.Round(number, MidpointRounding.AwayFromZero).ToString(WholeFormat, CultureInfo.InvariantCulture) + " " + unit,
            static () => FleetCardText.Missing);

    private const double MetresPerFoot = 0.3048;
    private const double MetresPerNauticalMile = 1852;
    private const double SecondsPerHour = 3600;
    private const double SecondsPerMinute = 60;
    private const string WholeFormat = "#,0;−#,0;0";
}
