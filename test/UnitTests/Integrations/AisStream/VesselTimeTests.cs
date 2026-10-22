using System.Reactive.Linq;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Reactive.Testing;
using Transporter.Integrations.AisStream;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.UnitTests.Integrations.AisStream.Fixtures;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Integrations.AisStream;

public class VesselTimeTests
{
    /// <summary>B-015: both supported message kinds advance time from provider UTC text.</summary>
    /// <param name="frame">A synthetic supported message.</param>
    /// <param name="reported">The provider's UTC text.</param>
    /// <param name="expected">The instant at tick precision.</param>
    /// <returns>The scripted receive operation.</returns>
    [Theory]
    [ClassData(typeof(VesselReportedTimeCases))]
    public async Task GivenProviderUtc_WhenSupportedMessagesArrive_ThenClockUsesReportedTime(string frame, string reported, DateTimeOffset expected)
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        ObservedClock clock = new ObservedClockFixture();
        var socket = new ScriptedVesselSocket("only", []);
        using var cache = new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi);
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers)
            .WithWriter(clock).WithVessels(cache);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));

        // When
        await Task.Run(() => socket.Receive(VesselPayloads.WithReportedTime(frame, reported)));

        // Then
        clock.Current.Should().Be(expected);
        cache.Items.Should().ContainSingle().Which.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(expected);
        socket.IsOpen.Should().BeTrue();
    }

    /// <summary>B-014, B-015: absent or malformed metadata never borrows a clock or the payload's UTC second.</summary>
    /// <param name="reportedJson">An invalid or absent metadata value.</param>
    /// <returns>The scripted receive operation.</returns>
    [Theory]
    [ClassData(typeof(VesselUnreadableTimeCases))]
    public async Task GivenUnreadableTime_WhenAReportArrives_ThenPositionUpdatesAndTimeStays(string reportedJson)
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        ObservedClock clock = new ObservedClockFixture();
        var socket = new ScriptedVesselSocket("only", []);
        using var cache = new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi);
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers)
            .WithWriter(clock).WithVessels(cache);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        await Task.Run(() => socket.Receive(VesselPayloads.WithReportedTime(
            VesselPayloads.PositionReport, "2026-10-20 14:32:25 +0000 UTC")));
        var expected = new DateTimeOffset(2026, 10, 20, 14, 32, 25, TimeSpan.Zero);
        var frame = JsonNode.Parse(VesselPayloads.WithReportedTime(VesselPayloads.MovedPositionReport, JsonNode.Parse(reportedJson)))!;
        frame["Message"]!["PositionReport"]!["Timestamp"] = 30;

        // When
        await Task.Run(() => socket.Receive(frame.ToJsonString()));

        // Then
        var held = cache.Lookup("366999001").Value;
        held.SpeedOverGround.IfNone(0).Should().Be(13.4);
        held.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(expected);
        clock.Current.Should().Be(expected);
        socket.IsOpen.Should().BeTrue();
    }

    /// <summary>B-014: untimed vessels remain cached, and untimed static data preserves an existing contact.</summary>
    /// <returns>The scripted receive operation.</returns>
    [Fact]
    public async Task GivenUntimedMessages_WhenTheyArrive_ThenContactRemainsAbsentOrUnchanged()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        ObservedClock clock = new ObservedClockFixture();
        var socket = new ScriptedVesselSocket("only", []);
        using var cache = new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi);
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers)
            .WithWriter(clock).WithVessels(cache);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));

        // When
        await Task.Run(() => socket.Receive(VesselPayloads.StaticData));
        cache.Lookup("366999003").Value.Reported.IsNone.Should().BeTrue();
        clock.Current.Should().Be(DateTimeOffset.MinValue);
        await Task.Run(() => socket.Receive(VesselPayloads.WithReportedTime(
            VesselPayloads.PositionBeforeStaticData, "2026-10-20 14:32:25 +0000 UTC")));
        await Task.Run(() => socket.Receive(VesselPayloads.StaticData));

        // Then
        var expected = new DateTimeOffset(2026, 10, 20, 14, 32, 25, TimeSpan.Zero);
        cache.Lookup("366999003").Value.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(expected);
        clock.Current.Should().Be(expected);
    }

    /// <summary>B-014, B-015: contact is per vessel, while only a later client instant is observed after its cache write.</summary>
    /// <returns>The scripted receive operation.</returns>
    [Fact]
    public async Task GivenOutOfOrderReports_WhenTheyArrive_ThenContactAndClockKeepTheirOwnLatestTime()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        ObservedClock clock = new ObservedClockFixture();
        var socket = new ScriptedVesselSocket("only", []);
        using var cache = new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi);
        var observed = new List<DateTimeOffset>();
        var cacheTimes = new List<DateTimeOffset>();
        using var ticks = clock.Instant.Skip(1).Subscribe(instant =>
        {
            observed.Add(instant);
            cacheTimes.Add(cache.Items.Max(vessel => vessel.Reported.IfNone(DateTimeOffset.MinValue)));
        });
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers)
            .WithWriter(clock).WithVessels(cache);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));

        // When
        await Task.Run(() => socket.Receive(VesselPayloads.WithReportedTime(
            VesselPayloads.PositionReport, "2026-10-20 14:32:18 +0000 UTC")));
        await Task.Run(() => socket.Receive(VesselPayloads.WithReportedTime(
            VesselPayloads.PositionReport, "2026-10-20 14:32:25 +0000 UTC")));
        await Task.Run(() => socket.Receive(VesselPayloads.WithReportedTime(
            VesselPayloads.MovedPositionReport, "2026-10-20 14:32:10 +0000 UTC")));
        await Task.Run(() => socket.Receive(VesselPayloads.WithReportedTime(
            VesselPayloads.SecondPositionReport, "2026-10-20 14:32:20 +0000 UTC")));
        await Task.Run(() => socket.Receive(VesselPayloads.WithReportedTime(
            VesselPayloads.PositionReport, "2026-10-20 14:32:25 +0000 UTC")));

        // Then
        var latest = new DateTimeOffset(2026, 10, 20, 14, 32, 25, TimeSpan.Zero);
        cache.Lookup("366999001").Value.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(latest);
        cache.Lookup("366999002").Value.Reported.IfNone(DateTimeOffset.MinValue).Should().Be(latest.AddSeconds(-5));
        observed.Should().Equal(latest.AddSeconds(-7), latest);
        cacheTimes.Should().Equal(observed);
        clock.Current.Should().Be(latest);
    }

    /// <summary>B-015: a different writer's reset does not change the client's own latest-time guard.</summary>
    /// <returns>The scripted receive operation.</returns>
    [Fact]
    public async Task GivenAnotherWriterResetsTime_WhenEqualAndLaterReportsArrive_ThenOnlyTheLaterOneWrites()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        ObservedClock clock = new ObservedClockFixture();
        var socket = new ScriptedVesselSocket("only", []);
        VesselClient sut = new VesselClientFixture().WithSockets(() => socket).WithProvider(schedulers).WithWriter(clock);
        using var listening = sut.Listen();
        await Task.Run(() => scheduler.AdvanceBy(1));
        await Task.Run(() => socket.Receive(VesselPayloads.WithReportedTime(
            VesselPayloads.PositionReport, "2026-10-20 14:32:25 +0000 UTC")));
        var reset = new DateTimeOffset(2026, 10, 20, 13, 0, 0, TimeSpan.Zero);
        ((IObservedClockWriter) clock).Observe(reset);

        // When
        await Task.Run(() => socket.Receive(VesselPayloads.WithReportedTime(
            VesselPayloads.PositionReport, "2026-10-20 14:32:25 +0000 UTC")));
        clock.Current.Should().Be(reset);
        await Task.Run(() => socket.Receive(VesselPayloads.WithReportedTime(
            VesselPayloads.PositionReport, "2026-10-20 14:32:26 +0000 UTC")));

        // Then
        clock.Current.Should().Be(new DateTimeOffset(2026, 10, 20, 14, 32, 26, TimeSpan.Zero));
    }
}
