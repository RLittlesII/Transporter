using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Transponder.Analyzers;

namespace Transponder.UnitTests.Analyzers;

public class BoundaryAnalyzerTests
{
    [Fact]
    public async Task GivenAViolationInAMethodBody_WhenAnalyzed_ThenTheDiagnosticIsReportedAtThatNodeAndNamesTheSymbol()
    {
        // Given, When
        var diagnostics = await BoundaryAnalyzerContext.Analyze(("Contracts.cs", WireType), ("ViewModel.cs", ViewModelNamingTheRow));

        // Then
        var reported = diagnostics.Should().ContainSingle().Subject;
        reported.Id.Should().Be("TRN0001");
        reported.Location.Should().NotBe(Location.None, "a diagnostic nobody can locate is one nobody can suppress where it lands");
        reported.Location.GetLineSpan().Path.Should().Be("ViewModel.cs");
        reported.TextAt().Should().Be("OpenSkyStateRow", "the span is the identifier that named the type, not the statement around it");
        reported.GetMessage().Should().Contain("OpenSkyStateRow").And.Contain("aircraft-source B-004");
    }

    [Fact]
    public async Task GivenAViolationInGeneratedSource_WhenAnalyzed_ThenNothingIsReported()
    {
        // Given, When
        var generated = await BoundaryAnalyzerContext.Analyze(("Contracts.cs", WireType), ("ViewModel.g.cs", ViewModelNamingTheRow));
        var handWritten = await BoundaryAnalyzerContext.Analyze(("Contracts.cs", WireType), ("ViewModel.cs", ViewModelNamingTheRow));

        // Then
        generated.Should().BeEmpty("the fix for generated source is upstream, and the only escape from a report on it is disabling the rule");
        handWritten.Should().ContainSingle("the same violation written by hand is still a violation");
    }

    [Fact]
    public async Task GivenADomainTypeNamingThePositionalRow_WhenAnalyzed_ThenTheRowIsReportedOutOfReach()
    {
        // Given, When
        var diagnostics = await BoundaryAnalyzerContext.Analyze(("Contracts.cs", WireType), ("Vehicle.cs", DomainTypeNamingTheRow));

        // Then
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("TRN0001");
    }

    [Fact]
    public async Task GivenTheIntegrationsOwnCodeNamingTheRow_WhenAnalyzed_ThenNothingIsReported()
    {
        // Given, When
        var diagnostics = await BoundaryAnalyzerContext.Analyze(("Contracts.cs", WireType), ("Http.cs", IntegrationCodeNamingTheRow));

        // Then
        diagnostics.Should().BeEmpty("the wire surface is the integration's to name; the claim holds it out of everything else");
    }

    [Fact]
    public async Task GivenATypeNamingAForbiddenTypeOnlyInsideAMethodBody_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var diagnostics = await BoundaryAnalyzerContext.Analyze(("Snapshot.cs", Snapshot), ("Vehicle.cs", DomainTypeNamingASnapshotInABodyOnly));

