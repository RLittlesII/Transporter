using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AwesomeAssertions;
using DynamicData;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Model;
using Transponder.Tracking;

namespace Transponder.UnitTests.Tracking;

// RSA1010 asks for ObserveOn before every Bind. These bind without one deliberately: the
// pipeline marshals for nobody (fleet-pipeline B-005) and the user-interface scheduler belongs to
// the consumer (§ 5 row 9). The consumer here is a test, which has no user-interface thread.
#pragma warning disable RSA1010

public class FleetTrackerTests
{
    /// <summary>
    /// B-042. The chain is assembled in the constructor and a swap happens below the seam, so two
    /// swaps connect the seam no more often than none do and the published stream is still the one
    /// built at construction. Two subscribers share that one connection, so a tracker that rebuilt
    /// its pipeline — per swap or per reader — would connect again, which is the leak that reads as
    /// a memory bug on a projector.
    /// </summary>
    [Fact]
    public void GivenASwapFollowedByASecondSwap_WhenEachCompletes_ThenThePipelineIsTheOneBuiltAtConstruction()
    {
        // Given
        var live = new BehaviorSubject<SourceCache<TransportVehicle, string>>(Cache());
        var source = Swapping(live);
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        var fleet = sut.Fleet;
        var observed = new List<IChangeSet<TrackedVehicle, string>>();
        using var subscription = fleet.Subscribe(observed.Add);
        using var second = fleet.Subscribe();

        // When
        live.OnNext(Cache());
        var third = Cache();
        live.OnNext(third);
        third.AddOrUpdate(Silent("a1b2c3"));

        // Then
        source.Received(1).Connect();
        sut.Fleet.Should().BeSameAs(fleet, "the stream is built once and handed out, not rebuilt per reader");
        observed.Should().ContainSingle().Which.Should().ContainSingle().Which.Key.Should().Be("a1b2c3");
    }

    /// <summary>
    /// B-043. The same vehicle, silent since the same instant, is not stale while the injected clock
    /// has observed nothing and is stale once that clock reports six minutes later — so the mark is
    /// derived from last contact against the clock it was given. The first half is what an ambient
    /// <c>DateTime.UtcNow</c> could not pass: the wall clock is hours past this vehicle's last
    /// contact, and a tracker reading it would call the first arrival stale.
    /// </summary>
    [Fact]
    public void GivenAnInjectedClockAdvancedPastTheThreshold_WhenStalenessIsRead_ThenItDerivesFromLastContactAndNoAmbientClockIsRead()
    {
        // Given
        var cache = Cache();
        var clock = new ObservedClock();
        FleetTracker sut = new FleetTrackerFixture().WithSource(Over(cache)).WithClock(clock);
        var observed = new List<IChangeSet<TrackedVehicle, string>>();
        using var subscription = sut.Fleet.Subscribe(observed.Add);
        cache.AddOrUpdate(Silent("a1b2c3"));

        // When
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(6));
        cache.AddOrUpdate(Silent("d4e5f6"));

