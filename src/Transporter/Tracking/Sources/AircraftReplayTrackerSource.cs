using System;
using DynamicData;
using Transporter.Model;

namespace Transporter.Tracking.Sources;

/// <summary>
/// The replay chain's strategy: the live <see cref="AircraftTrackerSource"/> under a seam of its
/// own, so the selector can tell two instances of one class apart.
/// </summary>
/// <remarks>
/// <para>
/// It projects nothing, polls nothing and holds no cache — B-022 forbids a projection, a client or
/// a cache of replay's own, and what this delegates to is the live class over the replay contract
/// (B-013). The seam is the only thing it adds, and the reason is registration: its strategy alias
/// and its <see cref="TrackerSourceEntry"/> each resolve this instance by its seam, which a second
/// <see cref="AircraftTrackerSource"/> registered beside the live one would make ambiguous.
/// </para>
/// <para>
/// Rejected: making <see cref="AircraftTrackerSource"/> implement both seams, which puts both
/// instances behind both and leaves the ambiguity exactly where it was; and splitting it into a
/// live subclass and a replay subclass, which edits a delivered type of another Feature to say
/// what one line of delegation says here.
/// </para>
/// </remarks>
internal sealed class AircraftReplayTrackerSource : IAircraftReplayTrackerSource
{
    /// <summary>Initializes a new instance of the <see cref="AircraftReplayTrackerSource"/> class.</summary>
    /// <param name="replayed">The live strategy, built over the replay contract at registration.</param>
    public AircraftReplayTrackerSource(ITrackerSource replayed)
    {
        _replayed = replayed;
    }

    /// <inheritdoc/>
    public IObservable<IChangeSet<TransportVehicle, string>> Connect() => _replayed.Connect();

    private readonly ITrackerSource _replayed;
}
