using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using DynamicData;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Model;
using Transporter.Tracking;
using Transporter.Tracking.Sources;

namespace Transporter.UnitTests.Tracking;

public class SwappingTrackerSourceTests
{
    /// <summary>
    /// B-038. Two strategies reach the decorator as the container's enumerable, and the live one is
    /// named by its registration entry rather than by a kind the seam carries. The second clause is the
    /// absence of a resolver: no member anywhere in the assembly answers "which source is live",
    /// because the decorator is told and nothing asks it.
    /// </summary>
    [Fact]
    public void GivenTwoStrategies_WhenTheLiveOneIsSelected_ThenTheDecoratorChoosesAndNoResolverTypeExists()
    {
        // Given
        var strategies = new SwappingTrackerSourceFixture();
        SwappingTrackerSource sut = strategies;
        var observed = new List<IChangeSet<TransportVehicle, string>>();
        using var subscription = sut.Connect().Subscribe(observed.Add);

        // When
        strategies.Aircraft.Report("a1b2c3");
        sut.Select(sut.Entries[1]);
        strategies.Vessels.Report("imo9074729");

        // Then
        observed.SelectMany(static changes => changes)
            .Where(static change => change.Reason == ChangeReason.Add)
            .Select(static change => change.Key)
            .Should().Equal("a1b2c3", "imo9074729");
        Resolvers().Should().BeEmpty("nothing asks which strategy is live; the decorator is told");
    }

    /// <summary>
    /// B-039. One subscription spans the swap, and every change it carries is an ordinary one: the
    /// outgoing fleet leaves as removes, exactly as it would if those aircraft had left the box, and
    /// the incoming fleet arrives as adds. Nothing marks the handover — no completion, no error, no
    /// reason and no key a single source could not have produced.
    /// </summary>
    [Fact]
    public void GivenASubscriber_WhenASwapOccurs_ThenNothingInTheStreamRevealsIt()
    {
        // Given
        var strategies = new SwappingTrackerSourceFixture();
        SwappingTrackerSource sut = strategies;
        var observed = new List<IChangeSet<TransportVehicle, string>>();
        var completed = false;
        Exception? failure = null;
        using var subscription = sut.Connect().Subscribe(observed.Add, error => failure = error, () => completed = true);
        strategies.Aircraft.Report("a1b2c3");

        // When
        sut.Select(sut.Entries[1]);
        strategies.Vessels.Report("imo9074729");

        // Then
        observed.SelectMany(static changes => changes).Select(static change => (change.Reason, change.Key))
            .Should().Equal(
                (ChangeReason.Add, "a1b2c3"),
                (ChangeReason.Remove, "a1b2c3"),
                (ChangeReason.Add, "imo9074729"));
        completed.Should().BeFalse("the subscription that spanned the swap is the one that was made");
        failure.Should().BeNull();
    }

    /// <summary>
    /// B-040. The outgoing strategy's subscription is what owns its poller, so <c>Switch</c> stops
    /// the poll by dropping the subscription and nothing has to remember to dispose anything.
    /// Swapping back starts it again — the flat-across-repetitions property a swapped-away singleton
    /// would not have if the decorator disposed it.
    /// </summary>
    [Fact]
    public void GivenAnOutgoingSource_WhenTheSwapCompletes_ThenItIsStopped()
    {
        // Given
        var strategies = new SwappingTrackerSourceFixture();
        SwappingTrackerSource sut = strategies;
        using var subscription = sut.Connect().Subscribe();

        // When
        var beforeTheSwap = (strategies.Aircraft.Started, strategies.Aircraft.Stopped, strategies.Vessels.Started);
        sut.Select(sut.Entries[1]);
        var afterTheSwap = (strategies.Aircraft.Started, strategies.Aircraft.Stopped, strategies.Vessels.Started);
        sut.Select(sut.Entries[0]);

        // Then
        beforeTheSwap.Should().Be((1, 0, 0), "the first subscription starts the first poll");
        afterTheSwap.Should().Be((1, 1, 1), "the outgoing poll stops with the subscription that owned it");
        strategies.Aircraft.Started.Should().Be(2, "swapping back starts the poll again rather than finding it disposed");
        strategies.Vessels.Stopped.Should().Be(1);
    }

