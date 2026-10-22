using System.Reactive.Concurrency;
using DynamicData;
using LanguageExt;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.AisStream;
using Transporter.Integrations.AisStream.Messages;
using Transporter.Recording;

namespace Transporter.Tracking.Sources;

/// <summary>Feeds the vessel strategy's own cache from a paced recording.</summary>
internal sealed class VesselReplayPlayback
{
    public VesselReplayPlayback(
        IRecordingPacer pacer,
        SourceCache<VesselSnapshot, string> snapshots,
        IObservedClockWriter clock,
        ISchedulerProvider schedulers)
    {
        _pacer = pacer;
        _snapshots = snapshots;
        _clock = clock;
        _schedulers = schedulers;
    }

    /// <summary>Starts playback for as long as the strategy is selected.</summary>
    /// <returns>The playback lifetime.</returns>
    public IDisposable Start() =>
        _schedulers.BackgroundThread.ScheduleAsync((_, cancellation) => Run(cancellation));

    /// <summary>Releases and applies the next recorded provider message.</summary>
    /// <param name="cancellationToken">Stops a pending playback wait.</param>
    /// <returns>The pending frame operation.</returns>
    internal async Task Next(CancellationToken cancellationToken)
    {
        var body = await _pacer.Next(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (_pacer.RewindCount != _rewindCount)
        {
            _rewindCount = _pacer.RewindCount;
            _timedThisPass.Clear();
            _latestReported = Option<DateTimeOffset>.None;
            _rebaseClock = true;
        }

        VesselMessageReader.Read(body).IfSome(Write);
    }

    private async Task Run(CancellationToken cancellationToken)
    {
        try
        {
            await _ownership.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Next(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _ownership.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void Write(VesselMessage message)
    {
        var firstTimed = message.Reported.IsSome && _timedThisPass.Add(message.Mmsi);
        if (_rebaseClock && message.Reported.IsSome)
        {
            message.Reported.IfSome(_clock.Observe);
            _rebaseClock = false;
            _latestReported = message.Reported;
        }

        _snapshots.Edit(updater =>
        {
            var held = updater.Lookup(message.Mmsi);
            var snapshot = held.HasValue ? held.Value : new VesselSnapshot { Mmsi = message.Mmsi };
            updater.AddOrUpdate(snapshot.Apply(message, firstTimed));
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

    private readonly IRecordingPacer _pacer;
    private readonly SourceCache<VesselSnapshot, string> _snapshots;
    private readonly IObservedClockWriter _clock;
    private readonly ISchedulerProvider _schedulers;
    private readonly SemaphoreSlim _ownership = new(1, 1);
    private readonly System.Collections.Generic.HashSet<string> _timedThisPass = [];
    private Option<DateTimeOffset> _latestReported;
    private long _rewindCount;
    private bool _rebaseClock;
}
