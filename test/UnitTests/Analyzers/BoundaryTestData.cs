namespace Transponder.UnitTests.Analyzers;

/// <summary>Sources the boundary rules are tested against, one layer per constant.</summary>
/// <remarks>Shared the way Airframe's <c>DesignTestData</c> is: each namespace is one <c>Layers</c> classification.</remarks>
internal static class BoundaryTestData
{
    // lang=csharp
    internal const string WireType = """
        namespace Transponder.Integrations.OpenSky.Contracts;

        internal sealed class OpenSkyStateRow
        {
            public int Index { get; set; }
        }
        """;

    // lang=csharp
    internal const string ViewModelNamingTheRow = """
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Features.Demo.ViewModels;

        internal sealed class DemoViewModel
        {
            public int Count()
            {
                var row = new OpenSkyStateRow();

                return row.Index;
            }
        }
        """;

    // lang=csharp
    internal const string DomainTypeNamingTheRow = """
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Model;

        internal sealed class TransportVehicle
        {
            public int Key()
            {
                OpenSkyStateRow row = new();

                return row.Index;
            }
        }
        """;

    // lang=csharp
    internal const string IntegrationCodeNamingTheRow = """
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Integrations.OpenSky.Http;

        internal sealed class OpenSkyHttpApi
        {
            public int Read()
            {
                var row = new OpenSkyStateRow();

                return row.Index;
            }
        }
        """;

    // lang=csharp
    internal const string WireSurface = """
        using System.Threading;
        using System.Threading.Tasks;

        namespace Transponder.Integrations.OpenSky.Contracts;

        internal sealed class OpenSkyStateRow
        {
            public int Index { get; set; }
        }

        internal sealed class OpenSkyStatesResponse
        {
            public OpenSkyStateRow[] States { get; set; } = [];
        }

        internal interface IOpenSkyApi
        {
            Task<OpenSkyStatesResponse> GetStates(CancellationToken cancellationToken);
        }
        """;

    // lang=csharp
    internal const string RegistrationNamingTheRow = """
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Integrations.OpenSky.Container;

        internal static class OpenSkyRegistration
        {
            public static int Rows()
            {
                OpenSkyStateRow row = new();

                return row.Index;
            }
        }
        """;

    // lang=csharp
    internal const string ReplayImplementingTheContract = """
        using System.Threading;
        using System.Threading.Tasks;
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Integrations.OpenSky.Replay;

        internal sealed class ReplayOpenSkyApi : IOpenSkyApi
        {
            Task<OpenSkyStatesResponse> IOpenSkyApi.GetStates(CancellationToken cancellationToken) =>
                Task.FromResult(new OpenSkyStatesResponse());
        }
        """;

    // lang=csharp
    internal const string ReplayCodeImplementingNothing = """
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Integrations.OpenSky.Replay;

        internal sealed class ReplayPayloads
        {
            public int Rows()
            {
                OpenSkyStatesResponse response = new();

                return response.States.Length;
            }
        }
        """;

    // lang=csharp
    internal const string Snapshot = """
        namespace Transponder.Integrations.OpenSky;

        internal sealed record AircraftSnapshot
        {
            public string Icao24 { get; init; } = string.Empty;
        }
        """;

    // lang=csharp
    internal const string Domain = """
        namespace Transponder.Model;

        internal sealed class TransportVehicle
        {
            public string Key { get; init; } = string.Empty;
        }
        """;

    // lang=csharp
    internal const string DomainTypeNamingASnapshot = """
        using Transponder.Integrations.OpenSky;

        namespace Transponder.Model;

        internal sealed class Aircraft
        {
            public string From(AircraftSnapshot snapshot) => snapshot.Icao24;
        }
        """;

    // lang=csharp
    internal const string DomainTypeNamingASnapshotInABodyOnly = """
        namespace Transponder.Model;

        internal sealed class Aircraft
        {
            public string Key()
            {
                Transponder.Integrations.OpenSky.AircraftSnapshot snapshot = new();

                return snapshot.Icao24;
            }
        }
        """;

