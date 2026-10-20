namespace Transporter.UnitTests.Analyzers;

/// <summary>Violations and allowed counterparts for fleet-dashboard B-017, B-018 and B-022.</summary>
public sealed class DashboardBoundaryCases : TheoryData<string, string>
{
    /// <summary>Initializes a new instance of the <see cref="DashboardBoundaryCases"/> class.</summary>
    public DashboardBoundaryCases()
    {
        Add(
            DashboardBoundarySources.Prelude +
            """
            namespace Transporter.Integrations.OpenSky { class SnapshotClient { } }
            namespace Gui.Widgets
            {
                class Surface : Microsoft.Maui.Controls.ContentPage
                {
                    void Run() { var client = new Transporter.Integrations.OpenSky.SnapshotClient(); }
                }
            }
            """,
            "TRN0006");
        Add(
            DashboardBoundarySources.Prelude +
            """
            namespace Transporter.Integrations.OpenSky { class SnapshotClient { } }
            namespace Transporter.Features.Fleet.Presentation
            {
                class Surface : ReactiveMarbles.Mvvm.RxObject
                {
                    void Run() { var client = new Transporter.Integrations.OpenSky.SnapshotClient(); }
                }
            }
            """,
            "TRN0006");
        Add(
            DashboardBoundarySources.View(
            """
            System.Net.Sockets.TcpClient client = new();
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            System.Timers.Timer timer = new();
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            System.Net.Http.HttpClient client = new();
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run(System.Threading.Tasks.Task<int> task) { var result = task.Result; }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run(System.Threading.Tasks.Task task) { task.Wait(); }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run(System.Threading.Tasks.Task task) { task.GetAwaiter().GetResult(); }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run() { System.Threading.Thread.Sleep(1); }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run() { var task = Akka.Actor.Requests.Ask<int>(new object(), new object()); }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run(System.Reactive.Concurrency.IScheduler scheduler) { var tick =
            System.Reactive.Linq.Observable.Interval(System.TimeSpan.FromSeconds(1), scheduler); }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run(System.Reactive.Concurrency.IScheduler scheduler) { var tick =
            System.Reactive.Linq.Observable.Timer(System.TimeSpan.FromSeconds(1), scheduler).Select(_ => true); }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run() { var tick = System.Reactive.Linq.Observable.Timer(System.TimeSpan.FromSeconds(1),
            System.Reactive.Concurrency.Scheduler.Default).Select(_ => false); }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run(System.Reactive.Concurrency.IScheduler scheduler) { var tick =
            System.Reactive.Linq.Observable.Timer(System.TimeSpan.FromSeconds(1), scheduler).Select(_ => {
            System.Console.WriteLine(1); return false; }); }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run(System.Reactive.Concurrency.IScheduler scheduler) {
            System.Reactive.Linq.Observable.Timer(System.TimeSpan.FromSeconds(1), scheduler)
                .Select(_ => false).Subscribe(_ => Poll()); }
            void Poll() { }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run(System.Reactive.Concurrency.IScheduler scheduler) {
            var deadline = System.Reactive.Linq.Observable.Timer(System.TimeSpan.FromSeconds(1), scheduler).Select(_ => false);
            deadline.Do(_ => Poll()); }
            void Poll() { }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run(System.Reactive.Concurrency.IScheduler scheduler) {
            System.Reactive.Linq.Observable.Timer(System.TimeSpan.FromSeconds(1), scheduler)
                .Select(_ => false).Subscribe(Poll); }
            void Poll(bool closed) { }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            int DomainState;
            void Run(System.Reactive.Concurrency.IScheduler scheduler) {
            System.Reactive.Linq.Observable.Timer(System.TimeSpan.FromSeconds(1), scheduler)
                .Select(_ => false).AsValue(_ => { DomainState = 1; }, scheduler, () => false); }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            IObservable<bool> Run(System.Reactive.Concurrency.IScheduler scheduler) =>
            System.Reactive.Linq.Observable.Timer(System.TimeSpan.FromSeconds(1), scheduler).Select(_ => false);
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            int DomainState;
            void Run(System.Reactive.Concurrency.IScheduler scheduler) {
            Action<bool> changed = _ => DomainState = 1;
            System.Reactive.Linq.Observable.Timer(System.TimeSpan.FromSeconds(1), scheduler)
                .Select(_ => false).AsValue(changed, scheduler, () => false); }
            """),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run(Microsoft.Maui.Dispatching.IDispatcher dispatcher) {
            dispatcher.CreateTimer().Start(); }
            """,
            true),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run() { Microsoft.Maui.Controls.Device.StartTimer(System.TimeSpan.FromSeconds(1), () => true); }
            """,
            true),
            "TRN0019");
        Add(
            DashboardBoundarySources.View(
            """
            void Run() { var rows = new[] { 1, 2 }.Where(x => x > 0); }
            """),
            "TRN0020");
        Add(
            DashboardBoundarySources.View(
            """
            void Run() { var rows = new[] { 1, 2 }.OrderBy(x => x); }
            """),
            "TRN0020");
        Add(
            DashboardBoundarySources.View(
            """
            void Run() { var rows = new[] { 1, 2 }.GroupBy(x => x); }
            """),
            "TRN0020");
        Add(
            DashboardBoundarySources.View(
            """
            void Run(System.DateTimeOffset contact) { var stale = System.DateTimeOffset.UtcNow - contact; }
            """),
            "TRN0020");
        Add(
            DashboardBoundarySources.Feature(
            """
            object Run(Transporter.Model.TransportVehicle item) => (Transporter.Model.Aircraft)item;
            """),
            "TRN0021");
        Add(
            DashboardBoundarySources.Feature(
            """
            bool Run(Transporter.Model.TransportVehicle item) => item is Transporter.Model.Aircraft;
            """),
            "TRN0021");
        Add(
            DashboardBoundarySources.Feature(
            """
            object Run(Transporter.Model.TransportVehicle item) => item as Transporter.Model.Aircraft;
            """),
            "TRN0021");
        Add(
            DashboardBoundarySources.Feature(
            """
            int Run(Transporter.Model.TransportVehicle item) => item switch { Transporter.Model.Aircraft _ => 1, _ =>
            0 };
            """),
            "TRN0021");
        Add(
            DashboardBoundarySources.View(
            """
            System.Net.Http.HttpClient client = new();
            """,
            true),
            "TRN0019");
    }
}

