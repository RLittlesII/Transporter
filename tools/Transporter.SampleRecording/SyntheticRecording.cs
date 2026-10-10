using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Transporter.Recording;

namespace Transporter.SampleRecording;

/// <summary>
/// Writes a synthetic aircraft recording through the one writer of the format, so the application
/// runs and the replay path is developed before anyone holds a provider credential.
/// </summary>
/// <remarks>
/// <para>
/// Every value is invented, so the file is a fixture rather than operational data and needs no
/// scrubbing. The payload is the provider's own wire shape — positional rows as
/// <c>README.md</c> § "Response shape" indexes them — and never a serialization of a type in this
/// repository, because a recording holds what the provider sent.
/// </para>
/// <para>
/// One seed writes one byte-identical file: the fleet and every drawn value come from
/// <see cref="Random"/> seeded with it, and a seeded <see cref="Random"/> is the same sequence on
/// every machine and run. The length and the cadence are arguments; which poll carries which
/// hazard is derived from the length, so a shorter recording still carries all of them.
/// </para>
/// </remarks>
internal sealed class SyntheticRecording
{
    /// <summary>Initializes a new instance of the <see cref="SyntheticRecording"/> class.</summary>
    /// <param name="recorder">The one writer of the recorded line's shape, which composes each line.</param>
    /// <param name="seed">What every drawn value comes from, so the file is reproducible.</param>
    /// <param name="polls">How many payloads it holds.</param>
    /// <param name="interval">The spacing between them.</param>
    public SyntheticRecording(IRecordingWriter recorder, int seed, int polls, TimeSpan interval)
    {
        _recorder = recorder;
        _seed = seed;
        Polls = polls;
        Interval = interval;
    }

    /// <summary>How many payloads a recording holds unless an argument says otherwise.</summary>
    internal const int DefaultPolls = 40;

    /// <summary>How many aircraft carry no hazard and are there to fill the grid.</summary>
    internal const int Ordinary = 6;

    /// <summary>The spacing between payloads unless an argument says otherwise, which is the live poll interval.</summary>
    internal static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(15);

    /// <summary>The instant the recording begins, fixed rather than read from a clock so the file is reproducible.</summary>
    internal static readonly DateTimeOffset Begins = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Gets how many payloads it holds.</summary>
    public int Polls { get; }

    /// <summary>Gets the spacing between payloads.</summary>
    public TimeSpan Interval { get; }

    /// <summary>Gets how much of the provider's own time it covers, which is what the startup check measures.</summary>
    public TimeSpan Span => Interval * (Polls - 1);

    /// <summary>Gets the poll carrying the one row the reader cannot read.</summary>
    public int Malformed => Polls / 10;

    /// <summary>Gets the last poll one aircraft reports a fresh contact on, so it is quiet for most of the recording.</summary>
    public int QuietensAfter => Polls * 3 / 10;

    /// <summary>Gets the poll that reports no aircraft at all.</summary>
    public int Empties => Polls / 2;

    /// <summary>Writes the whole recording.</summary>
    /// <returns>The running write.</returns>
    public async Task Write()
    {
        var random = new Random(_seed);
        var fleet = Roster(random);

        for (var poll = 0; poll < Polls; poll++)
        {
            var instant = Begins + (Interval * poll);

            await _recorder.Write(instant, Payload(poll, instant, fleet)).ConfigureAwait(false);

            foreach (var aircraft in fleet)
            {
                if (aircraft.Quietens && poll >= QuietensAfter)
                {
                    continue;
                }

                Advance(aircraft, random);
            }
        }
    }

