using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rocket.Surgery.Airframe;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Tracking;

namespace Transponder.Integrations.OpenSky;

/// <summary>
/// Polls OpenSky, reads the positional rows, and applies each fetched set to its cache as one
/// differential update. That differential write is the step this repository exists to show: a
/// request/response feed becomes a reactive collection here and nowhere else
/// (<c>dynamic-data-pipeline</c> § "Where EditDiff lives").
/// </summary>
/// <remarks>
/// Takes the contract and the cache by constructor and constructs neither (B-015). The box and the
/// interval arrive as options rather than on the contract, which B-006 and B-024 both forbid.
/// </remarks>
internal sealed class AircraftSnapshotClient : IAircraftSnapshotClient, IDemandedPoll
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AircraftSnapshotClient"/> class.
    /// </summary>
    /// <param name="api">The API contract to poll (B-015).</param>
    /// <param name="cache">The plain keyed store each fetched set is diffed into (B-015, B-023).</param>
    /// <param name="clock">The write side of the observed clock, reported to as the set is applied (B-003).</param>
    /// <param name="options">The box and the interval (B-024, B-050).</param>
    /// <param name="schedulers">Where a poll is scheduled and time is read (§ 4 row 14).</param>
    /// <param name="logger">Where an excluded row and a throttle are recorded (B-022, B-028).</param>
    public AircraftSnapshotClient(
        IOpenSkyApi api,
        SourceCache<AircraftSnapshot, string> cache,
        IObservedClockWriter clock,
        IOptions<OpenSkyOptions> options,
        ISchedulerProvider schedulers,
        ILogger<AircraftSnapshotClient> logger)
    {
        _api = api;
        _cache = cache;
        _clock = clock;
        _options = options;
        _schedulers = schedulers;
        _logger = logger;
    }

    /// <summary>
    /// Gets the changeset stream the differential write produces — one add, update or remove per
    /// aircraft that actually changed, and nothing for one that did not (B-023).
    /// </summary>
    public IObservable<IChangeSet<AircraftSnapshot, string>> Snapshots => _cache.Connect();

    /// <summary>
    /// Gets the number of rows the last poll could not read. Excluded and counted, per B-022: a
    /// malformed row is data about the provider, not a reason to lose the rows beside it.
    /// </summary>
    public int UnreadableRows { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    /// Kept as ticks behind <see cref="Interlocked"/> rather than as the option itself, because the
    /// cadence writes it and the demanded poll reads it from another thread, and a struct wider than
    /// a word is not written atomically.
    /// </remarks>
    public Option<DateTimeOffset> LastPoll =>
        Interlocked.Read(ref _lastPoll) switch
        {
            NeverPolled => Option<DateTimeOffset>.None,
            var ticks => new DateTimeOffset(ticks, TimeSpan.Zero),
        };

    /// <inheritdoc/>
    /// <remarks>
    /// Where this is called from is not this client's business — the poller's hosting is § 5
    /// row 16's — and the thread is a stated choice rather than an ambient one: a poll runs on
    /// <see cref="ISchedulerProvider.BackgroundThread"/>. What is this client's is that the interval
    /// comes from its options, the wait after
    /// a throttle comes from the provider's header, and a failed poll neither completes nor
    /// error-terminates <see cref="Snapshots"/> (B-028, B-029).
    /// </remarks>
    public IDisposable Poll() =>
        _schedulers.BackgroundThread.ScheduleAsync(async (scheduler, cancellation) =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                if (Due() is { Ticks: > 0 } remaining)
                {
                    await scheduler.Sleep(remaining, cancellation).ConfigureAwait(false);

                    continue;
                }

                var wait = await Fetch(cancellation).ConfigureAwait(false);

                await scheduler.Sleep(wait, cancellation).ConfigureAwait(false);
            }
        });

    /// <inheritdoc/>
    /// <remarks>
    /// The cadence's own <see cref="Fetch"/>, so a demanded poll and a scheduled one are one call
    /// in one place, and the demanded one is stamped as the last poll the way a scheduled one is.
    /// </remarks>
    public Task PollNow(CancellationToken cancellationToken) => Fetch(cancellationToken);

    /// <summary>
    /// Fetches one set, applies it, and answers how long to wait before the next poll.
    /// </summary>
    /// <param name="cancellationToken">Stops the poll.</param>
    /// <returns>The interval normally; the delay the provider asked for after a throttle.</returns>
    /// <exception cref="InvalidOperationException">
    /// No bounding box is configured. B-050 gives the box no default, so a poll cannot invent one.
    /// Validation is supposed to have stopped the host before this (B-029); reaching it means the
    /// options were supplied without going through <c>AddOpenSky</c>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Two exceptions are caught here, and each is caught because B-028 and B-029 say what the next
    /// poll does rather than letting the stream end.
    /// </para>
    /// <para>
    /// <see cref="OpenSkyThrottledException"/> — the provider answered with a throttle instead of a
    /// payload. The transport read <c>X-Rate-Limit-Retry-After-Seconds</c> off the response before
    /// throwing (ADR-0008), so the wait is exactly the seconds OpenSky asked for and never a backoff
    /// of this code's own. Nothing is written to the cache for that poll, and no exception reaches a
    /// subscriber of <see cref="Snapshots"/> (B-028).
    /// </para>
    /// <para>
    /// Anything else — a timeout, a <c>5xx</c>, an unreadable body — costs this poll and nothing
    /// else, because a demo that loses its collection to one bad response ends on a blank grid
    /// (B-029). The filter reads <paramref name="cancellationToken"/> rather than the exception's
    /// type: a timed-out request surfaces as <see cref="System.Threading.Tasks.TaskCanceledException"/>,
    /// which <em>is</em> an <see cref="OperationCanceledException"/>, so excluding that type excluded
    /// the one failure the claim is named for. What must still end the loop is this client's own
    /// cancellation — the subscription being disposed — and that is the token, not the type.
    /// </para>
    /// </remarks>
    internal async Task<TimeSpan> Fetch(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var box = options.Box
                  ?? throw new InvalidOperationException(
                      $"No bounding box is configured. {OpenSkyOptions.Section}:{nameof(OpenSkyOptions.Box)} has no "
                      + "default, because a box nobody chose is a demo pointed at open ocean (B-050).");

        try
        {
            // The window B-053 refuses inside is measured from the call, so it is stamped before one
            // is made: an attempt is what spends the credit, whatever the provider answers with.
            Interlocked.Exchange(ref _lastPoll, _schedulers.BackgroundThread.Now.UtcTicks);

            var response = await _api.GetStates(
                    box.LatitudeMinimum,
                    box.LongitudeMinimum,
                    box.LatitudeMaximum,
                    box.LongitudeMaximum,
                    options.RequestsCategory,
                    cancellationToken)
                .ConfigureAwait(false);

            Apply(response);

            return options.PollInterval;
        }
        catch (OpenSkyThrottledException throttled)
        {
            // B-028.
            _logger.LogDebug(
                "OpenSky throttled the poll and asked for {RetryAfterSeconds} seconds; the next poll waits exactly that long.",
                throttled.RetryAfter.TotalSeconds);

            return throttled.RetryAfter;
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // B-029.
            _logger.LogWarning(failure, "An OpenSky poll failed; the next one is still due.");

            return options.PollInterval;
        }
    }

    /// <summary>
    /// Reads one positional row, field by field, by index.
    /// </summary>
    /// <param name="row">The row to read.</param>
    /// <returns>The snapshot, or <see langword="null"/> when the row cannot be read.</returns>
    /// <remarks>
    /// <para>
    /// The index order is README.md § "Response shape" and nothing else (B-016). Element count is
    /// read first, because two of B-022's three ways to be unreadable are answerable before any
    /// member is parsed.
    /// </para>
    /// <para>
    /// Index 12 is <c>sensors</c>, and the discard that reads past it is deliberate: B-021 keeps it
    /// off the snapshot and off every domain type, and requires the exclusion to be a statement
    /// rather than a gap nobody notices. It is also the one element whose value is a collection,
    /// which is part of why the snapshot can hold value equality.
    /// </para>
    /// <para>
    /// Two exceptions mean "this row is unreadable" rather than "this poll failed".
    /// <see cref="FormatException"/> is thrown by the reads above for a row with no
    /// <c>icao24</c>, a null where the index table declares no nullability, or a flag element that
    /// is neither <c>true</c> nor <c>false</c>. <see cref="InvalidOperationException"/> is what
    /// <see cref="JsonElement"/> itself throws when an element is of the wrong kind for the
    /// accessor — a row whose index 8 is the string <c>"yes"</c>, one of B-022's three defects.
    /// Either way the row is excluded and counted, and the rows beside it survive.
    /// </para>
    /// </remarks>
    private static AircraftSnapshot? Read(OpenSkyStateRow row)
    {
        if (row.Count is < 17 or > 18)
        {
            return null;
        }

        try
        {
            // B-021: index 12 is read past, and said so.
            _ = row[12];

            return new AircraftSnapshot
            {
                Icao24 = Text(row[0]) ?? throw new FormatException("A row with no icao24 identifies no aircraft."),
                Callsign = Callsign(row[1]),
                OriginCountry = Text(row[2]) ?? throw new FormatException("Index 2 declares no nullability."),
                TimePosition = Integer(row[3]),
                LastContact = Integer(row[4]).IfNone(static () => throw new FormatException("Index 4 declares no nullability.")),
                Longitude = Number(row[5]),
                Latitude = Number(row[6]),
                BarometricAltitude = Number(row[7]),
                OnGround = Flag(row[8]),
                Velocity = Number(row[9]),
                TrueTrack = Number(row[10]),
                VerticalRate = Number(row[11]),
                GeometricAltitude = Number(row[13]),
                Squawk = Squawk(row[14]),
                Spi = Flag(row[15]),
                PositionSource = Count(row[16]).IfNone(static () => throw new FormatException("Index 16 declares no nullability.")),
                Category = row.Count == 18 ? Count(row[17]) : Option<int>.None,
            };
        }
        catch (FormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // B-022.
            return null;
        }
    }

    /// <summary>The callsign with the wire's eight-character padding removed; padding alone is absent, never empty (B-019).</summary>
    private static Option<string> Callsign(JsonElement element) =>
        Text(element) is { } value && value.Trim() is { Length: > 0 } trimmed ? trimmed : Option<string>.None;

    /// <summary>The squawk as the four characters the provider sent, so <c>"0021"</c> never becomes the number 21 (B-020).</summary>
    private static Option<string> Squawk(JsonElement element) =>
        Text(element) is { } value ? value : Option<string>.None;

    private static string? Text(JsonElement element) =>
        element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element.GetString();

    /// <summary>A number, or absent. A <see langword="null"/> element is carried as absent and never substituted with <c>0</c> (B-017).</summary>
    private static Option<double> Number(JsonElement element) =>
        element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? Option<double>.None : element.GetDouble();

    private static Option<long> Integer(JsonElement element) =>
        element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? Option<long>.None : element.GetInt64();

    /// <summary>An integer, or absent — the shape B-018 turns on, where present-with-zero and absent are two values.</summary>
    private static Option<int> Count(JsonElement element) =>
        element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? Option<int>.None : element.GetInt32();

    private static bool Flag(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new FormatException("A flag element is true or false; anything else makes the row unreadable."),
        };

    /// <summary>
    /// Applies one fetched set: the whole set to the cache as a differential update, and the
    /// envelope's reported time to the observed clock.
    /// </summary>
    /// <param name="response">What the provider sent.</param>
    /// <remarks>
    /// One method, because ADR-0007 puts the instant's report beside the write that uses it: the
    /// value never travels through the snapshot, the cache or the seam (B-003). The instant is not
    /// a snapshot member on purpose — it differs on every poll, so carrying it would make the
    /// differ emit a change for every aircraft every interval.
    /// </remarks>
    private void Apply(OpenSkyStatesResponse response)
    {
        var snapshots = new List<AircraftSnapshot>(response.States.Count);
        var unreadable = 0;

        foreach (var row in response.States)
        {
            if (Read(row) is { } snapshot)
            {
                snapshots.Add(snapshot);
            }
            else
            {
                unreadable++;
            }
        }

        UnreadableRows = unreadable;

        if (unreadable > 0)
        {
            _logger.LogWarning(
                "{UnreadableRows} of {TotalRows} OpenSky rows could not be read and were excluded.",
                unreadable,
                response.States.Count);
        }

        _cache.EditDiff(snapshots, EqualityComparer<AircraftSnapshot>.Default);
        _clock.Observe(DateTimeOffset.FromUnixTimeSeconds(response.Time));
    }

    /// <summary>How long is left of the interval since the last poll, whoever made it; zero or less when one is due.</summary>
    /// <returns>What the cadence waits before polling again.</returns>
    /// <remarks>
    /// The cadence yields to a demanded poll rather than keeping a clock of its own, which is what
    /// makes B-053's guarantee hold in both directions: one poll per interval, whoever asked for it.
    /// </remarks>
    private TimeSpan Due() =>
        LastPoll.Match(
            last => _options.Value.PollInterval - (_schedulers.BackgroundThread.Now - last),
            static () => TimeSpan.Zero);

    /// <summary>The <see cref="_lastPoll"/> of a client that has not polled; not zero, which is a virtual clock's first instant.</summary>
    private const long NeverPolled = -1;

    private readonly IOpenSkyApi _api;
    private readonly SourceCache<AircraftSnapshot, string> _cache;
    private readonly IObservedClockWriter _clock;
    private readonly IOptions<OpenSkyOptions> _options;
    private readonly ISchedulerProvider _schedulers;
    private readonly ILogger<AircraftSnapshotClient> _logger;
    private long _lastPoll = NeverPolled;
}