    /// <summary>
    /// B-056. A late subscriber reads the live strategy's status at once; a swap reads the incoming
    /// one's at once; what the outgoing strategy writes after the swap is not published; and a swap
    /// back reads that strategy's latest at once. The decorator buffers nothing of its own — each
    /// entry's seam starts with the status in force, and <c>Switch</c> drops the outgoing one.
    /// </summary>
    [Fact]
    public void GivenTwoStrategiesWithStatuses_WhenTheSourceSwaps_ThenTheIncomingStatusIsReadAtOnceAndNothingOutgoingFollows()
    {
        // Given
        var strategies = new SwappingTrackerSourceFixture();
        SwappingTrackerSource sut = strategies;
        strategies.AircraftStatus.OnNext(Due(Aircraft));
        strategies.VesselStatus.OnNext(Due(Vessels));
        var observed = new List<PollStatus>();
        using var subscription = sut.Status.Subscribe(observed.Add);
        var beforeTheSwap = observed.ToArray();

        // When
        sut.Select(sut.Entries[1]);
        strategies.AircraftStatus.OnNext(Due(AircraftLater));
        var afterTheSwap = observed.Skip(beforeTheSwap.Length).ToArray();
        sut.Select(sut.Entries[0]);

        // Then
        beforeTheSwap.Should().Equal([Due(Aircraft)], "a late subscriber reads the live entry's status at once");
        afterTheSwap.Should().Equal([Due(Vessels)], "the incoming status at once, and nothing the outgoing one wrote after");
        observed[^1].Should().Be(Due(AircraftLater), "a swap back reads that entry's latest at once");
    }

    /// <summary>
    /// B-056, a strategy registered with no poll status. Selecting it reads none at once, so a
    /// countdown left over from the outgoing poller is replaced rather than kept.
    /// </summary>
    [Fact]
    public void GivenAStrategyWithNoPollStatus_WhenItIsSelected_ThenNoneIsRead()
    {
        // Given
        var aircraft = new FirstTrackerSource();
        var recording = new SecondTrackerSource();
        SwappingTrackerSource sut = new SwappingTrackerSourceFixture().WithEnumerable(
            [
                new TrackerSourceEntry(aircraft, "Aircraft", new PollStatusDouble(new(Due(Aircraft))), AircraftFleetDescription.Offered),
                new TrackerSourceEntry(recording, "Recording", new NoPollStatus(), AircraftFleetDescription.Offered),
            ]);
        var observed = new List<PollStatus>();
        using var subscription = sut.Status.Subscribe(observed.Add);

        // When
        sut.Select(sut.Entries[1]);

        // Then
        observed.Should().Equal([Due(Aircraft), PollStatus.None]);
    }

    /// <summary>B-060. A swap removes outgoing rows under their old description, publishes the incoming description, then adds cached incoming rows.</summary>
    [Fact]
    public void GivenTwoEntriesWithDescriptions_WhenTheSecondIsSelected_ThenItsDescriptionIsPublishedWithItsVehicles()
    {
        // Given
        var strategies = new SwappingTrackerSourceFixture();
        strategies.Aircraft.Report("a1b2c3");
        strategies.Vessels.Report("366999001");
        SwappingTrackerSource sut = strategies;
        var order = new List<string>();
        using var descriptions = sut.Description.Subscribe(description =>
            order.Add(ReferenceEquals(description, VesselFleetDescription.Offered) ? "vessel description" : "aircraft description"));
        using var vehicles = sut.Connect().Subscribe(changes =>
        {
            foreach (var change in changes)
            {
                order.Add($"{change.Reason}:{change.Key}");
            }
        });
        order.Clear();

        // When
        sut.Select(sut.Entries[1]);
        var late = new List<object>();
        using var lateSubscription = sut.Description.Subscribe(late.Add);

        // Then
        order.Should().Equal("Remove:a1b2c3", "vessel description", "Add:366999001");
        late.Should().ContainSingle().Which.Should().BeSameAs(VesselFleetDescription.Offered);
    }

    /// <summary>B-025, B-028. Capability follows the source even when live aircraft status has not yet left None.</summary>
    [Fact]
    public void GivenALivePollerAndANonPollingSource_WhenTheySwap_ThenDemandPollCapabilityChangesImmediately()
    {
        // Given
        SwappingTrackerSource sut = new SwappingTrackerSourceFixture().WithEnumerable(
            [
                new TrackerSourceEntry(new FirstTrackerSource(), "Aircraft", new PollStatusDouble(new(PollStatus.None)), AircraftFleetDescription.Offered),
                new TrackerSourceEntry(new SecondTrackerSource(), "Vessels", new NoPollStatus(), VesselFleetDescription.Offered),
            ]);
        var observed = new List<bool>();
        using var subscription = sut.CanDemandPoll.Subscribe(observed.Add);

        // When
        sut.Select(sut.Entries[1]);
        sut.Select(sut.Entries[0]);

        // Then
        observed.Should().Equal(true, false, true);
    }