    // lang=csharp
    internal const string ProjectionNamingASnapshot = """
        using Transponder.Integrations.OpenSky;

        namespace Transponder.Tracking;

        internal sealed class AircraftTrackerSource
        {
            public string Project(AircraftSnapshot snapshot) => snapshot.Icao24;
        }
        """;

    // lang=csharp
    internal const string HostNamingTheContract = """
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Gui;

        public class MainPage
        {
            public object? Api { get; set; }

            internal void Bind(IOpenSkyApi api) => Api = api;
        }
        """;

    // lang=csharp
    internal const string CacheStandIn = """
        namespace DynamicData;

        internal sealed class SourceCache<TObject, TKey>
        {
            public int Count { get; set; }
        }
        """;

    // lang=csharp
    internal const string CacheOfADomainType = """
        using DynamicData;
        using Transponder.Model;

        namespace Transponder.Integrations.OpenSky;

        internal sealed class AircraftSnapshotClient
        {
            public int Count()
            {
                SourceCache<TransportVehicle, string> cache = new();

                return cache.Count;
            }
        }
        """;

    // lang=csharp
    internal const string CacheOfASnapshot = """
        using DynamicData;

        namespace Transponder.Integrations.OpenSky;

        internal sealed class AircraftSnapshotClient
        {
            public int Count()
            {
                SourceCache<AircraftSnapshot, string> cache = new();

                return cache.Count;
            }
        }
        """;

    // lang=csharp
    internal const string ConcreteTrackerSource = """
        namespace Transponder.Tracking;

        internal sealed class AircraftTrackerSource
        {
            public int Count { get; set; }
        }
        """;

    // lang=csharp
    internal const string TrackerSeam = """
        namespace Transponder.Tracking;

        public interface IFleetTracker
        {
            TrackedVehicle Published { get; }

            FleetSourceDescription Description { get; }
        }

        public sealed class TrackedVehicle
        {
            public string Key { get; set; } = string.Empty;
        }

        public sealed class FleetSourceDescription
        {
            public FleetColumn[] Columns { get; set; } = [];
        }

        public sealed class FleetColumn
        {
            public string Name { get; set; } = string.Empty;
        }
        """;

    // lang=csharp
    internal const string ViewModelNamingWhatTheSeamPublishes = """
        using Transponder.Tracking;

        namespace Transponder.Features.Fleet.ViewModels;

        public class FleetViewModel
        {
            public string Read(TrackedVehicle tracked, FleetColumn column) => tracked.Key + column.Name;
        }
        """;

    // lang=csharp
    internal const string ViewModelNamingAStrategy = """
        using Transponder.Tracking;

        namespace Transponder.Features.Fleet.ViewModels;

        public class FleetViewModel
        {
            public int Count()
            {
                AircraftTrackerSource source = new();

                return source.Count;
            }
        }
        """;

    // lang=csharp
    internal const string ViewModelMutatingABoundCollection = """
        using System.Collections.ObjectModel;

        namespace Transponder.Features.Fleet.ViewModels;

        public class FleetViewModel
        {
            public ObservableCollection<string> Rows { get; } = [];

            public void Refresh(string row)
            {
                Rows.Add(row);
                Rows.Clear();
            }
        }
        """;

    // lang=csharp
    internal const string EnvelopeNamingPerAircraftTypes = """
        namespace Transponder.Integrations.OpenSky.Contracts;

        internal sealed class OpenSkyStateRow
        {
            public int Index { get; set; }
        }

        internal sealed class OpenSkyAircraft
        {
            public string Icao24 { get; set; } = string.Empty;
        }

        internal sealed class OpenSkyStatesResponse
        {
            public OpenSkyAircraft[] States { get; set; } = [];

            public OpenSkyAircraft First { get; set; } = new();
        }
        """;

    // lang=csharp
    internal const string ContractMethodsOfTheWrongShape = """
        using System.Threading;
        using System.Threading.Tasks;

        namespace Transponder.Integrations.OpenSky.Contracts;

        internal sealed class OpenSkyStatesResponse
        {
            public int Time { get; set; }
        }

        internal interface IOpenSkyApi
        {
            Task<OpenSkyStatesResponse> GetStates(CancellationToken cancellationToken);

            Task<OpenSkyStatesResponse> GetStates(double lamin, CancellationToken cancellationToken);

            void Ping(CancellationToken cancellationToken);

            Task<OpenSkyStatesResponse> GetFlights(double lamin);

            Task<OpenSkyStatesResponse> GetTracks(CancellationToken cancellationToken, double lamin);
        }
        """;

