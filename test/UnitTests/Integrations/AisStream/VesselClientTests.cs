using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.AisStream;
using Transporter.Integrations.AisStream.Configuration;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Model;
using Transporter.Scheduling;
using Transporter.UnitTests.Integrations.AisStream.Fixtures;
using Transporter.UnitTests.Integrations.OpenSky;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Integrations.AisStream;

public class VesselClientTests
{
    /// <summary>B-001: the sent subscription reads the aircraft options rather than a compiled geography.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenAChangedAircraftBox_WhenTheClientSubscribes_ThenTheSentFrameUsesThatBox()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var events = new List<string>();
        var socket = new ScriptedVesselSocket("only", events);
        BoundingBox box = new BoundingBoxFixture().WithLatitudeMinimum(41.1).WithLatitudeMaximum(42.2)
            .WithLongitudeMinimum(-73.3).WithLongitudeMaximum(-72.4);
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers)
            .WithOptions(Options.Create(new OpenSkyOptions { Box = box }));

        // When
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));

        // Then
        socket.Sent.Should().ContainSingle();
        using var frame = JsonDocument.Parse(socket.Sent.Single());
        var boxes = frame.RootElement.GetProperty("BoundingBoxes");
        boxes.GetArrayLength().Should().Be(1);
        var corners = boxes[0];
        corners[0].EnumerateArray().Select(static value => value.GetDouble()).Should()
            .Equal(box.LatitudeMinimum, box.LongitudeMinimum);
        corners[1].EnumerateArray().Select(static value => value.GetDouble()).Should()
            .Equal(box.LatitudeMaximum, box.LongitudeMaximum);
    }

    /// <summary>B-005: reselecting vessels waits for the outgoing connection's asynchronous close.</summary>
    /// <returns>The completion of the serialized selection.</returns>
    [Fact(Timeout = 10000)]
    public async Task GivenAnOutgoingSocketStillClosing_WhenVesselsAreReselected_ThenTheNewSocketWaitsForItsClose()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var events = new List<string>();
        var first = new ScriptedVesselSocket("first", events) { HoldClose = true };
        var second = new ScriptedVesselSocket("second", events);
        var sockets = new Queue<IVesselSocket>([first, second]);
        VesselClient sut = new VesselClientFixture().WithSockets(sockets.Dequeue).WithProvider(schedulers);
        var outgoing = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        first.IsOpen.Should().BeTrue();

        // When
        await Task.Run(outgoing.Dispose);
        using var incoming = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        first.CloseStarted.Should().BeTrue();
        second.IsOpen.Should().BeFalse();
        await Task.Run(first.ReleaseClose);
        await second.Opened;

        // Then
        first.IsOpen.Should().BeFalse();
        second.IsOpen.Should().BeTrue();
        events.Should().ContainInOrder("first:closed", "second:open");
    }

    /// <summary>B-002: each connection subscribes before reading and sends exactly once.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenAKey_WhenAConnectionOpens_ThenTheSubscriptionIsTheFirstFrameAndTheOnlyOne()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var events = new List<string>();
        var first = new ScriptedVesselSocket("first", events);
        var second = new ScriptedVesselSocket("second", events);
        var sockets = new Queue<IVesselSocket>([first, second]);
        VesselClient sut = new VesselClientFixture().WithSockets(sockets.Dequeue).WithProvider(schedulers);

        // When
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        first.Sent.Should().ContainSingle();
        await Task.Run(first.ServiceCloses);
        await Task.Run(() => scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks));
        second.Sent.Should().ContainSingle();
        await Task.Run(() => second.Receive("{}"));

        // Then
        events.Should().ContainInOrder("first:open", "first:send", "first:read", "first:closed", "second:open", "second:send", "second:read");
        first.Sent.Should().ContainSingle();
        second.Sent.Should().ContainSingle();
        using var frame = JsonDocument.Parse(second.Sent.Single());
        frame.RootElement.GetProperty("APIKey").GetString().Should().Be("synthetic-credential");
        frame.RootElement.GetProperty("FilterMessageTypes").EnumerateArray().Select(static filter => filter.GetString())
            .Should().BeEquivalentTo(["PositionReport", "ShipStaticData"]);
    }

    /// <summary>B-004: an absent key reports its name and leaves selection operational.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenNoKey_WhenTheVesselStrategyIsSelected_ThenNoSocketOpensAnErrorNamesTheSettingAndAnotherStrategyStillSwapsIn()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var logger = new RecordingLogger<VesselClient>();
        var opened = 0;
        VesselClient sut = new VesselClientFixture()
            .WithOptions(Options.Create(new AisStreamOptions()))
            .WithProvider(schedulers)
            .WithLogger(logger)
            .WithSockets(() =>
            {
                opened++;
                throw new InvalidOperationException("No connection is allowed without a key.");
            });
        using var selected = new BehaviorSubject<bool>(true);
        var values = new List<string>();
        var completed = false;
        Exception? failure = null;
        using var stream = selected.Select(vessels => vessels
                ? Observable.Using(sut.Listen, _ => Observable.Never<string>())
                : Observable.Return("aircraft"))
            .Switch().Subscribe(values.Add, error => failure = error, () => completed = true);

        // When
        await Task.Run(() => scheduler.AdvanceBy(1));
        await Task.Run(() => selected.OnNext(false));

        // Then
        opened.Should().Be(0);
        logger.At(LogLevel.Error).Should().ContainSingle().Which.Should().Contain("AisStream:ApiKey");
        values.Should().Equal("aircraft");
        failure.Should().BeNull();
        completed.Should().BeFalse();
    }

    /// <summary>B-005: repeated subscription swaps close each outgoing connection before opening another.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenRepeatedSwaps_WhenEachCompletes_ThenAtMostOneSocketIsOpenAndTheOutgoingOneIsClosed()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var events = new List<string>();
        var first = new ScriptedVesselSocket("first", events);
        var second = new ScriptedVesselSocket("second", events);
        var sockets = new Queue<IVesselSocket>([first, second]);
        VesselClient sut = new VesselClientFixture().WithSockets(sockets.Dequeue).WithProvider(schedulers);
        using var selected = new BehaviorSubject<bool>(false);
        using var stream = selected.Select(vessels => vessels
                ? Observable.Using(sut.Listen, _ => Observable.Never<int>())
                : Observable.Never<int>())
            .Switch().Subscribe();

        // When
        await Task.Run(() => selected.OnNext(true));
        await Task.Run(() => scheduler.AdvanceBy(1));
        first.IsOpen.Should().BeTrue();
        await Task.Run(() => selected.OnNext(false));
        first.IsOpen.Should().BeFalse();
        await Task.Run(() => selected.OnNext(true));
        await Task.Run(() => scheduler.AdvanceBy(1));
        second.IsOpen.Should().BeTrue();
        await Task.Run(() => selected.OnNext(false));

        // Then
        second.IsOpen.Should().BeFalse();
        events.Should().ContainInOrder("first:open", "first:closed", "second:open", "second:closed");
        sockets.Should().BeEmpty();
    }

    /// <summary>B-005: a reconnect waits for asynchronous closure before spending a second connection.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenADroppedConnection_WhenTheClientReconnects_ThenTheOldSocketClosedBeforeTheNewOneOpened()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var events = new List<string>();
        var first = new ScriptedVesselSocket("first", events) { HoldClose = true };
        var second = new ScriptedVesselSocket("second", events);
        var sockets = new Queue<IVesselSocket>([first, second]);
        VesselClient sut = new VesselClientFixture().WithSockets(sockets.Dequeue).WithProvider(schedulers);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        first.IsOpen.Should().BeTrue();

        // When
        await Task.Run(first.ServiceCloses);
        await Task.Run(() => scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks));
        first.CloseStarted.Should().BeTrue();
        second.IsOpen.Should().BeFalse();
        await Task.Run(first.ReleaseClose);
        await Task.Run(() => scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks));

        // Then
        second.IsOpen.Should().BeTrue();
        events.Should().ContainInOrder("first:closing", "first:closed", "second:open");
    }

    /// <summary>B-005: swapping away cancels a reconnect already waiting on the injected scheduler.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenAPendingReconnect_WhenTheStrategyIsSwappedOut_ThenNoSocketOpensWhenTheDelayPasses()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var events = new List<string>();
        var first = new ScriptedVesselSocket("first", events);
        var second = new ScriptedVesselSocket("second", events);
        var sockets = new Queue<IVesselSocket>([first, second]);
        VesselClient sut = new VesselClientFixture().WithSockets(sockets.Dequeue).WithProvider(schedulers);
        var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        first.IsOpen.Should().BeTrue();
        await Task.Run(first.ServiceCloses);
        first.IsOpen.Should().BeFalse();

        // When
        await Task.Run(listening.Dispose);
        await Task.Run(() => scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks));

        // Then
        second.IsOpen.Should().BeFalse();
        sockets.Should().ContainSingle();
        events.Should().NotContain("second:open");
    }

    /// <summary>B-006: reconnecting resubscribes without clearing the injected cache.</summary>
    /// <param name="connectionLost">Whether receiving throws instead of ending normally.</param>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Theory]
    [ClassData(typeof(VesselDisconnectionCases))]
    public async Task GivenTheServiceClosesTheSocket_WhenTheClientReconnects_ThenItResubscribesAndTheHeldVesselsStay(bool connectionLost)
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var events = new List<string>();
        var first = new ScriptedVesselSocket("first", events);
        var second = new ScriptedVesselSocket("second", events);
        var sockets = new Queue<IVesselSocket>([first, second]);
        using var cache = new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi);
        VesselSnapshot held = new VesselSnapshotFixture();
        VesselSnapshot other = new VesselSnapshotFixture().WithMmsi("366999002");
        cache.AddOrUpdate([held, other]);
        VesselClient sut = new VesselClientFixture().WithSockets(sockets.Dequeue).WithProvider(schedulers).WithVessels(cache);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        first.IsOpen.Should().BeTrue();

        // When
        if (connectionLost)
        {
            await Task.Run(() => first.ServiceFails(new InvalidOperationException("The service lost the connection.")));
        }
        else
        {
            await Task.Run(first.ServiceCloses);
        }

        await Task.Run(() => scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks));

        // Then
        second.IsOpen.Should().BeTrue();
        second.Sent.Should().ContainSingle();
        cache.Lookup(held.Mmsi).Value.Should().BeSameAs(held);
        cache.Lookup(other.Mmsi).Value.Should().BeSameAs(other);
        cache.Count.Should().Be(2);
    }

    /// <summary>B-007: denying compression warns without ending or reopening the stream.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenAConfirmationWithoutCompression_WhenItArrives_ThenAWarningIsLoggedAndTheStreamStaysOpen()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var events = new List<string>();
        var socket = new ScriptedVesselSocket("only", events);
        var logger = new RecordingLogger<VesselClient>();
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers).WithLogger(logger);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        socket.IsOpen.Should().BeTrue();

        // When
        await Task.Run(() => socket.Receive(VesselPayloads.CompressionDenied));

        // Then
        logger.At(LogLevel.Warning).Should().ContainSingle().Which.Should().ContainEquivalentOf("compression");
        socket.IsOpen.Should().BeTrue();
        socket.Reads.Should().Be(2);
        socket.Sent.Should().ContainSingle();
    }

    /// <summary>B-011: an unrequested base-station report leaves the cache unchanged and reading continues.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenABaseStationReport_WhenItArrives_ThenHeldVesselsStayUnchangedAndTheStreamContinues()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var events = new List<string>();
        var socket = new ScriptedVesselSocket("only", events);
        using var cache = new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi);
        VesselSnapshot held = new VesselSnapshotFixture();
        cache.AddOrUpdate(held);
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers).WithVessels(cache);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        socket.IsOpen.Should().BeTrue();

        // When
        await Task.Run(() => socket.Receive(VesselPayloads.BaseStationReport));

        // Then
        cache.Lookup(held.Mmsi).Value.Should().BeSameAs(held);
        cache.Count.Should().Be(1);
        socket.Reads.Should().Be(2);
        socket.IsOpen.Should().BeTrue();
        socket.Sent.Should().ContainSingle();
    }

    /// <summary>B-003: connection failures cannot disclose a configured key through their exception text.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenAFailureContainingTheKey_WhenTheConnectionFails_ThenLogsDoNotDiscloseTheKey()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var logger = new RecordingLogger<VesselClient>();
        VesselClient sut = new VesselClientFixture().WithProvider(schedulers).WithLogger(logger)
            .WithSockets(static () => throw new InvalidOperationException("synthetic-credential"));

        // When
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));

        // Then
        logger.Entries.Should().NotBeEmpty();
        logger.Messages.Should().OnlyContain(static message => !message.Contains("synthetic-credential", StringComparison.Ordinal));
    }

    /// <summary>B-009: each position frame writes one keyed cache change.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenSuccessivePositionReports_WhenEachArrives_ThenEachIsOneAddOrUpdate()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var socket = new ScriptedVesselSocket("only", []);
        using var cache = new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi);
        var changes = new List<(ChangeReason Reason, string Key)>();
        using var subscription = cache.Connect().Subscribe(set =>
            changes.AddRange(set.Select(change => (change.Reason, change.Key))));
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers).WithVessels(cache);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));

        // When
        await Task.Run(() => socket.Receive(VesselPayloads.PositionReport));
        await Task.Run(() => socket.Receive(VesselPayloads.MovedPositionReport));
        await Task.Run(() => socket.Receive(VesselPayloads.SecondPositionReport));

        // Then
        changes.Should().Equal(
            (ChangeReason.Add, "366999001"),
            (ChangeReason.Update, "366999001"),
            (ChangeReason.Add, "366999002"));
        cache.Count.Should().Be(2);
        cache.Lookup("366999001").Value.Position.IfNone(default(Transporter.Model.GeoPosition)).Should().Be(new Transporter.Model.GeoPosition(29.71, -95.01));
        cache.Lookup("366999001").Value.SpeedOverGround.IfNone(0).Should().Be(13.4);
        socket.IsOpen.Should().BeTrue();
    }

    /// <summary>B-010: static data adds a vessel before a position is known.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenStaticDataForAnUnheldVessel_WhenItArrives_ThenItIsCached()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var socket = new ScriptedVesselSocket("only", []);
        using var cache = new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi);
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers).WithVessels(cache);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));

        // When
        await Task.Run(() => socket.Receive(VesselPayloads.StaticData));

        // Then
        var held = cache.Lookup("366999003");
        held.HasValue.Should().BeTrue();
        held.Value.Name.IfNone(string.Empty).Should().Be("GULF TRADER");
        held.Value.ShipType.IfNone(-1).Should().Be(70);
        held.Value.Position.IsNone.Should().BeTrue();
        cache.Count.Should().Be(1);
        socket.IsOpen.Should().BeTrue();
    }

    /// <summary>B-010: static data updates identity without erasing a position already held for that MMSI.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenPositionBeforeStaticData_WhenStaticDataArrives_ThenTheHeldPositionRemains()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var socket = new ScriptedVesselSocket("only", []);
        using var cache = new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi);
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers).WithVessels(cache);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        await Task.Run(() => socket.Receive(VesselPayloads.PositionBeforeStaticData));

        // When
        await Task.Run(() => socket.Receive(VesselPayloads.StaticData));

        // Then
        var held = cache.Lookup("366999003").Value;
        held.Position.IfNone(default(Transporter.Model.GeoPosition)).Should().Be(new Transporter.Model.GeoPosition(29.40, -94.90));
        held.Name.IfNone(string.Empty).Should().Be("GULF TRADER");
        cache.Count.Should().Be(1);
    }

    /// <summary>B-012: an unreadable frame leaves held state untouched and reading continues.</summary>
    /// <returns>The completion of the scripted socket operation.</returns>
    [Fact]
    public async Task GivenAnUnreadableFrame_WhenItArrives_ThenItIsSkippedAndTheStreamContinues()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var socket = new ScriptedVesselSocket("only", []);
        using var cache = new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi);
        VesselSnapshot held = new VesselSnapshotFixture();
        cache.AddOrUpdate(held);
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers).WithVessels(cache);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));

        // When
        await Task.Run(() => socket.Receive("{not JSON"));

        // Then
        cache.Lookup(held.Mmsi).Value.Should().BeSameAs(held);
        cache.Count.Should().Be(1);
        socket.IsOpen.Should().BeTrue();
        await Task.Run(() => socket.Receive(VesselPayloads.SecondPositionReport));
        cache.Lookup("366999002").HasValue.Should().BeTrue();
    }
}

/// <summary>Builds a synthetic cached vessel snapshot.</summary>
/// <remarks>Hand-written because the target record uses a required property rather than constructor parameters.</remarks>
internal sealed class VesselSnapshotFixture : AutoFixtureBase<VesselSnapshotFixture>
{
    public VesselSnapshotFixture WithMmsi(string mmsi) => With(ref _mmsi, mmsi);

    public static implicit operator VesselSnapshot(VesselSnapshotFixture fixture) => new() { Mmsi = fixture._mmsi };

    private string _mmsi = "366999001";
}
