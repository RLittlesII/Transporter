using System.Reactive.Disposables;
using DynamicData;
using Transponder.Integrations.OpenSky;
using Transponder.UnitTests.Integrations.OpenSky.Fixtures;

namespace Transponder.UnitTests.Integrations.OpenSky.Replay;

/// <summary>
/// The live poller, counted: it reports one aircraft the moment it is polled and records when the
/// poll was stopped.
/// </summary>
/// <remarks>
/// <para>
/// It stands in for the live transport rather than for the chain above it — the cache, the
/// projection, the strategy, the decorator and the tracker in a swap test are all the real ones.
/// What it replaces is the one thing a swap test must not do, which is reach the provider: the live
/// chain would fetch a token and poll, and B-011 is about a composition that has neither.
/// </para>
/// <para>
/// The aircraft it reports carries a key no recording holds, so a vehicle in the bound collection
/// says which source produced it without the test asking anything else.
/// </para>
/// </remarks>
internal sealed class LiveSnapshots : IAircraftSnapshotClient
{
    /// <summary>Initializes a new instance of the <see cref="LiveSnapshots"/> class.</summary>
    /// <param name="snapshots">The live chain's own cache, which a real poll writes into.</param>
    internal LiveSnapshots(SourceCache<AircraftSnapshot, string> snapshots)
    {
        _snapshots = snapshots;
    }

    /// <summary>The key the live aircraft reports under, which no recorded payload carries.</summary>
    internal const string Key = "11ff22";

    /// <summary>The country the live aircraft reports, which no recorded payload carries either.</summary>
    internal const string Country = "Liveland";

    /// <summary>Gets how many times the poll was started.</summary>
    internal int Polls { get; private set; }

    /// <summary>Gets how many times the poll was stopped.</summary>
    internal int Stops { get; private set; }

    /// <inheritdoc/>
    public IDisposable Poll()
    {
        Polls++;
        _snapshots.AddOrUpdate((AircraftSnapshot) new AircraftSnapshotFixture()
            .WithIcao24(Key)
            .WithOriginCountry(Country)
            .WithLatitude(29.7604)
            .WithLongitude(-95.3698));

        return Disposable.Create(() => Stops++);
    }

    private readonly SourceCache<AircraftSnapshot, string> _snapshots;
}
