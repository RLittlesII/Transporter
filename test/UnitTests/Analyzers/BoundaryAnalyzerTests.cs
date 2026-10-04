using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Transponder.Analyzers;

namespace Transponder.UnitTests.Analyzers;

public class BoundaryAnalyzerTests
{
    // The repository's convention is that a system under test is built by a generated fixture
    // rather than a constructor call. It does not apply here: the AutoFixtures generator builds a
    // type from its constructor parameters, and an analyzer has none — nothing to arrange, nothing
    // for a constructor change to ripple through. Declaring a fixture for it would add a file that
    // holds nothing.

    [Fact]
    public async Task GivenAViolationInAMethodBody_WhenAnalyzed_ThenTheDiagnosticIsReportedAtThatNodeAndNamesTheSymbol()
    {
        // Given
        var sut = new BoundaryAnalyzer();

        // When
        var diagnostics = await AnalyzerHarness.Analyze(sut, ("Contracts.cs", WireType), ("ViewModel.cs", ViewModelNamingTheRow));

        // Then
        var reported = diagnostics.Should().ContainSingle().Subject;
        reported.Id.Should().Be("TRN0001");
        reported.Location.Should().NotBe(Location.None, "a diagnostic nobody can locate is one nobody can suppress where it lands");
        reported.Location.GetLineSpan().Path.Should().Be("ViewModel.cs");
        reported.Location.GetLineSpan().StartLinePosition.Line.Should().Be(LineOf(ViewModelNamingTheRow, "new OpenSkyStateRow()"));
        reported.GetMessage().Should().Contain("OpenSkyStateRow").And.Contain("aircraft-source B-004");
    }

    [Fact]
    public async Task GivenAViolationInGeneratedSource_WhenAnalyzed_ThenNothingIsReported()
    {
        // Given
        var sut = new BoundaryAnalyzer();

        // When
        var generated = await AnalyzerHarness.Analyze(sut, ("Contracts.cs", WireType), ("ViewModel.g.cs", ViewModelNamingTheRow));
        var handWritten = await AnalyzerHarness.Analyze(sut, ("Contracts.cs", WireType), ("ViewModel.cs", ViewModelNamingTheRow));

        // Then
        generated.Should().BeEmpty("the fix for generated source is upstream, and the only escape from a report on it is disabling the rule");
        handWritten.Should().ContainSingle("the same violation written by hand is still a violation");
    }

    [Fact]
    public async Task GivenADomainTypeNamingThePositionalRow_WhenAnalyzed_ThenTheRowIsReportedOutOfReach()
    {
        // Given
        var sut = new BoundaryAnalyzer();

        // When
        var diagnostics = await AnalyzerHarness.Analyze(sut, ("Contracts.cs", WireType), ("Vehicle.cs", DomainTypeNamingTheRow));

        // Then
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("TRN0001");
    }

    [Fact]
    public async Task GivenTheIntegrationsOwnCodeNamingTheRow_WhenAnalyzed_ThenNothingIsReported()
    {
        // Given
        var sut = new BoundaryAnalyzer();

        // When
        var diagnostics = await AnalyzerHarness.Analyze(sut, ("Contracts.cs", WireType), ("Http.cs", IntegrationCodeNamingTheRow));

        // Then
        diagnostics.Should().BeEmpty("the wire surface is the integration's to name; the claim holds it out of everything else");
    }

    private static int LineOf(string source, string fragment) =>
        source.Split('\n').ToList().FindIndex(line => line.Contains(fragment));

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
}
