using System.Collections.ObjectModel;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Rocket.Surgery.Extensions.Testing.SourceGenerators;
using Transponder.Analyzers;

namespace Transponder.UnitTests.Analyzers;

public class BoundaryAnalyzerTests
{
    [Fact]
    public async Task GivenAViolationInAMethodBody_WhenAnalyzed_ThenTheDiagnosticIsReportedAtThatNodeAndNamesTheSymbol()
    {
        // Given, When
        var results = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Contracts.cs", BoundaryTestData.WireType)
           .AddSource("ViewModel.cs", BoundaryTestData.ViewModelNamingTheRow)
           .GenerateAsync();

        // Then
        var reported = results.AnalyzerResults[typeof(BoundaryAnalyzer)].Diagnostics.Should().ContainSingle().Subject;
        reported.Id.Should().Be("TRN0001");
        reported.Location.Should().NotBe(Location.None, "a diagnostic nobody can locate is one nobody can suppress where it lands");
        reported.Location.GetLineSpan().Path.Should().Be("ViewModel.cs");
        TextAt(reported).Should().Be("OpenSkyStateRow", "the span is the identifier that named the type, not the statement around it");
        reported.GetMessage().Should().Contain("OpenSkyStateRow").And.Contain("aircraft-source B-004");
    }

    [Fact]
    public async Task GivenAViolationInGeneratedSource_WhenAnalyzed_ThenNothingIsReported()
    {
        // Given, When. The file name is what makes a source generated code.
        var generated = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Contracts.cs", BoundaryTestData.WireType)
           .AddSource("ViewModel.g.cs", BoundaryTestData.ViewModelNamingTheRow)
           .GenerateAsync();

        var handWritten = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Contracts.cs", BoundaryTestData.WireType)
           .AddSource("ViewModel.cs", BoundaryTestData.ViewModelNamingTheRow)
           .GenerateAsync();

        // Then
        generated.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should()
           .BeEmpty("the fix for generated source is upstream, and the only escape from a report on it is disabling the rule");
        handWritten.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should()
           .ContainSingle("the same violation written by hand is still a violation");
    }

    [Fact]
    public async Task GivenADomainTypeNamingThePositionalRow_WhenAnalyzed_ThenTheRowIsReportedOutOfReach()
    {
        // Given, When
        var results = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSources(BoundaryTestData.WireType, BoundaryTestData.DomainTypeNamingTheRow)
           .GenerateAsync();

        // Then
        results.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should()
           .ContainSingle()
           .Which.Id.Should()
           .Be("TRN0001");
    }

    [Fact]
    public async Task GivenTheIntegrationsOwnCodeNamingTheRow_WhenAnalyzed_ThenNothingIsReported()
    {
        // Given, When
        var results = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSources(BoundaryTestData.WireType, BoundaryTestData.IntegrationCodeNamingTheRow)
           .GenerateAsync();

        // Then
        NothingFailedToCompile(results);
        results.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should()
           .BeEmpty("the wire surface is the integration's to name; the claim holds it out of everything else");
    }

    [Fact]
    public async Task GivenATypeNamingAForbiddenTypeOnlyInsideAMethodBody_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var diagnostics = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
           .AddSource("Vehicle.cs", BoundaryTestData.DomainTypeNamingASnapshotInABodyOnly)
           .GenerateAsync();

