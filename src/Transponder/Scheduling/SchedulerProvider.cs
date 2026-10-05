using System.Reactive.Concurrency;
using Rocket.Surgery.Airframe;

namespace Transponder.Scheduling;

/// <summary>
/// The application's two schedulers, behind the one interface everything time-based takes.
/// </summary>
/// <remarks>
/// <c>Rocket.Surgery.Airframe.Core</c> declares <see cref="ISchedulerProvider"/> and ships no
/// implementation, so this is the repository's. Taking the provider rather than a bare
/// <see cref="IScheduler"/> is what keeps "which thread" a stated choice at every call site: a poll
/// belongs on <see cref="BackgroundThread"/>, and anything a view binds to belongs on
/// <see cref="UserInterfaceThread"/>. A test substitutes the provider and hands the same
/// <c>TestScheduler</c> to both, so one object advances time for the whole chain
/// (<c>test-from-scenarios</c> § "Time is injected, always").
/// </remarks>
internal sealed class SchedulerProvider : ISchedulerProvider
{
    /// <inheritdoc/>
    public IScheduler UserInterfaceThread => CurrentThreadScheduler.Instance;

    /// <inheritdoc/>
    public IScheduler BackgroundThread => TaskPoolScheduler.Default;
}