        // Then
        var reported = diagnostics.Should().ContainSingle().Subject;
        reported.Id.Should().Be("TRN0003");
        reported.TextAt().Should().Be("AircraftSnapshot", "a signature-only rule would have reported nothing here");
    }

    [Fact]
    public async Task GivenATypeOtherThanTheContractImplementationOrClientNamingTheEnvelopeOrRow_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await BoundaryAnalyzerContext.Analyze(("Contracts.cs", WireSurface), ("Registration.cs", RegistrationNamingTheRow));
        var allowed = await BoundaryAnalyzerContext.Analyze(("Contracts.cs", WireSurface), ("Http.cs", IntegrationCodeNamingTheRow));

        // Then
        reported.Should().ContainSingle().Which.Id.Should().Be("TRN0002");
        allowed.Should().BeEmpty("the transport implements the contract, so the envelope and the row are its to hold");
    }

    [Fact]
    public async Task GivenATypeDownstreamOfTheProjectionNamingASnapshot_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await BoundaryAnalyzerContext.Analyze(("Snapshot.cs", Snapshot), ("Vehicle.cs", DomainTypeNamingASnapshot));
        var allowed = await BoundaryAnalyzerContext.Analyze(("Snapshot.cs", Snapshot), ("Projection.cs", ProjectionNamingASnapshot));

        // Then
        reported.Should().ContainSingle().Which.Id.Should().Be("TRN0003");
        allowed.Should().BeEmpty("the projection is where the snapshot dies, so it is the last thing allowed to read one");
    }

    [Fact]
    public async Task GivenAConsumerOfTheFleetTrackerNamingAnythingUpstream_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var diagnostics = await BoundaryAnalyzerContext.Analyze(("Contracts.cs", WireSurface), ("Page.cs", HostNamingTheContract));

        // Then
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("TRN0004");
    }

    [Fact]
    public async Task GivenACacheNamingADomainType_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await BoundaryAnalyzerContext.Analyze(("Cache.cs", CacheStandIn), ("Vehicle.cs", Domain), ("Client.cs", CacheOfADomainType));
        var allowed = await BoundaryAnalyzerContext.Analyze(("Cache.cs", CacheStandIn), ("Snapshot.cs", Snapshot), ("Client.cs", CacheOfASnapshot));

        // Then
        reported.Should().ContainSingle().Which.Id.Should().Be("TRN0005");
        allowed.Should().BeEmpty("a cache of snapshots is what the claim asks for");
    }

    [Fact]
    public async Task GivenAViewModelNamingAStrategyClientCacheOrDecorator_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var diagnostics = await BoundaryAnalyzerContext.Analyze(("Source.cs", ConcreteTrackerSource), ("ViewModel.cs", ViewModelNamingAStrategy));

        // Then
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("TRN0006");
    }

    [Fact]
    public async Task GivenCodeAddingToOrRemovingFromABoundCollection_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var diagnostics = await BoundaryAnalyzerContext.Analyze(("ViewModel.cs", ViewModelMutatingABoundCollection));

        // Then
        diagnostics.Should().HaveCount(2).And.OnlyContain(static diagnostic => diagnostic.Id == "TRN0007");
        diagnostics.Select(static diagnostic => diagnostic.TextAt())
            .Should()
            .BeEquivalentTo(["Add", "Clear"], "each lands on the member being called, not on the statement around it");
    }


    private const string WireType = """
        namespace Transponder.Integrations.OpenSky.Contracts;

        internal sealed class OpenSkyStateRow
        {
            public int Index { get; set; }
        }
        """;

    private const string ViewModelNamingTheRow = """
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

    private const string DomainTypeNamingTheRow = """
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

    private const string IntegrationCodeNamingTheRow = """
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

    private const string WireSurface = """
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

    private const string RegistrationNamingTheRow = """
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

    private const string Snapshot = """
        namespace Transponder.Integrations.OpenSky;

        internal sealed record AircraftSnapshot
        {
            public string Icao24 { get; init; } = string.Empty;
        }
        """;

    private const string Domain = """
        namespace Transponder.Model;

        internal sealed class TransportVehicle
        {
            public string Key { get; init; } = string.Empty;
        }
        """;

    private const string DomainTypeNamingASnapshot = """
        using Transponder.Integrations.OpenSky;

        namespace Transponder.Model;

        internal sealed class Aircraft
        {
            public string From(AircraftSnapshot snapshot) => snapshot.Icao24;
        }
        """;

    private const string DomainTypeNamingASnapshotInABodyOnly = """
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

    private const string ProjectionNamingASnapshot = """
        using Transponder.Integrations.OpenSky;

        namespace Transponder.Tracking;

        internal sealed class AircraftTrackerSource
        {
            public string Project(AircraftSnapshot snapshot) => snapshot.Icao24;
        }
        """;

    private const string HostNamingTheContract = """
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Gui;

        public class MainPage
        {
            public object? Api { get; set; }

            internal void Bind(IOpenSkyApi api) => Api = api;
        }
        """;

    private const string CacheStandIn = """
        namespace DynamicData;

        internal sealed class SourceCache<TObject, TKey>
        {
            public int Count { get; set; }
        }
        """;

    private const string CacheOfADomainType = """
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

    private const string CacheOfASnapshot = """
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

    private const string ConcreteTrackerSource = """
        namespace Transponder.Tracking;

        internal sealed class AircraftTrackerSource
        {
            public int Count { get; set; }
        }
        """;

    private const string ViewModelNamingAStrategy = """
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

    private const string ViewModelMutatingABoundCollection = """
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
