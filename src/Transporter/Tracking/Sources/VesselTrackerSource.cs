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
        : this(client.Listen, snapshots, mapper)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="VesselTrackerSource"/> class over a supplied lifetime.</summary>
    /// <param name="subscribe">Starts the source until its returned lifetime is disposed.</param>
    /// <param name="snapshots">The cache retained by this source.</param>
    /// <param name="mapper">The sole vessel projection.</param>
    private VesselTrackerSource(Func<IDisposable> subscribe, SourceCache<VesselSnapshot, string> snapshots, VesselSnapshotMapper mapper)
    {
        _subscribe = subscribe;
        _snapshots = snapshots;
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        Observable.Using(
            _subscribe,
            _ => _snapshots.Connect()
                .Filter(static snapshot => snapshot.Reported.IsSome)
                .Transform(TransportVehicle (snapshot) => _mapper.Project(snapshot)));

    /// <summary>Builds the same projection over a supplied playback lifetime and separate cache.</summary>
    /// <param name="subscribe">Starts playback until its returned lifetime is disposed.</param>
    /// <param name="snapshots">The replay cache, retained across recording rewinds.</param>
    /// <param name="mapper">The sole vessel projection.</param>
    /// <returns>A source using the supplied subscription rather than a live socket.</returns>
    internal static VesselTrackerSource FromSubscription(
        Func<IDisposable> subscribe,
        SourceCache<VesselSnapshot, string> snapshots,
        VesselSnapshotMapper mapper) =>
        new(subscribe, snapshots, mapper);

    private readonly Func<IDisposable> _subscribe;
    private readonly SourceCache<VesselSnapshot, string> _snapshots;
    private readonly VesselSnapshotMapper _mapper;
}
