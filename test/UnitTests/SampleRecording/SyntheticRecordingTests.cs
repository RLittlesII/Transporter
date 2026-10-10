using AwesomeAssertions;
using DynamicData;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Container;
using Transporter.Recording;
using Transporter.SampleRecording;
using Transporter.Tracking;
using Transporter.UnitTests.Integrations.OpenSky.Replay;
using Transporter.UnitTests.Recording;

namespace Transporter.UnitTests.SampleRecording;

public class SyntheticRecordingTests
{
    /// <summary>
    /// One seed writes one file, so two developers reading the same grid are reading the same
    /// aircraft. A clock, a GUID or an unseeded draw anywhere in the generator fails here, and
    /// would otherwise surface as a demo nobody can reproduce from a seed written in a runbook.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenOneSeed_WhenTheRecordingIsWrittenTwice_ThenTheTwoAreIdentical()
    {
        // Given
        using var first = new StringWriter();
        using var second = new StringWriter();
        RecordingWriter toFirst = new RecordingWriterFixture().WithDestination(first);
        RecordingWriter toSecond = new RecordingWriterFixture().WithDestination(second);
        SyntheticRecording once = new SyntheticRecordingFixture().WithWriter(toFirst).WithSeed(7);
        SyntheticRecording again = new SyntheticRecordingFixture().WithWriter(toSecond).WithSeed(7);

        // When
        await once.Write();
        await again.Write();

        // Then
        second.ToString().Should().Be(first.ToString(), "one seed is one byte-identical recording");
        first.ToString().Should().NotBeEmpty("a recording of nothing is identical to another recording of nothing");
    }

    /// <summary>
    /// A second seed is a second fleet. Without this, a generator that ignored its seed would pass
    /// the determinism test above by writing the same file whatever it was handed.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenTwoSeeds_WhenEachIsWritten_ThenTheRecordingsDiffer()
    {
        // Given
        using var first = new StringWriter();
        using var second = new StringWriter();
        RecordingWriter toFirst = new RecordingWriterFixture().WithDestination(first);
        RecordingWriter toSecond = new RecordingWriterFixture().WithDestination(second);
        SyntheticRecording one = new SyntheticRecordingFixture().WithWriter(toFirst).WithSeed(7);
        SyntheticRecording other = new SyntheticRecordingFixture().WithWriter(toSecond).WithSeed(8);

        // When
        await one.Write();
        await other.Write();

        // Then
        second.ToString().Should().NotBe(first.ToString(), "the seed is what the fleet is drawn from");
    }

    /// <summary>
    /// The startup check the application runs over a configured recording accepts this one: it is
    /// readable, and it covers more than the staleness threshold, which is what a recording has to
    /// span to rehearse the part of the talk staleness is in.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenTheGeneratedRecording_WhenTheStartupCheckReadsIt_ThenItIsUsableAndOutlastsTheStalenessThreshold()
    {
        // Given
        using var destination = new StringWriter();
        RecordingWriter recorder = new RecordingWriterFixture().WithDestination(destination);
        SyntheticRecording generator = new SyntheticRecordingFixture().WithWriter(recorder);
        await generator.Write();
        var recordings = new RecordedFiles().Holding("sample.ndjson", destination.ToString());

        // When
        var report = CheckedRecording.Check("aircraft", "sample.ndjson", recordings, FleetTracker.DefaultStaleAfter);

        // Then
        report.Usable.Should().BeTrue("a generated recording is read like any other");
        report.Span.Should().BeGreaterThan(FleetTracker.DefaultStaleAfter, "a shorter one cannot show an aircraft go quiet");
    }

    /// <summary>
    /// The hazard polls are derived from the length, so a recording asked for shorter than the
    /// default still carries all of them. A fixed poll number would quietly drop the malformed row
    /// and the empty poll out of a shorter recording, leaving a sample that exercises nothing and
    /// says nothing about it.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenARecordingShorterThanTheDefault_WhenItIsReplayed_ThenItStillCarriesTheMalformedRowAndTheEmptyPoll()
    {
        // Given
        using var destination = new StringWriter();
        RecordingWriter recorder = new RecordingWriterFixture().WithDestination(destination);
        SyntheticRecording generator = new SyntheticRecordingFixture().WithWriter(recorder).WithPolls(24);
        await generator.Write();
        var scheduler = new TestScheduler();
        using var recording = ReplayComposition.Recorded(destination.ToString());
        using var composed = ReplayComposition.Over(recording, scheduler);
        var replay = (AircraftSnapshotClient) composed.GetRequiredKeyedService<IAircraftSnapshotClient>(OpenSkyReplayRegistration.Chain);
        var cache = composed.GetRequiredKeyedService<SourceCache<AircraftSnapshot, string>>(OpenSkyReplayRegistration.Chain);
        var excluded = new List<int>();
        var reported = new List<int>();

        // When
        for (var poll = 0; poll < generator.Polls; poll++)
        {
            var fetch = replay.Fetch(CancellationToken.None);
            scheduler.AdvanceBy(generator.Interval.Ticks);
            await fetch;
            excluded.Add(replay.UnreadableRows);
            reported.Add(cache.Count);
        }

        // Then
        generator.Span.Should().BeGreaterThan(FleetTracker.DefaultStaleAfter, "a recording this short is still worth replaying");
        excluded.Sum().Should().Be(1, "the malformed row lands on a poll this recording has");
        excluded[generator.Malformed].Should().Be(1, "and on the one the length derives");
        reported[generator.Empties].Should().Be(0, "so does the poll that reports nothing");
    }

