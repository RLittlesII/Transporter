using System.Reactive.Concurrency;
using System.Text.Json;
using DynamicData;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.AisStream.Configuration;
using Transporter.Integrations.AisStream.Messages;
using Transporter.Integrations.OpenSky;
using Transporter.Tracking;

namespace Transporter.Integrations.AisStream;

internal sealed class VesselClient
{
    public VesselClient(
        Func<IVesselSocket> sockets,
        SourceCache<VesselSnapshot, string> vessels,
        IObservedClockWriter clock,
        IOptions<AisStreamOptions> options,
        IOptions<OpenSkyOptions> box,
        ISchedulerProvider schedulers,
        ILogger<VesselClient> logger)
    {
        _sockets = sockets;
        _vessels = vessels;
        _clock = clock;
        _options = options;
        _box = box;
        _schedulers = schedulers;
        _logger = logger;
    }

    public IDisposable Listen() =>
        _schedulers.BackgroundThread.ScheduleAsync((scheduler, cancellation) => Listen(scheduler, cancellation));

    private async Task Listen(IScheduler scheduler, CancellationToken cancellation)
    {
        var options = _options.Value;
        var key = options.ApiKey.IfNone(string.Empty);
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogError("No AISStream API key is configured. Set AisStream:ApiKey.");
            return;
        }

        try
        {
            await _connectionOwnership.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    try
                    {
                        await using var socket = _sockets.Invoke();
                        await socket.Open(options.Endpoint, cancellation).ConfigureAwait(false);
                        var box = _box.Value.Box ?? throw new InvalidOperationException("OpenSky:Box is absent.");
                        await socket.Send(VesselSubscription.Write(key, box), cancellation).ConfigureAwait(false);
                        await foreach (var frame in socket.Frames(cancellation).ConfigureAwait(false))
                        {
                            ReadConfirmation(frame);
                            VesselMessageReader.Read(frame).IfSome(Write);
                        }
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception)
                    {
                        _logger.LogWarning("The AISStream connection failed; it will reconnect while selected.");
                    }

                    await scheduler.Sleep(options.ReconnectAfter, cancellation).ConfigureAwait(false);
                }
            }
            finally
            {
                _connectionOwnership.Release();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }

    private void ReadConfirmation(string frame)
    {
        try
        {
            using var document = JsonDocument.Parse(frame);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("MessageType", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() == "SubscriptionConfirmation"
                && root.TryGetProperty("Message", out var message)
                && message.ValueKind == JsonValueKind.Object
                && message.TryGetProperty("CompressionEnabled", out var compression)
                && compression.ValueKind == JsonValueKind.False)
            {
                _logger.LogWarning("AISStream did not grant compression; the connection remains open.");
            }
        }
        catch (JsonException)
        {
        }
    }

    private void Write(VesselMessage message)
    {
        _vessels.Edit(updater =>
        {
            var held = updater.Lookup(message.Mmsi);
            var snapshot = held.HasValue ? held.Value : new VesselSnapshot { Mmsi = message.Mmsi };
            message.Reported.IfSome(instant =>
            {
                if (snapshot.Reported.Match(latest => instant > latest, static () => true))
                {
                    snapshot = snapshot with { Reported = instant };
                }
            });
            updater.AddOrUpdate(message switch
            {
                VesselPositionMessage position => snapshot with
                {
                    Position = position.Position,
                    SpeedOverGround = position.SpeedOverGround,
                    CourseOverGround = position.CourseOverGround,
                    TrueHeading = position.TrueHeading,
                },
                VesselStaticMessage staticData => snapshot with
                {
                    Name = staticData.Name,
                    ShipType = staticData.ShipType,
                },
                _ => snapshot,
            });
        });
        message.Reported.IfSome(instant =>
        {
            if (_latestReported.Match(latest => instant <= latest, static () => false))
            {
                return;
            }

            _latestReported = instant;
            _clock.Observe(instant);
        });
    }

    private readonly Func<IVesselSocket> _sockets;
    private readonly SourceCache<VesselSnapshot, string> _vessels;
    private readonly IObservedClockWriter _clock;
    private readonly IOptions<AisStreamOptions> _options;
    private readonly IOptions<OpenSkyOptions> _box;
    private readonly ISchedulerProvider _schedulers;
    private readonly ILogger<VesselClient> _logger;
    private readonly SemaphoreSlim _connectionOwnership = new(1, 1);
    private Option<DateTimeOffset> _latestReported;
}
