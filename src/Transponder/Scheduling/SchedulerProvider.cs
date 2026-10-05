using System.Reactive.Concurrency;
using Rocket.Surgery.Airframe;

namespace Transponder.Scheduling;

/// <summary>
/// The application's two schedulers, behind the one interface everything time-based takes.
/// </summary>
/// <remarks>
/// <para>
/// <c>Rocket.Surgery.Airframe.Core</c> declares <see cref="ISchedulerProvider"/> and ships no
/// implementation, so this is the repository's. Taking the provider rather than a bare
/// <see cref="IScheduler"/> is what keeps "which thread" a stated choice at every call site: a poll
/// belongs on <see cref="BackgroundThread"/>, and anything a view binds to belongs on
/// <see cref="UserInterfaceThread"/>.
/// </para>
/// <para>
/// Both schedulers arrive by constructor rather than being read from
/// <see cref="CurrentThreadScheduler"/> and <see cref="TaskPoolScheduler"/> inside the members. The
/// composition root chooses them once, and a test builds this type through its generated fixture
/// with one <c>TestScheduler</c> in both positions — so time advances for the whole chain by
/// advancing one object, and no test substitutes the interface
/// (<c>test-from-scenarios</c> § "Time is injected, always").
/// </para>
/// </remarks>
internal sealed class SchedulerProvider : ISchedulerProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulerProvider"/> class.
    /// </summary>
    /// <param name="userInterfaceThread">Where work a view observes is scheduled.</param>
    /// <param name="backgroundThread">Where a poll, and anything else off the view, runs.</param>
    public SchedulerProvider(IScheduler userInterfaceThread, IScheduler backgroundThread)
    {
        UserInterfaceThread = userInterfaceThread;
        BackgroundThread = backgroundThread;
    }

    /// <inheritdoc/>
    public IScheduler UserInterfaceThread { get; }

    /// <inheritdoc/>
    public IScheduler BackgroundThread { get; }
}
