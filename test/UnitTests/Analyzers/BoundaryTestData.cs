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
}
