using System;
using System.Reactive.Linq;
using DynamicData;
using LanguageExt;
using Transporter.Model;

namespace Transporter.Tracking;

/// <summary>The stage that derives movement, first in the chain (fleet-pipeline § 7).</summary>
internal static class FleetMovement
{
    /// <summary>Carries each vehicle's replaced vehicle, leg and running total on its element.</summary>
    /// <param name="source">The changesets the seam reports.</param>
    /// <returns>The same changes, each element carrying its movement.</returns>
    /// <remarks>
    /// A stage of its own because <c>Transform</c> hands its factory the previous source, never the
    /// previous element, and a running total is a fold over elements. The cache is per subscription
    /// and holds exactly what the stage last emitted, so it dies with the subscription (B-002).
    /// </remarks>
    internal static IObservable<IChangeSet<MovedVehicle, string>> Move(this IObservable<IChangeSet<TransportVehicle, string>> source) =>
        Observable.Create<IChangeSet<MovedVehicle, string>>(observer =>
        {
            var moved = new ChangeAwareCache<MovedVehicle, string>();

            return source.Subscribe(
                changes =>
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                moved.AddOrUpdate(Entered(change.Current), change.Key);
                                break;
                            case ChangeReason.Update:
                                moved.AddOrUpdate(Updated(moved.Lookup(change.Key), change), change.Key);
                                break;
                            case ChangeReason.Remove:
                                moved.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                moved.Refresh(change.Key);
                                break;
                        }
                    }

                    var captured = moved.CaptureChanges();

                    if (captured.Count > 0)
                    {
                        observer.OnNext(captured);
                    }
                },
                observer.OnError,
                observer.OnCompleted);
        });

    /// <summary>A vehicle that has just entered: nothing replaced, no leg, nothing flown.</summary>
    /// <param name="vehicle">The vehicle.</param>
    /// <returns>Its element.</returns>
    private static MovedVehicle Entered(TransportVehicle vehicle) =>
        new() { Vehicle = vehicle, Replaced = Option<TransportVehicle>.None, Leg = Option<double>.None, Travelled = 0 };

    /// <summary>A vehicle an update replaced: the leg between the two positions, added to the total.</summary>
    /// <param name="last">The element the stage last emitted for this key.</param>
    /// <param name="change">The update.</param>
    /// <returns>Its element.</returns>
    /// <remarks>A missing position on either side is no leg, and the total stands rather than adding a zero.</remarks>
    private static MovedVehicle Updated(DynamicData.Kernel.Optional<MovedVehicle> last, Change<TransportVehicle, string> change)
    {
        if (!last.HasValue)
        {
            return Entered(change.Current);
        }

        var replaced = change.Previous.HasValue ? change.Previous.Value : last.Value.Vehicle;
        var leg = replaced.Position.Bind(before => change.Current.Position.Map(after => GreatCircle.Metres(before, after)));

        return new()
        {
            Vehicle = change.Current,
            Replaced = Option<TransportVehicle>.Some(replaced),
            Leg = leg,
            Travelled = last.Value.Travelled + leg.IfNone(0),
        };
    }
}