/// <summary>Presentation operations the boundary rules must leave intact.</summary>
public sealed class DashboardAllowedCases : TheoryData<string>
{
    /// <summary>Initializes a new instance of the <see cref="DashboardAllowedCases"/> class.</summary>
    public DashboardAllowedCases()
    {
        Add(DashboardBoundarySources.View(
            """
            bool IsSwapping;
            void Run(System.Reactive.Concurrency.IScheduler scheduler) {
            System.Reactive.Linq.Observable.Timer(System.TimeSpan.FromSeconds(10), scheduler).Select(_ => false)
                .AsValue(_ => RaisePropertyChanged(nameof(IsSwapping)), scheduler, () => false); }
            """));
        Add(DashboardBoundarySources.View(
            """
            void Run(System.Reactive.Concurrency.IScheduler scheduler) { var deadline =
            System.Reactive.Linq.Observable.Timer(System.TimeSpan.FromSeconds(10), scheduler).Select(_ => false); }
            """));
        Add(DashboardBoundarySources.View(
            """
            void Run() { var task = Akka.Actor.Requests.Ask<int>(new object(), new object(),
            System.TimeSpan.FromSeconds(2)); }
            """));
        Add(DashboardBoundarySources.View(
            """
            void Run() { var names = new[] { 1, 2 }.Select(x => x.ToString()); }
            """));
        Add(DashboardBoundarySources.View(
            """
            void Run() { Animate(); } void Animate() { }
            """,
            true));
        Add(DashboardBoundarySources.Prelude +
            """
            namespace Transporter.Tracking.Sources { class Description { object
            Read(Transporter.Model.TransportVehicle item) => (Transporter.Model.Aircraft)item; } }
            """);
        Add(DashboardBoundarySources.Prelude + "namespace Example { class Utility { void Wait(System.Threading.Tasks.Task task) { task.Wait(); } } }");
    }
}

/// <summary>Synthetic symbols keep diagnostic tests independent of runtime transports.</summary>
internal static class DashboardBoundarySources
{
    internal const string Prelude = """
        using System;
        using System.Linq;
        using System.Reactive.Linq;
        namespace ReactiveMarbles.Mvvm { public class RxObject { protected void RaisePropertyChanged(string name) { } } }
        namespace Microsoft.Maui.Controls { public class Element { } public class ContentPage : Element { } public static class Device { public static void
        StartTimer(TimeSpan interval, Func<bool> callback) { } } }
        namespace Microsoft.Maui.Dispatching
        {
            public interface IDispatcher { IDispatcherTimer CreateTimer(); }
            public interface IDispatcherTimer { void Start(); }
        }
        namespace Transporter.Model { public class TransportVehicle { } public class Aircraft : TransportVehicle { } }
        namespace System.Reactive.Concurrency { public interface IScheduler { } public static class Scheduler { public
        static IScheduler Default => null; } }
        namespace System.Reactive.Linq
        {
            public static class Observable
            {
                public static IObservable<long> Timer(TimeSpan due, System.Reactive.Concurrency.IScheduler scheduler) => null;
                public static IObservable<long> Interval(TimeSpan due, System.Reactive.Concurrency.IScheduler scheduler) => null;
                public static object AsValue(this IObservable<bool> source, Action<bool> changed,
                    System.Reactive.Concurrency.IScheduler scheduler, Func<bool> initial) => null;
                public static IDisposable Subscribe(this IObservable<bool> source, Action<bool> action) => null;
                public static IObservable<bool> Do(this IObservable<bool> source, Action<bool> action) => null;
                public static IObservable<bool> Select(this IObservable<long> source, Func<long, bool> selector) => null;
            }
        }
        namespace Akka.Actor
        {
            public static class Requests
            {
                public static System.Threading.Tasks.Task<T> Ask<T>(object actor, object message, TimeSpan? timeout = null) => null;
            }
        }
        """;

    /// <summary>Builds a semantic view or view model, without relying on its folder or name.</summary>
    /// <param name="member">The member under analysis.</param>
    /// <param name="head">Whether the source is a head view.</param>
    /// <returns>The synthetic compilation.</returns>
    internal static string View(string member, bool head = false) => Prelude + (head
        ? "namespace Gui.Widgets { class Surface : Microsoft.Maui.Controls.ContentPage { " + member + " } }"
        : "namespace Transporter.Features.Fleet.Presentation { class Surface : ReactiveMarbles.Mvvm.RxObject { " + member + " } }");

    /// <summary>Builds a feature static, where concrete-vehicle tests are also forbidden.</summary>
    /// <param name="member">The member under analysis.</param>
    /// <returns>The synthetic compilation.</returns>
    internal static string Feature(string member) => Prelude + "namespace Transporter.Features.Fleet { static class Display { static " + member + " } }";
}