    // lang=csharp
    internal const string ContractNamingWhatBelongsAboveIt = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using DynamicData;
        using Transponder.Integrations.OpenSky;

        namespace Transponder.Integrations.OpenSky.Contracts;

        internal interface IOpenSkyApi
        {
            Task<IObservable<int>> Stream(CancellationToken cancellationToken);

            Task<int> Count(SourceCache<string, string> cache, CancellationToken cancellationToken);

            Task<int> Inside(BoundingBox box, CancellationToken cancellationToken);

            Task<int> Poll(TimeSpan interval, CancellationToken cancellationToken);

            Task<int> Authorize(OpenSkyCredentials credentials, CancellationToken cancellationToken);
        }
        """;

    // lang=csharp
    internal const string OptionsAndCredentials = """
        namespace Transponder.Integrations.OpenSky;

        internal sealed class BoundingBox
        {
            public double Lamin { get; set; }
        }

        internal sealed class OpenSkyCredentials
        {
            public string ClientId { get; set; } = string.Empty;
        }
        """;

    // lang=csharp
    internal const string PublicContract = """
        using System.Threading;
        using System.Threading.Tasks;

        namespace Transponder.Integrations.OpenSky.Contracts;

        public sealed class OpenSkyStatesResponse
        {
            public int Time { get; set; }
        }

        public interface IOpenSkyApi
        {
            Task<OpenSkyStatesResponse> GetStates(CancellationToken cancellationToken);
        }
        """;

    // lang=csharp
    internal const string ImplementationsOfTheWrongShape = """
        using System.Threading;
        using System.Threading.Tasks;
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Integrations.OpenSky.Http;

        public class OpenSkyHttpApi : IOpenSkyApi
        {
            public Task<OpenSkyStatesResponse> GetStates(CancellationToken cancellationToken) =>
                Task.FromResult(new OpenSkyStatesResponse());
        }

        internal sealed class OpenSkyBackupApi : IOpenSkyApi
        {
            Task<OpenSkyStatesResponse> IOpenSkyApi.GetStates(CancellationToken cancellationToken) =>
                Task.FromResult(new OpenSkyStatesResponse());
        }
        """;

    // lang=csharp
    internal const string TheOneImplementationPerTransport = """
        using System.Threading;
        using System.Threading.Tasks;
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Integrations.OpenSky.Http;

        internal sealed class OpenSkyHttpApi : IOpenSkyApi
        {
            Task<OpenSkyStatesResponse> IOpenSkyApi.GetStates(CancellationToken cancellationToken) =>
                Task.FromResult(new OpenSkyStatesResponse());
        }
        """;

    // lang=csharp
    internal const string SnapshotCarryingDerivedMembers = """
        namespace Transponder.Integrations.OpenSky;

        internal sealed record AircraftSnapshot
        {
            public string Icao24 { get; init; } = string.Empty;

            public bool IsStale { get; init; }

            public string GroupingKey { get; init; } = string.Empty;

            public string DisplayLabel => Icao24;

            public int Age => Icao24.Length;
        }
        """;

    // lang=csharp
    internal const string ClientReadingADerivedMember = """
        namespace Transponder.Integrations.OpenSky;

        internal sealed class AircraftSnapshotClient
        {
            public string Describe(AircraftSnapshot snapshot) => snapshot.DisplayLabel;
        }
        """;

    // lang=csharp
    internal const string SeamDescribingItsSource = """
        namespace Transponder.Tracking;

        internal interface IAircraftTrackerSource
        {
            string SourceName { get; }

            string ProviderUrl { get; }

            void Connect();
        }
        """;

    // lang=csharp
    internal const string Seam = """
        namespace Transponder.Tracking;

        internal interface IAircraftTrackerSource
        {
            void Connect();
        }
        """;

    // lang=csharp
    internal const string ContractMethodTakingCancellationFirst = """
        using System.Threading;
        using System.Threading.Tasks;