    /// <summary>A status reporting only a due instant.</summary>
    /// <param name="instant">When the next poll is due.</param>
    /// <returns>The status.</returns>
    private static PollStatus Due(DateTimeOffset instant) => new() { NextDue = instant };

    /// <summary>Every member in the production assembly that would answer which source is live.</summary>
    /// <returns>The offending methods, which is the empty set.</returns>
    /// <remarks>
    /// Compiler-generated types are excluded: a registration lambda returning the seam is one. So is
    /// <see cref="TrackerSourceEntry"/>, which names the strategy registration paired with a name,
    /// not the one that is live (ADR-0015).
    /// </remarks>
    private static IEnumerable<string> Resolvers() =>
        typeof(ITrackerSource).Assembly.GetTypes()
            .Where(static type => !Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute)))
            .Where(static type => type != typeof(TrackerSourceEntry))
            .SelectMany(static type => type.GetMethods())
            .Where(static method => typeof(ITrackerSource).IsAssignableFrom(method.ReturnType))
            .Select(static method => $"{method.DeclaringType!.Name}.{method.Name}");

    /// <summary>The first strategy's due instant.</summary>
    private static readonly DateTimeOffset Aircraft = new(2026, 10, 15, 14, 33, 22, TimeSpan.Zero);

    /// <summary>What the first strategy reports once it is no longer live.</summary>
    private static readonly DateTimeOffset AircraftLater = new(2026, 10, 15, 14, 33, 37, TimeSpan.Zero);

    /// <summary>The second strategy's due instant.</summary>
    private static readonly DateTimeOffset Vessels = new(2026, 10, 15, 14, 33, 30, TimeSpan.Zero);
}

/// <summary>Builds the decorator over two strategies, and keeps a handle on each.</summary>
/// <remarks>The strategies are the arrangement, so a test that drives one holds the fixture rather than the set.</remarks>
[AutoFixture(typeof(SwappingTrackerSource))]
internal partial class SwappingTrackerSourceFixture
{
    public SwappingTrackerSourceFixture() =>
        WithEnumerable(
            [
                new TrackerSourceEntry(Aircraft, "Aircraft", new PollStatusDouble(AircraftStatus), AircraftFleetDescription.Offered),
                new TrackerSourceEntry(Vessels, "Vessels", new PollStatusDouble(VesselStatus), VesselFleetDescription.Offered),
            ]);

    /// <summary>Gets the strategy that is live until something selects the other.</summary>
    public FirstTrackerSource Aircraft { get; } = new();

    /// <summary>Gets the strategy a swap selects.</summary>
    public SecondTrackerSource Vessels { get; } = new();

    /// <summary>Gets the poll status the first strategy writes.</summary>
    public BehaviorSubject<PollStatus> AircraftStatus { get; } = new(PollStatus.None);

    /// <summary>Gets the poll status the second strategy writes.</summary>
    public BehaviorSubject<PollStatus> VesselStatus { get; } = new(PollStatus.None);
}

/// <summary>A per-type seam, the way a real strategy is named.</summary>
internal interface IFirstTrackerSource : ITrackerSourceStrategy;

/// <summary>The second per-type seam, which makes selection a choice rather than a default.</summary>
internal interface ISecondTrackerSource : ITrackerSourceStrategy;

/// <summary>A strategy whose <c>Connect</c> owns a resource; the real one's is a poller.</summary>
internal abstract class StubTrackerSource : ITrackerSourceStrategy
{
    /// <summary>Gets how many times a subscription started the resource.</summary>
    public int Started { get; private set; }

    /// <summary>Gets how many times a subscription ending stopped it.</summary>
    public int Stopped { get; private set; }

    /// <inheritdoc/>
    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        Observable.Using(
            () =>
            {
                Started++;

                return Disposable.Create(() => Stopped++);
            },
            _ => _vehicles.Connect());

    /// <summary>Reports a vehicle, the way a poll writing its set into a cache does.</summary>
    /// <param name="key">The vehicle's key.</param>
    public void Report(string key) => _vehicles.AddOrUpdate(new Aircraft(key, DateTimeOffset.UnixEpoch));

    private readonly SourceCache<TransportVehicle, string> _vehicles = new(static vehicle => vehicle.Key);
}

/// <summary>The strategy that is live until something selects the other.</summary>
internal sealed class FirstTrackerSource : StubTrackerSource, IFirstTrackerSource;

/// <summary>The strategy a swap selects.</summary>
internal sealed class SecondTrackerSource : StubTrackerSource, ISecondTrackerSource;
