using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using ReactiveMarbles.Locator;
using ReactiveMarbles.Mvvm;

namespace Transporter.UnitTests;

/// <summary>
/// Puts ReactiveMarbles' core registrations in its locator once for the whole test assembly, which
/// is what the application's composition root does for the application (`0058`).
/// </summary>
/// <remarks>
/// <c>RxCommand</c> reads its exception handler from that locator whether or not a scheduler is
/// passed to it, and the locator is process-wide — so a test that registered its own would be
/// writing state every other test reads. Nothing here decides timing: every view model takes its
/// schedulers by constructor and every test passes its own <c>TestScheduler</c>, so what is
/// registered here is only the handler and the defaults nothing in these tests uses
/// (`fleet-pipeline` B-005).
/// </remarks>
internal static class ReactiveMarblesRegistrations
{
    [ModuleInitializer]
    internal static void Register() =>
        ServiceLocator.Current().AddCoreRegistrations(
            CurrentThreadScheduler.Instance,
            TaskPoolScheduler.Default,
            Observer.Create<Exception>(static error => ExceptionDispatchInfo.Capture(error).Throw()));
}