        // Then
        Marks(observed, "a1b2c3").Should().BeFalse("the clock had observed no instant, and a wall-clock read would have made a past contact stale");
        Marks(observed, "d4e5f6").Should().BeTrue("the clock now reports six minutes past a contact the default threshold allows five");
    }

    /// <summary>
    /// B-051. A vehicle past the threshold stays in the stream carrying the mark, and no change
    /// removes it: a row vanishing reads as a bug and a flagged row reads as information. The
    /// threshold is the tracker's own five minutes until a caller says otherwise, and raising it to
    /// ten un-marks the same vehicle rather than rebuilding anything.
    /// </summary>
    [Fact]
    public void GivenAVehiclePastTheConfiguredThreshold_WhenTheCollectionIsRead_ThenItIsPresentAndObservablyStale()
    {
        // Given
        var cache = Cache();
        var clock = new ObservedClock();
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(6));
        FleetTracker sut = new FleetTrackerFixture().WithSource(Over(cache)).WithClock(clock);
        var observed = new List<IChangeSet<TrackedVehicle, string>>();
        using var subscription = sut.Fleet.Subscribe(observed.Add);

        // When
        cache.AddOrUpdate(Silent("a1b2c3"));

        // Then
        Marks(observed, "a1b2c3").Should().BeTrue("five minutes is the default the tracker holds, and the clock is six past");
        observed.SelectMany(static changes => changes).Should().NotContain(static change => change.Reason == ChangeReason.Remove);

        // And the threshold is configurable, which re-derives the mark for what is already there
        sut.StaleAfter(TimeSpan.FromMinutes(10));
        Marks(observed, "a1b2c3").Should().BeFalse("ten minutes tolerates a six-minute silence");
        observed.SelectMany(static changes => changes).Should().NotContain(static change => change.Reason == ChangeReason.Remove);
    }

    /// <summary>
    /// fleet-pipeline B-002. The pipeline publishes a stream and owns no collection: two consumers
    /// binding the same fleet each materialise their own, and every row carries the vehicle beside
    /// the mark the pipeline derived for it. A tracker that held a bound collection and handed it
    /// out would fail the last assertion, which is the shape ADR-0009 rejected.
    /// </summary>
    [Fact]
    public void GivenTheFleetStream_WhenTwoConsumersEachBindIt_ThenEachMaterialisesItsOwnCollectionCarryingTheStaleMark()
    {
        // Given
        var cache = Cache();
        var clock = new ObservedClock();
        ((IObservedClockWriter) clock).Observe(LastContact + TimeSpan.FromMinutes(6));
        FleetTracker sut = new FleetTrackerFixture().WithSource(Over(cache)).WithClock(clock);
        using var first = sut.Fleet.Bind(out var rows).Subscribe();
        using var second = sut.Fleet.Bind(out var others).Subscribe();

        // When
        cache.AddOrUpdate(Silent("a1b2c3"));
        cache.AddOrUpdate(new Aircraft("d4e5f6", LastContact + TimeSpan.FromMinutes(6)));

        // Then
        rows.Should().HaveCount(2).And.OnlyContain(static row => row.Vehicle != null);
        rows.Single(static row => row.Vehicle.Key == "a1b2c3").IsStale.Should().BeTrue("it has been silent six minutes against a five-minute threshold");
        rows.Single(static row => row.Vehicle.Key == "d4e5f6").IsStale.Should().BeFalse("it reported at the instant the clock observed");
        others.Should().NotBeSameAs(rows, "the tracker owns no collection to hand out; each consumer materialises its own");
        others.Should().BeEquivalentTo(rows, "both read the same shared stream");
    }

    /// <summary>
    /// fleet-pipeline B-028. One connection to the seam however many subscribers, the filter
    /// evaluated once per changeset rather than once per reader, and the stages torn down when the
    /// last subscriber leaves. A per-subscriber chain passes none of the three: it connects twice,
    /// filters twice, and holds the seam while anyone has ever subscribed.
    /// </summary>
    [Fact]
    public void GivenTwoSubscribers_WhenBothAreBound_ThenTheSeamIsConnectedOnceAndTheStagesStopWhenTheLastUnsubscribes()
    {
        // Given
        var cache = Cache();
        var source = new CountingTrackerSource(cache);
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        var evaluations = 0;
        sut.Filter(_ =>
        {
            evaluations++;

            return true;
        });
        var first = sut.Fleet.Subscribe();
        var second = sut.Fleet.Subscribe();

        // When
        cache.AddOrUpdate(Silent("a1b2c3"));

        // Then
        source.Connections.Should().Be(1, "the stages are shared, so a second subscriber costs the seam nothing");
        evaluations.Should().Be(1, "one changeset is filtered once however many consumers are reading it");
        first.Dispose();
        source.Disconnections.Should().Be(0, "one subscriber remains, so the chain is still live");
        second.Dispose();
        source.Disconnections.Should().Be(1, "the last subscriber leaving tears the stages down");
    }

    /// <summary>
    /// fleet-pipeline B-004. Disposal is observable in the streams rather than in a list of handles:
    /// a bound consumer's subscription completes, the seam has no subscriber left, the collection it
    /// bound stops changing, and a second disposal is not an error — a container disposing its
    /// singleton twice must not throw.
    /// </summary>
    [Fact]
    public void GivenABoundConsumer_WhenTheTrackerIsDisposed_ThenEveryPublishedStreamCompletesAndNothingRemainsSubscribedToTheSeam()
    {
        // Given
        var cache = Cache();
        var source = new CountingTrackerSource(cache);
        FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        var completed = false;
        using var subscription = sut.Fleet.Bind(out var rows).Subscribe(static _ => { }, () => completed = true);
        cache.AddOrUpdate(Silent("a1b2c3"));

        // When
        sut.Dispose();

        // Then
        completed.Should().BeTrue("the published stream completes, which is what a consumer can observe");
        source.Disconnections.Should().Be(1, "nothing remains subscribed to the seam");
        cache.AddOrUpdate(Silent("d4e5f6"));
        rows.Should().ContainSingle().Which.Vehicle.Key.Should().Be("a1b2c3", "the bound collection stops receiving changes");
        sut.Invoking(static tracker => tracker.Dispose()).Should().NotThrow("a second disposal does nothing");
    }

    /// <summary>
    /// fleet-pipeline B-004, the state that would hide the defect. Until someone subscribes the
    /// tracker holds nothing: the seam is never connected, so an application that resolves the
    /// singleton at startup and binds nothing yet spends no credits and keeps no chain alive.
    /// </summary>
    [Fact]
    public void GivenATrackerNobodyHasSubscribedTo_WhenTheSeamReports_ThenItWasNeverConnected()
    {
        // Given
        var cache = Cache();
        var source = new CountingTrackerSource(cache);

        // When
        using FleetTracker sut = new FleetTrackerFixture().WithSource(source);
        cache.AddOrUpdate(Silent("a1b2c3"));

        // Then
        source.Connections.Should().Be(0, "an idle tracker holds no subscription of its own");
    }

    /// <summary>
    /// fleet-pipeline B-005. No stage reads an ambient scheduler and none marshals for a consumer:
    /// the change is delivered before the edit that caused it returns, on the thread that made it.
    /// An <c>ObserveOn</c> or a <c>Throttle</c> anywhere in the chain reddens the first assertion,
    /// which is the failure that only shows up once the grid is real.
    /// </summary>
    [Fact]
    public void GivenNoSchedulerInTheArrangement_WhenTheFleetChanges_ThenTheChangeArrivesSynchronouslyOnTheThreadThatFedTheSeam()
    {
        // Given
        var cache = Cache();
        FleetTracker sut = new FleetTrackerFixture().WithSource(Over(cache));
        var reader = 0;
        using var subscription = sut.Fleet.Subscribe(_ => reader = Environment.CurrentManagedThreadId);

        // When
        cache.AddOrUpdate(Silent("a1b2c3"));

        // Then
        reader.Should().NotBe(0, "a stage that scheduled the delivery would not have run before the edit returned");
        reader.Should().Be(Environment.CurrentManagedThreadId, "nothing marshals on a consumer's behalf; that boundary is the consumer's");
    }

    /// <summary>A cache of domain vehicles, keyed the way the seam keys its changesets.</summary>
    /// <returns>A store a test edits to make the seam report.</returns>
    private static SourceCache<TransportVehicle, string> Cache() => new(static vehicle => vehicle.Key);

    /// <summary>A seam over one store.</summary>
    /// <param name="cache">The store the seam reports from.</param>
    /// <returns>The substituted seam.</returns>
    private static ITrackerSource Over(SourceCache<TransportVehicle, string> cache)
    {
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(cache.Connect());
        source.ClearReceivedCalls();

        return source;
    }

    /// <summary>A seam whose live store changes, which is what a swap looks like from above it.</summary>
    /// <param name="live">The store that is live, and every store that replaces it.</param>
    /// <returns>The substituted seam.</returns>
    private static ITrackerSource Swapping(IObservable<SourceCache<TransportVehicle, string>> live)
    {
        var source = Substitute.For<ITrackerSource>();
        source.Connect().Returns(live.Select(static cache => cache.Connect()).Switch());
        source.ClearReceivedCalls();

        return source;
    }

    /// <summary>An aircraft that has reported nothing since <see cref="LastContact"/>.</summary>
    /// <param name="key">The <c>icao24</c> in lowercase hex.</param>
    /// <returns>The vehicle a test puts into the store.</returns>
    private static TransportVehicle Silent(string key) => new Aircraft(key, LastContact);

    /// <summary>The mark the pipeline last published for one vehicle.</summary>
    /// <param name="observed">Every changeset the subscription has seen.</param>
    /// <param name="key">The vehicle to read.</param>
    /// <returns>Whether that vehicle was stale when the pipeline last evaluated it.</returns>
    private static bool Marks(IEnumerable<IChangeSet<TrackedVehicle, string>> observed, string key) =>
        observed
            .SelectMany(static changes => changes)
            .Where(change => change.Key == key)
            .Select(static change => change.Current.IsStale)
            .Last();

    /// <summary>The instant every vehicle here was last heard from, and the only one the clock is told about.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
}

/// <summary>Builds the tracker, so a constructor change edits this and not every test.</summary>
[AutoFixture(typeof(FleetTracker))]
internal partial class FleetTrackerFixture
{
    public FleetTrackerFixture()
    {
        WithSource(Substitute.For<ITrackerSource>());
        WithClock(new ObservedClock());
    }
}

/// <summary>A seam that counts what the pipeline does to it, which is what B-004 and B-028 assert.</summary>
/// <param name="cache">The store every connection reports from.</param>
internal sealed class CountingTrackerSource(SourceCache<TransportVehicle, string> cache) : ITrackerSource
{
    /// <summary>Gets how many times something subscribed to this seam.</summary>
    public int Connections { get; private set; }

    /// <summary>Gets how many of those subscriptions have since been disposed.</summary>
    public int Disconnections { get; private set; }

    /// <inheritdoc/>
    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        Observable.Create<IChangeSet<TransportVehicle, string>>(observer =>
        {
            Connections++;
            var subscription = cache.Connect().Subscribe(observer);

            return Disposable.Create(() =>
            {
                Disconnections++;
                subscription.Dispose();
            });
        });
}

#pragma warning restore RSA1010
