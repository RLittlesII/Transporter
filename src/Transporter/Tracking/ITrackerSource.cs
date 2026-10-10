using System;
using DynamicData;
using Transporter.Model;

namespace Transporter.Tracking;

/// <summary>
/// The strategy seam: a changeset stream of domain vehicles, and nothing that says where they came
/// from. Every source is substitutable through this without a cast (B-033, ADR-0002 item 6).
/// </summary>
public interface ITrackerSource
{
    /// <summary>Connects to the live vehicles.</summary>
    /// <returns>One add, update or remove per vehicle that changed.</returns>
    IObservable<IChangeSet<TransportVehicle, string>> Connect();
}
