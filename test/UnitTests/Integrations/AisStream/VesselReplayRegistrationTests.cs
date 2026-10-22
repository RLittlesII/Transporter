using System.Reactive.Linq;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DynamicData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.AisStream;
using Transporter.Integrations.AisStream.Container;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Recording;
using Transporter.Recording.Container;
using Transporter.Tracking;
using Transporter.Tracking.Container;
using Transporter.Tracking.Sources;
using Transporter.UnitTests.Integrations.OpenSky.Replay;

namespace Transporter.UnitTests.Integrations.AisStream;

public class VesselReplayRegistrationTests
{
    /// <summary>B-020/B-023: selecting recorded vessels feeds the common seam with the live vessel projection.</summary>
    /// <returns>The selected-source assertions.</returns>
    [Fact(Timeout = 10000)]
    public async Task GivenARecordedVessel_WhenReplayIsSelected_ThenTheOneSelectorPublishesAVesselWithoutASocket()
    {
        var frame = VesselPayloads.WithReportedTime(VesselPayloads.PositionReport, "2026-10-20 14:00:00 +0000 UTC");
        var body = JsonNode.Parse(frame)!.ToJsonString();
        var lines = """{"receivedAt":"2026-10-20T16:00:00.000Z","body":BODY}""".Replace("BODY", body, StringComparison.Ordinal) + "\n"
            + """{"receivedAt":"2026-10-20T16:11:00.000Z","body":{}}""" + "\n";
        var recordings = new RecordedFiles().Holding(Name, lines);
        var settings = ReplayComposition.Settings(vessels: Name);
        var scheduler = new TestScheduler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISchedulerProvider>(ReplayComposition.Schedulers(scheduler));
        services.AddOpenSky(settings);
        services.AddAisStream(settings);
        services.AddSingleton<Func<IVesselSocket>>(static _ => static () => throw new InvalidOperationException("Replay opened a socket."));
        services.AddReplaySources(settings, recordings);
        services.AddFleetTracking();
        using var provider = services.BuildServiceProvider();
        var selector = provider.GetRequiredService<SwappingTrackerSource>();
        selector.Select(selector.Entries.Single(static entry => entry.Name == "Recorded vessels"));
        var replayCache = provider.GetRequiredKeyedService<SourceCache<VesselSnapshot, string>>(AisStreamReplayRegistration.Chain);
        var cacheFilled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cacheSubscription = replayCache.Connect().Subscribe(_ =>
        {
            if (replayCache.Count > 0)
            {
                cacheFilled.TrySetResult();
            }
        });
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = provider.GetRequiredService<ITrackerSource>().Connect().ObserveOn(scheduler).Bind(out var rows)
            .Subscribe(_ =>
            {
                if (rows.Count > 0)
                {
                    arrived.TrySetResult();
                }
            });

        scheduler.AdvanceBy(1);
        await cacheFilled.Task;
        scheduler.AdvanceBy(1);
        await arrived.Task;

        rows.Should().ContainSingle().Which.Should().BeOfType<Transporter.Model.Vessel>();
        rows.Single().LastContact.Should().Be(new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.Zero));
        selector.Entries.Should().HaveCount(3, "the live aircraft and vessel strategies remain alongside replay");
    }

    /// <summary>B-020/B-023/B-026: a valid recording is selectable with vessel presentation and no poll.</summary>
    [Fact]
    public void GivenAVesselRecordingLongerThanItsSilenceThreshold_WhenComposed_ThenItIsAReportedSelectableStrategy()
    {
        var recordings = new RecordedFiles().Holding(Name, Span(11));
        var settings = ReplayComposition.Settings(vessels: Name);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISchedulerProvider>(ReplayComposition.Schedulers(new TestScheduler()));
        services.AddOpenSky(settings);
        services.AddAisStream(settings);
        services.AddReplaySources(settings, recordings);
        services.AddFleetTracking();
        using var provider = services.BuildServiceProvider();

        var checkedVessels = provider.GetRequiredService<ReplayStartupReport>().Recordings
            .Single(static recording => recording.Source == nameof(ReplayOptions.Vessels));
        var entries = provider.GetServices<TrackerSourceEntry>().ToArray();
        var replay = entries.Single(static entry => entry.Name == "Recorded vessels");

        checkedVessels.Defect.Should().Be(RecordingDefect.None);
        checkedVessels.Span.Should().Be(TimeSpan.FromMinutes(11));
        entries.Select(static entry => entry.Name).Should().Equal("Live aircraft", "Vessels", "Recorded vessels");
        provider.GetServices<ITrackerSourceStrategy>().Should().Contain(replay.Source);
        replay.Source.Should().BeOfType<VesselTrackerSource>();
        replay.Status.CanDemandPoll.FirstAsync().Wait().Should().BeFalse();
        replay.Description.SilentAfter.IfNone(TimeSpan.Zero).Should().Be(TimeSpan.FromMinutes(10));
        provider.GetRequiredService<ITrackerSource>().Should().BeOfType<SwappingTrackerSource>();
    }

    /// <summary>B-026: an aircraft-length capture cannot demonstrate vessel silence.</summary>
    [Fact]
    public void GivenAVesselRecordingShorterThanTenMinutes_WhenComposed_ThenTheReportExcludesItsStrategy()
    {
        var recordings = new RecordedFiles().Holding(Name, Span(6));
        var settings = ReplayComposition.Settings(vessels: Name);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISchedulerProvider>(ReplayComposition.Schedulers(new TestScheduler()));
        services.AddOpenSky(settings);
        services.AddAisStream(settings);
        services.AddReplaySources(settings, recordings);
        using var provider = services.BuildServiceProvider();

        var checkedVessels = provider.GetRequiredService<ReplayStartupReport>().Recordings
            .Single(static recording => recording.Source == nameof(ReplayOptions.Vessels));
        checkedVessels.Defect.Should().Be(RecordingDefect.TooShort);
        checkedVessels.Span.Should().Be(TimeSpan.FromMinutes(6));
        provider.GetServices<TrackerSourceEntry>().Should().NotContain(static entry => entry.Name == "Recorded vessels");
    }

    /// <summary>B-026: the named vessel threshold also controls startup adequacy.</summary>
    [Fact]
    public void GivenATwentyMinuteVesselThreshold_WhenATwelveMinuteRecordingIsNamed_ThenItIsTooShort()
    {
        var recordings = new RecordedFiles().Holding(Name, Span(12));
        var settings = new ConfigurationBuilder()
            .AddConfiguration(ReplayComposition.Settings(vessels: Name))
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AisStream:SilentAfter"] = "00:20:00" })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISchedulerProvider>(ReplayComposition.Schedulers(new TestScheduler()));
        services.AddReplaySources(settings, recordings);
        using var provider = services.BuildServiceProvider();

        var checkedVessels = provider.GetRequiredService<ReplayStartupReport>().Recordings
            .Single(static recording => recording.Source == nameof(ReplayOptions.Vessels));
        checkedVessels.Defect.Should().Be(RecordingDefect.TooShort);
        checkedVessels.Span.Should().Be(TimeSpan.FromMinutes(12));
        provider.GetServices<TrackerSourceEntry>().Should().NotContain(static entry => entry.Name == "Recorded vessels");
    }

    private static string Span(int minutes) =>
        """{"receivedAt":"2026-10-20T14:00:00.000Z","body":{}}""" + "\n"
        + $"{{\"receivedAt\":\"2026-10-20T14:{minutes:00}:00.000Z\",\"body\":{{}}}}\n";

    private const string Name = "vessels-synthetic.ndjson";
}
