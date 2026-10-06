using AwesomeAssertions;
using DynamicData;
using Flurl.Http.Testing;
using Microsoft.Reactive.Testing;
using Transponder.Integrations.OpenSky;
using Transponder.Integrations.OpenSky.Authentication;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Integrations.OpenSky.Http.Api;
using Transponder.Model;
using Transponder.Recording;
using Transponder.Tracking;
using Transponder.Tracking.Sources;
using Transponder.UnitTests.Tracking;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class RecordingTapTests
{
    /// <summary>
    /// B-003. Recording is a tap on traffic the poll already paid for: turning it on adds no
    /// request, so a rehearsal spends the credits of one run rather than two.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenRecordingIsOn_WhenOnePollCompletes_ThenOnlyThePollsOwnRequestWasMade()
    {
        // Given
        var scheduler = new TestScheduler();
        var clients = OpenSkyTransport.Clients();
        using var http = new HttpTest();
        http.ForCallsTo(OpenSkyTransport.TokenCalls).RespondWithJson(new { access_token = "token-one", expires_in = 60 });
        http.ForCallsTo(OpenSkyTransport.StatesCalls).RespondWith(OpenSkyPayloads.NoRows);
        using var destination = new StringWriter();
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture()
            .WithCache(clients)
            .WithSource(OpenSkyTransport.Tokens(clients, scheduler, new RecordingLogger<OpenSkyTokenSource>()))
            .WithProvider(OpenSkyTransport.Schedulers(scheduler))
            .WithWriter(new RecordingWriter(destination, new RecordingLogger<RecordingWriter>()));

        // When
        await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        http.ShouldHaveCalled(OpenSkyTransport.StatesCalls).Times(1);
        destination.ToString().Should().NotBeEmpty("the payload was recorded, so this proves the tap ran rather than that it was absent");
        http.CallLog.Should().HaveCount(2, "one token call and one poll; a recorder that fetched anything of its own would be a third");
    }

    /// <summary>
    /// B-001 and B-009 together, at the one place they are easy to confuse. The line's
    /// <c>receivedAt</c> is the injected clock's instant — when the payload arrived — while the
    /// provider's own reported time stays inside <c>body</c> and is what the fleet observes.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAPayloadReportingItsOwnTime_WhenItIsRecorded_ThenTheLineCarriesTheArrivalInstantAndThePayloadKeepsTheReportedOne()
    {
        // Given
        var scheduler = new TestScheduler();
        var clients = OpenSkyTransport.Clients();
        using var http = new HttpTest();
        http.ForCallsTo(OpenSkyTransport.TokenCalls).RespondWithJson(new { access_token = "token-one", expires_in = 60 });
        http.ForCallsTo(OpenSkyTransport.StatesCalls).RespondWith(OpenSkyPayloads.NoRows);
        using var destination = new StringWriter();
        OpenSkyHttpApi sut = new OpenSkyHttpApiFixture()
            .WithCache(clients)
            .WithSource(OpenSkyTransport.Tokens(clients, scheduler, new RecordingLogger<OpenSkyTokenSource>()))
            .WithProvider(OpenSkyTransport.Schedulers(scheduler))
            .WithWriter(new RecordingWriter(destination, new RecordingLogger<RecordingWriter>()));

        // When
        await ((IOpenSkyApi) sut).GetStates(29.4, -95.9, 30.2, -94.8, true, CancellationToken.None);

        // Then
        var line = destination.ToString();

        line.Should().StartWith(
            $"{{\"receivedAt\":\"{scheduler.Now.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fff}Z\"",
            "the arrival instant is the injected scheduler's, never the wall clock");
        line.Should().Contain(
            $"\"time\": {OpenSkyPayloads.ReportedTime}",
            "the provider's reported time stays in the body, where B-009 reads it from");
    }

    /// <summary>
    /// B-004, at the boundary the claim names. The changesets reaching the fleet are compared
    /// between a run that records and a run that does not — not the envelope the transport
    /// returned, because the claim is about what the fleet sees and a proof one layer short of
    /// that is a proof of something else (`test-from-scenarios` § "A claim proven only against an
    /// inner method"). The chain is the real one throughout: transport, snapshot client, cache,
    /// and the strategy's projection.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenOnePayloadPolledWithRecordingOnAndOff_WhenTheFleetIsObserved_ThenTheChangesetsAreIdentical()
    {
        // Given
        using var destination = new StringWriter();

        // When
        var recorded = await Fleet(new RecordingWriter(destination, new RecordingLogger<RecordingWriter>()));
        var unrecorded = await Fleet(new UnrecordedPayloads());

        // Then
        destination.ToString().Should().NotBeEmpty("otherwise the two runs differ in nothing and the comparison proves nothing");
        recorded.Should().HaveCount(3, "two empty lists are equivalent, so the comparison below needs the fleet to have been told something");
        recorded.Should().BeEquivalentTo(unrecorded);
    }

    /// <summary>
    /// One poll all the way to the fleet, with the writer under test behind the transport.
    /// </summary>
    /// <param name="recorder">The writer the tap hands the payload to.</param>
    /// <returns>Each vehicle the fleet was told about, with the reason and the values a view binds.</returns>
    private static async Task<List<(ChangeReason Reason, string Key, string Label, bool OnGround)>> Fleet(IRecordingWriter recorder)
    {
        var scheduler = new TestScheduler();
        var clients = OpenSkyTransport.Clients();
        using var http = new HttpTest();
        http.ForCallsTo(OpenSkyTransport.TokenCalls).RespondWithJson(new { access_token = "token-one", expires_in = 60 });
        http.ForCallsTo(OpenSkyTransport.StatesCalls).RespondWith(OpenSkyPayloads.ThreeRows);

        var transport = new OpenSkyHttpApiFixture()
            .WithCache(clients)
            .WithSource(OpenSkyTransport.Tokens(clients, scheduler, new RecordingLogger<OpenSkyTokenSource>()))
            .WithProvider(OpenSkyTransport.Schedulers(scheduler))
            .WithWriter(recorder);

        var snapshots = new SourceCache<AircraftSnapshot, string>(static snapshot => snapshot.Icao24);
        AircraftSnapshotClient client = new AircraftSnapshotClientFixture()
            .WithApi((OpenSkyHttpApi) transport)
            .WithCache(snapshots);
        AircraftTrackerSource projection = new AircraftTrackerSourceFixture().WithSnapshots(snapshots).WithClient(client);
        ITrackerSource strategy = projection;

        var observed = new List<(ChangeReason, string, string, bool)>();
        using var subscription = strategy.Connect()
            .Subscribe(changes => observed.AddRange(
                changes.Select(static change =>
                    (change.Reason, change.Key, change.Current.Label, ((Aircraft) change.Current).OnGround))));

        await client.Fetch(CancellationToken.None);

        return observed;
    }
}
