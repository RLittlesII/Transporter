using System;
using Rocket.Surgery.Airframe;
using Transporter.Features.Fleet.ViewModels;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;

namespace Transporter.UnitTests.Container;

/// <summary>
/// Every service the first window needs resolved, from the one the page takes down to the seam
/// (`0047`).
/// </summary>
/// <remarks>
/// The application threw naming <see cref="IFleetTracker"/>, two hops below the type it was
/// activating. A case per hop fails on the registration that is actually missing, where one
/// resolution of the view model reports whichever service the container reached first and leaves
/// the reader to walk the chain back.
/// </remarks>
public sealed class WindowServiceCases : TheoryData<Type>
{
    public WindowServiceCases()
    {
        Add(typeof(FleetViewModel));
        Add(typeof(FleetSummaryViewModel));
        Add(typeof(IFleetTracker));
        Add(typeof(ITrackerSource));
        Add(typeof(IObservable<FleetSourceDescription>));
        Add(typeof(ISchedulerProvider));
    }
}
