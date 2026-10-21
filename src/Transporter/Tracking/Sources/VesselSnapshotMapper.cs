using Riok.Mapperly.Abstractions;
using Transporter.Integrations.AisStream;
using Transporter.Model;

namespace Transporter.Tracking.Sources;

/// <summary>Projects a cached vessel snapshot into its domain vehicle.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Both)]
internal sealed partial class VesselSnapshotMapper
{
    /// <summary>Projects one timed snapshot without converting canonical AIS units.</summary>
    /// <param name="snapshot">The cached provider values.</param>
    /// <returns>The vessel.</returns>
    [MapperIgnoreSource(nameof(VesselSnapshot.Mmsi))]
    [MapperIgnoreSource(nameof(VesselSnapshot.Reported))]
    [MapperIgnoreTarget(nameof(Vessel.Key))]
    [MapperIgnoreTarget(nameof(Vessel.LastContact))]
    internal partial Vessel Project(VesselSnapshot snapshot);

    /// <summary>Constructs the vessel with the stable key and reported instant.</summary>
    /// <param name="snapshot">The timed snapshot.</param>
    /// <returns>The vessel to populate.</returns>
    [ObjectFactory]
    private static Vessel Create(VesselSnapshot snapshot) =>
        new Vessel(snapshot.Mmsi, snapshot.Reported.IfNone(static () => throw new InvalidOperationException("A vessel must be timed before projection.")));
}
