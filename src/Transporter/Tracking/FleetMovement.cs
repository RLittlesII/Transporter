using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using LanguageExt;
using Transporter.Model;

namespace Transporter.Tracking;

/// <summary>The stage that folds movement into each vehicle's element.</summary>
internal static class FleetMovement
{
    internal static IObservable<IChangeSet<MovedVehicle, string>> Move(
        this IObservable<IChangeSet<TransportVehicle, string>> source,
        IObservable<Func<TransportVehicle, Option<double>>> measures,
        Func<TimeSpan> staleAfter,
        int trailBound) =>
        Observable.Create<IChangeSet<MovedVehicle, string>>(observer =>
        {
            var moved = new ChangeAwareCache<MovedVehicle, string>();
            Func<TransportVehicle, Option<double>> measure = static _ => Option<double>.None;
            var descriptions = measures.Subscribe(value => measure = value, observer.OnError);
            var subscription = source.Subscribe(
                changes =>
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                moved.AddOrUpdate(Entered(change.Current, measure), change.Key);
                                break;
                            case ChangeReason.Update:
                                moved.AddOrUpdate(Updated(moved.Lookup(change.Key), change, measure, staleAfter(), trailBound), change.Key);
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
            return new CompositeDisposable(descriptions, subscription);
        });

    private static MovedVehicle Entered(TransportVehicle vehicle, Func<TransportVehicle, Option<double>> measure) =>
        new()
        {
            Vehicle = vehicle,
            Replaced = Option<TransportVehicle>.None,
            Leg = Option<double>.None,
            Travelled = 0,
            Trail = Appended([], vehicle, Option<TransportVehicle>.None, measure, TimeSpan.Zero, 1),
        };

    private static MovedVehicle Updated(
        DynamicData.Kernel.Optional<MovedVehicle> last,
        Change<TransportVehicle, string> change,
        Func<TransportVehicle, Option<double>> measure,
        TimeSpan staleAfter,
        int trailBound)
    {
        if (!last.HasValue)
        {
            return Entered(change.Current, measure);
        }

        var replaced = change.Previous.HasValue ? change.Previous.Value : last.Value.Vehicle;
        var leg = replaced.Position.Bind(before => change.Current.Position.Map(after => GreatCircle.Metres(before, after)));

        return new()
        {
            Vehicle = change.Current,
            Replaced = Option<TransportVehicle>.Some(replaced),
            Leg = leg,
            Travelled = last.Value.Travelled + leg.IfNone(0),
            Trail = Appended(last.Value.Trail, change.Current, replaced, measure, staleAfter, trailBound),
        };
    }

    private static ImmutableList<TrailPoint> Appended(
        ImmutableList<TrailPoint> trail,
        TransportVehicle vehicle,
        Option<TransportVehicle> replaced,
        Func<TransportVehicle, Option<double>> measure,
        TimeSpan staleAfter,
        int bound) =>
        vehicle.Position.Match(
            position =>
            {
                if (trail.Count > 0 && trail[^1].Position == position)
                {
                    return trail;
                }

                var point = new TrailPoint
                {
                    Position = position,
                    LastContact = vehicle.LastContact,
                    Measure = measure(vehicle),
                    Distance = trail.Count == 0 ? Option<double>.None : GreatCircle.Metres(trail[^1].Position, position),
                    FollowsGap = trail.Count > 0 && replaced.Exists(previous => vehicle.LastContact - previous.LastContact > staleAfter),
                };
                var appended = trail.Add(point);
                return appended.Count > bound ? appended.RemoveAt(0) : appended;
            },
            () => trail);
}
