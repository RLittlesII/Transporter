using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Airframe;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Recording;
using Transporter.Tracking;
using Transporter.Tracking.Container;
using Transporter.Tracking.Sources;

namespace Transporter.UnitTests.Integrations.OpenSky.Replay;

public class ReplayRegistrationTests
{
    /// <summary>
    /// B-024. One key per replay source and no default compiled in: the aircraft key names a
    /// recording and the vessel key names none, so one replay source is registered beside the live
    /// one and the other is absent rather than selectable. A default would make the second source
    /// selectable over a recording nobody chose, which on stage is the wrong aircraft over the wrong
    /// city.
    /// <para>
    /// The live source is still the one the selector starts on, because configuration names which
    /// recording a replay source would read and never whether replay is live — that is the
    /// selector's (B-015).
    /// </para>
    /// </summary>
    [Fact]
    public void GivenAnAircraftRecordingNamedAndNoVesselOne_WhenTheHostStarts_ThenOnlyTheAircraftReplaySourceIsSelectableAndTheLiveSourceIsStillSelected()
    {
        // Given
        var scheduler = new TestScheduler();
        var recordings = new RecordedFiles().Holding(Rehearsal, ReplayRecordingCases.SpanningSixMinutes);
        var settings = ReplayComposition.Settings(aircraft: Rehearsal);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISchedulerProvider>(ReplayComposition.Schedulers(scheduler));
        services.AddOpenSky(settings);

        // When
        services.AddAircraftReplay(settings, recordings);
        services.AddFleetTracking();
        using var composed = services.BuildServiceProvider();

        // Then
        var strategies = composed.GetServices<ITrackerSourceStrategy>().ToArray();
        strategies.Should().HaveCount(2, "the live source, and the one replay source a recording was named for");
        strategies.Should().ContainSingle(static strategy => strategy is AircraftReplayTrackerSource);
        strategies[0].Should().BeOfType<AircraftTrackerSource>("the live source is registered first, so it is the one the selector starts on");
        composed.GetRequiredService<ReplayStartupReport>()
            .Recordings.Single(static recording => recording.Source == nameof(ReplayOptions.Vessels))
            .Defect.Should()
            .Be(RecordingDefect.Unnamed, "no vessel recording was named, so there is no vessel replay source to select");
    }

    /// <summary>
    /// B-025. A named recording resolves under the one configured recordings root, so a rehearsal
    /// writes and a replay reads the same place without either value carrying an absolute path —
    /// which is what lets the same settings run on the machine that recorded and the one on stage.
    /// <para>
    /// Asserted against the real library, over a root with nothing in it: the resolving is what the
    /// claim is about, and the row says where it looked rather than throwing. A recording is
    /// operational data that is never a fixture (B-006), so there is no file here to find.
    /// </para>
    /// </summary>
    [Fact]
    public void GivenAConfiguredRootAndARecordingName_WhenTheRecordingIsOpened_ThenItResolvesUnderThatRootAndNeitherValueCarriedAnAbsolutePath()
    {
        // Given
        var root = RecordingOptions.DefaultRoot;
        var recordings = new RecordingLibrary(root);

        // When
        var resolved = recordings.Resolve(Rehearsal);
        var inspected = CheckedRecording.Check(nameof(ReplayOptions.Aircraft), Rehearsal, recordings, FleetTracker.DefaultStaleAfter);

        // Then
        resolved.Should().Be(Path.Combine(root, Rehearsal), "the name resolves under the root and nowhere else");
        Path.IsPathRooted(root).Should().BeFalse("the root is relative to the running application");
        Path.IsPathRooted(Rehearsal).Should().BeFalse("and a configured name is a file name, not a path");
        inspected.Path.Should().Be(resolved, "the report names where it looked");
        inspected.Defect.Should().Be(RecordingDefect.Absent, "nothing is under that root here, which is a reported row rather than a failure");
    }

    /// <summary>The recording a rehearsal left behind, named the way ADR-0004 names one.</summary>
    private const string Rehearsal = "aircraft-2026-10-04T14-32-11Z.ndjson";
}
