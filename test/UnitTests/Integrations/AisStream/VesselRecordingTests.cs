using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Reactive.Testing;
using Transporter.Integrations.AisStream;
using Transporter.Recording;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.UnitTests.Integrations.AisStream.Fixtures;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Integrations.AisStream;

public class VesselRecordingTests
{
    /// <summary>B-017: a recorder finishing after source disposal cannot update the live cache.</summary>
    /// <returns>The source-disposal assertions.</returns>
    [Fact(Timeout = 10000)]
    public async Task GivenAWriteFinishesAfterTheSocketIsSwappedOut_WhenItCompletes_ThenTheFrameIsNotApplied()
    {
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var socket = new ScriptedVesselSocket("only", []);
        using var cache = new SourceCache<VesselSnapshot, string>(static snapshot => snapshot.Mmsi);
        var clock = new ObservedClock();
        var writer = new HoldingWriter();
        VesselClient client = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers)
            .WithVessels(cache).WithWriter(clock).WithWriter((IRecordingWriter) writer);
        using var listening = client.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        await socket.Opened;

        socket.Receive(VesselPayloads.PositionReport);
        await writer.Started;
        listening.Dispose();
        writer.Release();
        await socket.Closed;

        cache.Count.Should().Be(0, "the swapped-out socket cannot publish a late recorded frame");
        clock.Current.Should().Be(DateTimeOffset.MinValue);
    }

    /// <summary>B-019: each received frame becomes one raw line at its own arrival instant.</summary>
    /// <returns>The recorded-message assertions.</returns>
    [Fact]
    public async Task GivenThreeReceivedMessages_WhenRecorded_ThenThereIsOneLinePerMessageAndEightSecondsOfArrivalSpacing()
    {
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var socket = new ScriptedVesselSocket("only", []);
        using var destination = new StringWriter(CultureInfo.InvariantCulture);
        var writer = new RecordingWriter(destination, NullLogger<RecordingWriter>.Instance);
        VesselClient client = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers)
            .WithWriter((IRecordingWriter) writer);
        using var listening = client.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        var frames = new[]
        {
            JsonNode.Parse(VesselPayloads.PositionReport)!.ToJsonString(),
            JsonNode.Parse(VesselPayloads.MovedPositionReport)!.ToJsonString(),
            JsonNode.Parse(VesselPayloads.StaticData)!.ToJsonString(),
        };

        await Task.Run(() => socket.Receive(frames[0]));
        scheduler.AdvanceBy(TimeSpan.FromSeconds(3).Ticks);
        await Task.Run(() => socket.Receive(frames[1]));
        scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks);
        await Task.Run(() => socket.Receive(frames[2]));

        var lines = destination.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3);
        var instants = new List<DateTimeOffset>();
        for (var index = 0; index < lines.Length; index++)
        {
            using var line = JsonDocument.Parse(lines[index]);
            instants.Add(line.RootElement.GetProperty("receivedAt").GetDateTimeOffset());
            line.RootElement.GetProperty("body").GetRawText().Should().Be(frames[index]);
        }

        (instants[^1] - instants[0]).Should().Be(TimeSpan.FromSeconds(8));
        socket.Sent.Should().ContainSingle("recording opened no second connection or subscription");
    }

    private sealed class HoldingWriter : IRecordingWriter
    {
        public Task Started => _started.Task;

        /// <inheritdoc/>
        public Task Write(DateTimeOffset receivedAt, string body)
        {
            _started.TrySetResult();
            return _pending.Task;
        }

        public void Release() => _pending.SetResult();

        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