    /// <summary>
    /// The live reader reads it, and every hazard it carries lands where the reader's claims say
    /// it should: an absent velocity against a category of zero, an absent category, a callsign
    /// that was all padding, a squawk whose leading zero survived, one row excluded and counted,
    /// and one poll that reported nothing. A sample holding only well-formed rows exercises none
    /// of that, which is why the hazards are in it.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenTheGeneratedRecording_WhenItIsReplayedThroughTheLiveChain_ThenEveryHazardItCarriesIsRead()
    {
        // Given
        using var destination = new StringWriter();
        RecordingWriter recorder = new RecordingWriterFixture().WithDestination(destination);
        SyntheticRecording generator = new SyntheticRecordingFixture().WithWriter(recorder);
        await generator.Write();
        var scheduler = new TestScheduler();
        using var recording = ReplayComposition.Recorded(destination.ToString());
        using var composed = ReplayComposition.Over(recording, scheduler);
        var replay = (AircraftSnapshotClient) composed.GetRequiredKeyedService<IAircraftSnapshotClient>(OpenSkyReplayRegistration.Chain);
        var cache = composed.GetRequiredKeyedService<SourceCache<AircraftSnapshot, string>>(OpenSkyReplayRegistration.Chain);
        var clock = composed.GetRequiredService<IObservedClock>();
        var excluded = new List<int>();
        var reported = new List<int>();

        // When
        for (var poll = 0; poll < generator.Polls; poll++)
        {
            var fetch = replay.Fetch(CancellationToken.None);
            scheduler.AdvanceBy(generator.Interval.Ticks);
            await fetch;
            excluded.Add(replay.UnreadableRows);
            reported.Add(cache.Count);
        }

        // Then
        var fleet = cache.Items.ToDictionary(static snapshot => snapshot.Icao24);
        excluded[generator.Malformed].Should().Be(1, "one row carries a flag that is neither true nor false");
        excluded.Sum().Should().Be(1, "every other row in the recording is readable");
        reported[generator.Empties].Should().Be(0, "one poll reports nothing at all");
        ((object) fleet["a2b2c2"].Velocity).Should()
            .Be(Option<double>.None, "an aircraft on the ground reports no speed rather than a speed of zero");
        ((object) fleet["a2b2c2"].Category).Should()
            .Be(Option<int>.Some(0), "a category of zero is present information, not absent");
        ((object) fleet["a3b3c3"].Category).Should()
            .Be(Option<int>.None, "a seventeen-element row carries no category at all");
        ((object) fleet["a4b4c4"].Callsign).Should()
            .Be(Option<string>.None, "a callsign of nothing but padding is absent, never empty");
        ((object) fleet["a5b5c5"].Squawk).Should()
            .Be(Option<string>.Some("0123"), "a squawk is four characters, so a leading zero survives");
        fleet.Should().NotContainKey("a7b7c7", "the row that cannot be read is excluded, and the rows beside it survive");
        (clock.Current - DateTimeOffset.FromUnixTimeSeconds(fleet["a6b6c6"].LastContact))
            .Should()
            .BeGreaterThan(
                FleetTracker.DefaultStaleAfter,
                "one aircraft goes quiet partway through and is still reported, which is what staleness is read from");
    }
}

/// <summary>Builds the generator, so a constructor change edits this and not every test.</summary>
[AutoFixture(typeof(SyntheticRecording))]
internal partial class SyntheticRecordingFixture
{
    public SyntheticRecordingFixture()
    {
        WithSeed(1);
        WithPolls(SyntheticRecording.DefaultPolls);
        WithInterval(SyntheticRecording.DefaultInterval);
    }
}