        namespace Transponder.Integrations.OpenSky.Contracts;

        internal interface IOpenSkyApi
        {
            Task<int> GetStates(CancellationToken cancellationToken, double lamin);
        }
        """;

    // lang=csharp
    internal const string PublicImplementation = """
        using System.Threading;
        using System.Threading.Tasks;
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Integrations.OpenSky.Http;

        public class OpenSkyHttpApi : IOpenSkyApi
        {
            public Task<OpenSkyStatesResponse> GetStates(CancellationToken cancellationToken) =>
                Task.FromResult(new OpenSkyStatesResponse());
        }
        """;

    // lang=csharp
    internal const string ContractCarryingAVersion = """
        using System.Threading;
        using System.Threading.Tasks;

        namespace Transponder.Integrations.OpenSky.Contracts;

        internal interface IOpenSkyApiMarker;

        internal interface IOpenSkyApiV2 : IOpenSkyApiMarker
        {
            Task<int> GetStates(CancellationToken cancellationToken);
        }
        """;

    // lang=csharp
    internal const string ContainerStandIn = """
        using System;

        namespace Microsoft.Extensions.DependencyInjection;

        public interface IServiceCollection;

        public static class ServiceCollectionExtensions
        {
            public static IServiceCollection AddSingleton<TService>(this IServiceCollection services) => services;

            public static IServiceCollection AddSingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> factory) => services;

            public static IServiceCollection AddSingleton<TService, TImplementation>(this IServiceCollection services) => services;

            public static IServiceCollection AddScoped<TService>(this IServiceCollection services) => services;

            public static IServiceCollection AddScoped<TService>(this IServiceCollection services, Func<IServiceProvider, TService> factory) => services;

            public static IServiceCollection AddTransient<TService>(this IServiceCollection services) => services;

            public static IServiceCollection Add(this IServiceCollection services, Type service) => services;

