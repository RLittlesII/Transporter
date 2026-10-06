using System;
using System.Reactive.Linq;
using DynamicData;
using Transponder.Integrations.OpenSky;
using Transponder.Model;

namespace Transponder.Tracking.Sources;

/// <summary>
/// The aircraft strategy: it reads the snapshot cache's changesets and projects each one into a
/// domain vehicle, which is the only thing that genuinely varies between an aircraft feed and a
/// vessel feed (B-034, ADR-0002 item 6).
/// </summary>
/// <remarks>
/// Takes the client, the cache and the mapper by constructor and constructs none of them. The
/// projection runs per change rather than over the whole set, so an aircraft nothing reported about
/// produces no work.
/// </remarks>
internal sealed class AircraftTrackerSource : IAircraftTrackerSource
{
    /// <summary>Initializes a new instance of the <see cref="AircraftTrackerSource"/> class.</summary>
    /// <param name="client">The poller this strategy's subscription owns (B-040).</param>
    /// <param name="snapshots">The plain keyed store the snapshot client writes each fetched set into.</param>
    /// <param name="mapper">The projection this strategy owns (B-034).</param>
    public AircraftTrackerSource(
        AircraftSnapshotClient client,
        SourceCache<AircraftSnapshot, string> snapshots,
        AircraftSnapshotMapper mapper)
    {
        _client = client;
        _snapshots = snapshots;
        _mapper = mapper;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// The subscription owns the poll (B-040): the first subscriber starts it and the last one to
    /// leave stops it, so a strategy the decorator switches away from stops spending credits because
    /// its subscription is gone, and switching back starts it again. Nothing has to remember to
    /// dispose anything, and no strategy is left unusable by having been swapped away.
    /// </para>
    /// <para>
    /// The stream is re-keyed on the vehicle's own key rather than on the cache's, because the cache
    /// is keyed on <c>icao24</c> as the wire spelled it and the projection lowercases it: leaving the
    /// two different is the split into two entries B-037 exists to prevent.
    /// </para>
    /// </remarks>
    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        Observable.Using(
            () => _client.Poll(),
            _ => _snapshots.Connect()
                .Transform(TransportVehicle (snapshot) => _mapper.Project(snapshot))
                .ChangeKey(static vehicle => vehicle.Key));

    private readonly AircraftSnapshotClient _client;
    private readonly SourceCache<AircraftSnapshot, string> _snapshots;
    private readonly AircraftSnapshotMapper _mapper;
}
