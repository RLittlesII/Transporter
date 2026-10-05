using System;
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
/// Takes the cache and the mapper by constructor and constructs neither. The projection runs per
/// change rather than over the whole set, so an aircraft nothing reported about produces no work.
/// </remarks>
internal sealed class AircraftTrackerSource : IAircraftTrackerSource
{
    /// <summary>Initializes a new instance of the <see cref="AircraftTrackerSource"/> class.</summary>
    /// <param name="snapshots">The plain keyed store the snapshot client writes each fetched set into.</param>
    /// <param name="mapper">The projection this strategy owns (B-034).</param>
    public AircraftTrackerSource(SourceCache<AircraftSnapshot, string> snapshots, AircraftSnapshotMapper mapper)
    {
        _snapshots = snapshots;
        _mapper = mapper;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stream is re-keyed on the vehicle's own key rather than on the cache's, because the cache
    /// is keyed on <c>icao24</c> as the wire spelled it and the projection lowercases it: leaving the
    /// two different is the split into two entries B-037 exists to prevent.
    /// </remarks>
    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        _snapshots.Connect()
            .Transform(TransportVehicle (snapshot) => _mapper.Project(snapshot))
            .ChangeKey(static vehicle => vehicle.Key);

    private readonly SourceCache<AircraftSnapshot, string> _snapshots;
    private readonly AircraftSnapshotMapper _mapper;
}
