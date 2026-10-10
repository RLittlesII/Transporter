using System.Reactive.Concurrency;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Scheduling;

namespace Transporter.UnitTests.Scheduling;

/// <summary>
/// Builds a <see cref="SchedulerProvider"/>, so nothing substitutes
/// <c>Rocket.Surgery.Airframe.ISchedulerProvider</c> and every test advances the real type.
/// </summary>
/// <remarks>
/// Hand-written rather than generated, for the reason
/// <see cref="Integrations.OpenSky.Fixtures.AircraftSnapshotFixture"/> gives in its own remarks: the
/// generator names each builder method after its parameter's <em>type</em>, and
/// <see cref="SchedulerProvider"/> takes two <see cref="IScheduler"/> parameters — so it emits two
/// identical <c>WithScheduler</c> overloads and the fixture will not compile. One
/// <see cref="WithTestScheduler"/> is the method a test wants anyway: both members have to be the
/// same scheduler for time to advance once for the whole chain.
/// </remarks>
internal sealed class SchedulerProviderFixture : AutoFixtureBase<SchedulerProviderFixture>
{
    /// <summary>Puts one scheduler in both positions, which is what advancing time once requires.</summary>
    /// <param name="scheduler">The scheduler a test advances.</param>
    /// <returns>The fixture, so building chains.</returns>
    public SchedulerProviderFixture WithTestScheduler(TestScheduler scheduler) =>
        With(ref _userInterfaceThread, scheduler).With(ref _backgroundThread, scheduler);

    /// <summary>Puts a scheduler on the user-interface thread alone.</summary>
    /// <param name="scheduler">The scheduler work a view observes is scheduled on.</param>
    /// <returns>The fixture, so building chains.</returns>
    public SchedulerProviderFixture WithUserInterfaceThread(IScheduler scheduler) =>
        With(ref _userInterfaceThread, scheduler);

    /// <summary>Puts a scheduler on the background thread alone.</summary>
    /// <param name="scheduler">The scheduler a poll runs on.</param>
    /// <returns>The fixture, so building chains.</returns>
    public SchedulerProviderFixture WithBackgroundThread(IScheduler scheduler) =>
        With(ref _backgroundThread, scheduler);

    /// <summary>Takes the subject, as every fixture here is taken.</summary>
    /// <param name="fixture">The fixture to build.</param>
    public static implicit operator SchedulerProvider(SchedulerProviderFixture fixture) => fixture.Build();

    private SchedulerProvider Build() => new(_userInterfaceThread, _backgroundThread);

    private IScheduler _userInterfaceThread = new TestScheduler();
    private IScheduler _backgroundThread = new TestScheduler();
}