    /// <summary>
    /// The fleet, hazard carriers first.
    /// </summary>
    /// <param name="random">Where each starting position, heading and speed comes from.</param>
    /// <returns>The aircraft, in the order their rows are written.</returns>
    /// <remarks>
    /// A sample holding only well-formed rows exercises none of the reader's claims, so each
    /// hazard the live reader answers is carried by an aircraft named for it.
    /// </remarks>
    private static IReadOnlyList<SyntheticAircraft> Roster(Random random)
    {
        var fleet = new List<SyntheticAircraft>
        {
            new() { Icao24 = "a1b1c1", Callsign = "TRN101  ", Country = "Westmarch", Squawk = "1234", Category = 3 },
            new() { Icao24 = "a2b2c2", Callsign = "TRN202  ", Country = "Eastvale", Squawk = "4321", Category = 0, OnGround = true },
            new() { Icao24 = "a3b3c3", Callsign = "TRN303  ", Country = "Northreach", Squawk = "2143", Extended = false },
            new() { Icao24 = "a4b4c4", Callsign = "        ", Country = "Southmere", Squawk = "3412", Category = 4 },
            new() { Icao24 = "a5b5c5", Callsign = "TRN505  ", Country = "Westmarch", Squawk = "0123", Category = 3 },
            new() { Icao24 = "a6b6c6", Callsign = "TRN606  ", Country = "Eastvale", Squawk = "1432", Category = 3, Quietens = true },
            new() { Icao24 = "a7b7c7", Callsign = "TRN707  ", Country = "Northreach", Squawk = "4123", Category = 3, Unreadable = true },
        };

        fleet.AddRange(Enumerable.Range(0, Ordinary).Select(static index => new SyntheticAircraft
        {
            Icao24 = $"c{index:x}d{index:x}e0",
            Callsign = $"TRN{index + 1:000}  ",
            Country = index % 2 == 0 ? "Westmarch" : "Southmere",
            Squawk = $"{4000 + index}",
            Category = index % 2 == 0 ? 3 : 4,
        }));

        foreach (var aircraft in fleet)
        {
            Positioned(aircraft, random);
        }

        return fleet;
    }

    /// <summary>Puts an aircraft somewhere inside the box the demo polls, on a heading.</summary>
    /// <param name="aircraft">The aircraft to place.</param>
    /// <param name="random">Where the drawn values come from.</param>
    /// <remarks>
    /// An aircraft on the ground reports no speed at all rather than a speed of zero, which is the
    /// absent-against-present-zero pair the reader's claims turn on.
    /// </remarks>
    private static void Positioned(SyntheticAircraft aircraft, Random random)
    {
        aircraft.Latitude = Rounded(28.8 + (random.NextDouble() * 1.6), 4);
        aircraft.Longitude = Rounded(-96.0 + (random.NextDouble() * 1.8), 4);
        aircraft.TrueTrack = Rounded(random.NextDouble() * 360, 1);
        aircraft.Velocity = Rounded(120 + (random.NextDouble() * 140), 1);
        aircraft.BarometricAltitude = Rounded(600 + (random.NextDouble() * 9_000), 0);
        aircraft.VerticalRate = Rounded((random.NextDouble() * 10) - 5, 1);

        if (!aircraft.OnGround)
        {
            return;
        }

        aircraft.Velocity = null;
        aircraft.BarometricAltitude = 0;
        aircraft.VerticalRate = 0;
    }

    /// <summary>Writes one positional row.</summary>
    /// <param name="json">Where the row is written.</param>
    /// <param name="aircraft">The aircraft the row reports.</param>
    /// <param name="contact">The instant this row last heard from the aircraft, which is frozen once it goes quiet.</param>
    /// <remarks>
    /// The index order is <c>README.md</c> § "Response shape". Index 12 is <c>sensors</c> and is
    /// null, as the provider sends it unless sensor information was requested; index 17 is present
    /// only on an eighteen-element row, which is what an absent category means on the wire.
    /// </remarks>
    private static void Row(Utf8JsonWriter json, SyntheticAircraft aircraft, long contact)
    {
        json.WriteStartArray();
        json.WriteStringValue(aircraft.Icao24);
        json.WriteStringValue(aircraft.Callsign);
        json.WriteStringValue(aircraft.Country);
        json.WriteNumberValue(contact);
        json.WriteNumberValue(contact);
        json.WriteNumberValue(aircraft.Longitude);
        json.WriteNumberValue(aircraft.Latitude);
        json.WriteNumberValue(aircraft.BarometricAltitude);

        if (aircraft.Unreadable)
        {
            // A flag element that is neither true nor false is one of the three ways a row is
            // unreadable, and the reader excludes it and counts it.
            json.WriteStringValue("yes");
        }
        else
        {
            json.WriteBooleanValue(aircraft.OnGround);
        }

        if (aircraft.Velocity is { } velocity)
        {
            json.WriteNumberValue(velocity);
        }
        else
        {
            json.WriteNullValue();
        }

        json.WriteNumberValue(aircraft.TrueTrack);
        json.WriteNumberValue(aircraft.VerticalRate);
        json.WriteNullValue();
        json.WriteNumberValue(aircraft.BarometricAltitude + 25);
        json.WriteStringValue(aircraft.Squawk);
        json.WriteBooleanValue(false);
        json.WriteNumberValue(0);

        if (aircraft.Extended)
        {
            if (aircraft.Category is { } category)
            {
                json.WriteNumberValue(category);
            }
            else
            {
                json.WriteNullValue();
            }
        }

        json.WriteEndArray();
    }

