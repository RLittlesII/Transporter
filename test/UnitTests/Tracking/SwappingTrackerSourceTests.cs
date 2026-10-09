using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using DynamicData;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Model;
using Transponder.Tracking;
using Transponder.Tracking.Sources;

namespace Transponder.UnitTests.Tracking;

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
}

/// <summary>Builds the decorator over two strategies, and keeps a handle on each.</summary>
/// <remarks>The strategies are the arrangement, so a test that drives one holds the fixture rather than the set.</remarks>
[AutoFixture(typeof(SwappingTrackerSource))]
internal partial class SwappingTrackerSourceFixture
{
    public SwappingTrackerSourceFixture() =>
        WithEnumerable([new TrackerSourceEntry(Aircraft, "Aircraft"), new TrackerSourceEntry(Vessels, "Vessels")]);

    /// <summary>Gets the strategy that is live until something selects the other.</summary>
    public FirstTrackerSource Aircraft { get; } = new();

    /// <summary>Gets the strategy a swap selects.</summary>
    public SecondTrackerSource Vessels { get; } = new();
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