        // Then
        var reported = diagnostics.AnalyzerResults[typeof(BoundaryAnalyzer)].Diagnostics.Should().ContainSingle().Subject;
        reported.Id.Should().Be("TRN0003");
        TextAt(reported).Should().Be("AircraftSnapshot", "a signature-only rule would have reported nothing here");
    }

    [Fact]
    public async Task GivenATypeOtherThanTheContractImplementationOrClientNamingTheEnvelopeOrRow_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Contracts.cs", BoundaryTestData.WireSurface)
           .AddSource("Registration.cs", BoundaryTestData.RegistrationNamingTheRow)
           .GenerateAsync();
        var allowed = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Contracts.cs", BoundaryTestData.WireSurface)
           .AddSource("Http.cs", BoundaryTestData.IntegrationCodeNamingTheRow)
           .GenerateAsync();

        // Then
        reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should()
           .ContainSingle()
           .Which.Id.Should()
           .Be("TRN0002");
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should().BeEmpty("the transport implements the contract, so the envelope and the row are its to hold");
    }

    [Fact]
    public async Task GivenATypeDownstreamOfTheProjectionNamingASnapshot_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
           .AddSource("Vehicle.cs", BoundaryTestData.DomainTypeNamingASnapshot)
           .GenerateAsync();
        var allowed = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
           .AddSource("Projection.cs", BoundaryTestData.ProjectionNamingASnapshot)
           .GenerateAsync();

        // Then
        reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should()
           .ContainSingle()
           .Which.Id.Should()
           .Be("TRN0003");
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should().BeEmpty("the projection is where the snapshot dies, so it is the last thing allowed to read one");
    }

    [Fact]
    public async Task GivenAConsumerOfTheFleetTrackerNamingAnythingUpstream_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var diagnostics = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Contracts.cs", BoundaryTestData.WireSurface)
           .AddSource("Page.cs", BoundaryTestData.HostNamingTheContract)
           .GenerateAsync();

        // Then
        diagnostics.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should()
           .ContainSingle()
           .Which.Id.Should()
           .Be("TRN0004");
    }

    [Fact]
    public async Task GivenACacheNamingADomainType_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
           .AddSource("Vehicle.cs", BoundaryTestData.Domain)
           .AddSource("Client.cs", BoundaryTestData.CacheOfADomainType)
           .GenerateAsync();
        var allowed = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
           .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
           .AddSource("Client.cs", BoundaryTestData.CacheOfASnapshot)
           .GenerateAsync();

        // Then
        reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should()
           .ContainSingle()
           .Which.Id.Should()
           .Be("TRN0005");
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)].Diagnostics.Should().BeEmpty("a cache of snapshots is what the claim asks for");
    }

    [Fact]
    public async Task GivenAViewModelNamingAStrategyClientCacheOrDecorator_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var diagnostics = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddSource("Source.cs", BoundaryTestData.ConcreteTrackerSource)
           .AddSource("ViewModel.cs", BoundaryTestData.ViewModelNamingAStrategy)
           .GenerateAsync();

        // Then
        diagnostics.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should()
           .ContainSingle()
           .Which.Id.Should()
           .Be("TRN0006");
    }

    [Fact]
    public async Task GivenCodeAddingToOrRemovingFromABoundCollection_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var diagnostics = await GeneratorTestContextBuilder
           .Create()
           .WithAnalyzer<BoundaryAnalyzer>()
           .WithDiagnosticSeverity(DiagnosticSeverity.Error)
           .AddReferences(typeof(ObservableCollection<>))
           .AddSource("ViewModel.cs", BoundaryTestData.ViewModelMutatingABoundCollection)
           .GenerateAsync();

        // Then
        diagnostics.AnalyzerResults[typeof(BoundaryAnalyzer)]
           .Diagnostics
           .Should().HaveCount(2).And.OnlyContain(static diagnostic => diagnostic.Id == "TRN0007");
        diagnostics.AnalyzerResults[typeof(BoundaryAnalyzer)].Diagnostics.Select(static diagnostic => TextAt(diagnostic))
            .Should()
            .BeEquivalentTo(["Add", "Clear"], "each lands on the member being called, not on the statement around it");
    }

    /// <summary>The text the diagnostic's span covers — what a caret lands on, and edit-proof where a line number is not.</summary>
    /// <param name="diagnostic">The diagnostic to read.</param>
    /// <returns>The source text at the diagnostic's location.</returns>
    internal static string TextAt(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree is { } tree ? tree.GetText().ToString(diagnostic.Location.SourceSpan) : string.Empty;

    /// <summary>Asserts the source compiled.</summary>
    /// <remarks>
    /// <c>AssertCompilationWasSuccessful</c> cannot do this: every TRN rule defaults to error, so it
    /// fails on the diagnostic the test came to see. A test expecting none needs the guard most.
    /// </remarks>
    /// <param name="results">The results to check.</param>
    internal static void NothingFailedToCompile(GeneratorTestResults results) =>
        results.InputDiagnostics
            .Should()
            .NotContain(
                static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id.StartsWith("CS", StringComparison.Ordinal),
                "a diagnostic reported over source that does not compile proves nothing");
}