    /// <summary>Rounds a drawn value, so the same seed writes the same digits.</summary>
    /// <param name="value">The value.</param>
    /// <param name="digits">How many decimal places to keep.</param>
    /// <returns>The rounded value.</returns>
    private static double Rounded(double value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    /// <summary>Composes one payload, in the shape the provider answers a states request with.</summary>
    /// <param name="poll">Which poll it is, which decides what the payload reports.</param>
    /// <param name="instant">The instant it reports, which becomes the observed instant downstream.</param>
    /// <param name="fleet">The whole fleet, of which it reports whatever is audible.</param>
    /// <returns>The payload as the provider would have sent it.</returns>
    private string Payload(int poll, DateTimeOffset instant, IReadOnlyList<SyntheticAircraft> fleet)
    {
        var reported = instant.ToUnixTimeSeconds();
        var buffer = new ArrayBufferWriter<byte>();

        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("time", reported);
            json.WriteStartArray("states");

            foreach (var aircraft in fleet)
            {
                if (!Reports(aircraft, poll))
                {
                    continue;
                }

                Row(json, aircraft, Contact(aircraft, poll, reported));
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Whether an aircraft is reported on a poll.</summary>
    /// <param name="aircraft">The aircraft.</param>
    /// <param name="poll">Which poll it is.</param>
    /// <returns><see langword="true"/> when the payload carries its row.</returns>
    private bool Reports(SyntheticAircraft aircraft, int poll) =>
        poll != Empties && (!aircraft.Unreadable || poll == Malformed);

    /// <summary>The instant a row reports as its last contact.</summary>
    /// <param name="aircraft">The aircraft the row reports.</param>
    /// <param name="poll">Which poll it is.</param>
    /// <param name="reported">The instant the payload reports.</param>
    /// <returns>The payload's instant, or the instant the aircraft went quiet.</returns>
    private long Contact(SyntheticAircraft aircraft, int poll, long reported) =>
        aircraft.Quietens && poll > QuietensAfter
            ? (Begins + (Interval * QuietensAfter)).ToUnixTimeSeconds()
            : reported;

    /// <summary>Moves an aircraft along its track by one interval's worth of flight.</summary>
    /// <param name="aircraft">The aircraft to move.</param>
    /// <param name="random">Where the heading and speed drift come from.</param>
    private void Advance(SyntheticAircraft aircraft, Random random)
    {
        if (aircraft.Velocity is not { } velocity)
        {
            return;
        }

        var radians = aircraft.TrueTrack * Math.PI / 180;
        var degrees = velocity * Interval.TotalSeconds / MetresPerDegree;

        aircraft.Latitude = Rounded(aircraft.Latitude + (degrees * Math.Cos(radians)), 4);
        aircraft.Longitude = Rounded(aircraft.Longitude + (degrees * Math.Sin(radians)), 4);
        aircraft.TrueTrack = Rounded((aircraft.TrueTrack + (random.NextDouble() * 4) - 2 + 360) % 360, 1);
        aircraft.BarometricAltitude = Rounded(aircraft.BarometricAltitude + (aircraft.VerticalRate * Interval.TotalSeconds), 0);
        aircraft.Velocity = Rounded(velocity + (random.NextDouble() * 6) - 3, 1);
    }

    /// <summary>Metres per degree of latitude, near enough for a demo's positions.</summary>
    private const double MetresPerDegree = 111_320;

    private readonly IRecordingWriter _recorder;
    private readonly int _seed;
}
