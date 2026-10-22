using System.Reactive.Linq;
using DynamicData;
using Transporter.Integrations.AisStream;
using Transporter.Model;

namespace Transporter.Tracking.Sources;

/// <summary>Projects the vessel client's cache into the common tracker stream.</summary>
internal sealed class VesselTrackerSource : IVesselTrackerSource
{
    /// <summary>Initializes a new instance of the <see cref="VesselTrackerSource"/> class.</summary>
    /// <param name="client">The client owned by this strategy's subscription.</param>
    /// <param name="snapshots">The cache the client writes.</param>
    /// <param name="mapper">The sole vessel projection.</param>
    public VesselTrackerSource(VesselClient client, SourceCache<VesselSnapshot, string> snapshots, VesselSnapshotMapper mapper)
    {
        _client = client;
        _snapshots = snapshots;
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        Observable.Using(
            () => _client.Listen(),
            _ => _snapshots.Connect()
                .Filter(static snapshot => snapshot.Reported.IsSome)
                .Transform(TransportVehicle (snapshot) => _mapper.Project(snapshot)));

    private readonly VesselClient _client;
    private readonly SourceCache<VesselSnapshot, string> _snapshots;
    private readonly VesselSnapshotMapper _mapper;
}