            public static T GetRequiredService<T>(this IServiceProvider provider) => default!;
        }
        """;

    // lang=csharp
    internal const string TheOneAliasPerChain = """
        using Microsoft.Extensions.DependencyInjection;
        using Transponder.Integrations.OpenSky.Contracts;
        using Transponder.Integrations.OpenSky.Http;

        namespace Transponder.Integrations.OpenSky.Container;

        internal static class OpenSkyRegistration
        {
            public static IServiceCollection AddOpenSky(this IServiceCollection services) =>
                services.AddSingleton<IOpenSkyApi, OpenSkyHttpApi>();
        }
        """;

    // lang=csharp
    internal const string ImplementationRegisteredResolvedAndAliasedTwice = """
        using System;
        using Microsoft.Extensions.DependencyInjection;
        using Transponder.Integrations.OpenSky.Contracts;
        using Transponder.Integrations.OpenSky.Http;

        namespace Transponder.Integrations.OpenSky.Container;

        internal static class OpenSkyRegistration
        {
            public static IServiceCollection AddTheImplementationItself(this IServiceCollection services) =>
                services.AddSingleton<OpenSkyHttpApi>();

            public static IServiceCollection AddBySpellingTheRuleDoesNotKnow(this IServiceCollection services) =>
                services.Add(typeof(OpenSkyHttpApi));

            public static IServiceCollection AddTwoAliases(this IServiceCollection services)
            {
                services.AddSingleton<IOpenSkyApi, OpenSkyHttpApi>();

                return services.AddSingleton<IOpenSkyApi, OpenSkyBackupApi>();
            }

            public static OpenSkyHttpApi ResolveTheImplementation(IServiceProvider provider) =>
                provider.GetRequiredService<OpenSkyHttpApi>();
        }
        """;

    // lang=csharp
    internal const string TwoTransports = """
        using System.Threading;
        using System.Threading.Tasks;
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Integrations.OpenSky.Http;

        internal sealed class OpenSkyHttpApi : IOpenSkyApi
        {
            Task<OpenSkyStatesResponse> IOpenSkyApi.GetStates(CancellationToken cancellationToken) =>
                Task.FromResult(new OpenSkyStatesResponse());
        }

        internal sealed class OpenSkyBackupApi : IOpenSkyApi
        {
            Task<OpenSkyStatesResponse> IOpenSkyApi.GetStates(CancellationToken cancellationToken) =>
                Task.FromResult(new OpenSkyStatesResponse());
        }
        """;

    // lang=csharp
    internal const string CacheWrappedInATypeOfItsOwn = """
        using DynamicData;
        using Microsoft.Extensions.DependencyInjection;

        namespace Transponder.Integrations.OpenSky.Container;

        internal sealed class AircraftCache
        {
            internal SourceCache<AircraftSnapshot, string> Snapshots { get; } = new();
        }

        internal static class CacheRegistration
        {
            public static IServiceCollection AddCache(this IServiceCollection services) =>
                services.AddSingleton<AircraftCache>();
        }
        """;

    // lang=csharp
    internal const string CacheOfADomainTypeRegistered = """
        using DynamicData;
        using Microsoft.Extensions.DependencyInjection;
        using Transponder.Model;

        namespace Transponder.Integrations.OpenSky.Container;

        internal static class CacheRegistration
        {
            public static IServiceCollection AddCache(this IServiceCollection services) =>
                services.AddSingleton<SourceCache<TransportVehicle, string>>();
        }
        """;

    // lang=csharp
    internal const string CacheRegisteredScopedAndTwice = """
        using DynamicData;
        using Microsoft.Extensions.DependencyInjection;

        namespace Transponder.Integrations.OpenSky.Container;

        internal static class CacheRegistration
        {
            public static IServiceCollection AddScopedCache(this IServiceCollection services) =>
                services.AddScoped<SourceCache<AircraftSnapshot, string>>();

            public static IServiceCollection AddItTwice(this IServiceCollection services)
            {
                services.AddSingleton<SourceCache<AircraftSnapshot, string>>();

                return services.AddSingleton<SourceCache<AircraftSnapshot, string>>();
            }
        }
        """;

    // lang=csharp
    internal const string CacheRegisteredScoped = """
        using DynamicData;
        using Microsoft.Extensions.DependencyInjection;

        namespace Transponder.Integrations.OpenSky.Container;

        internal static class CacheRegistration
        {
            public static IServiceCollection AddCache(this IServiceCollection services) =>
                services.AddScoped<SourceCache<AircraftSnapshot, string>>();
        }
        """;

    // lang=csharp
    internal const string CacheRegisteredTwice = """
        using DynamicData;
        using Microsoft.Extensions.DependencyInjection;

        namespace Transponder.Integrations.OpenSky.Container;

        internal static class CacheRegistration
        {
            public static IServiceCollection AddItTwice(this IServiceCollection services)
            {
                services.AddSingleton<SourceCache<AircraftSnapshot, string>>();

                return services.AddSingleton<SourceCache<AircraftSnapshot, string>>();
            }
        }
        """;

    // lang=csharp
    internal const string ClientHoldingItsCache = """
        using DynamicData;
        using Microsoft.Extensions.DependencyInjection;

        namespace Transponder.Integrations.OpenSky;

        internal sealed class AircraftSnapshotClient
        {
            public AircraftSnapshotClient(SourceCache<AircraftSnapshot, string> cache) => _cache = cache;

            private readonly SourceCache<AircraftSnapshot, string> _cache;
        }
        """;

    // lang=csharp
    internal const string ClientRegistered = """
        using Microsoft.Extensions.DependencyInjection;

        namespace Transponder.Integrations.OpenSky.Container;

        internal static class ClientRegistration
        {
            public static IServiceCollection AddClient(this IServiceCollection services) =>
                services.AddSingleton<AircraftSnapshotClient>();
        }
        """;

    // lang=csharp
    internal const string TheOneCachePerClient = """
        using DynamicData;
        using Microsoft.Extensions.DependencyInjection;

        namespace Transponder.Integrations.OpenSky.Container;

        internal static class CacheRegistration
        {
            public static IServiceCollection AddCache(this IServiceCollection services) =>
                services.AddSingleton<SourceCache<AircraftSnapshot, string>>();
        }
        """;
}
