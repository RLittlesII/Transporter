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

    [Fact]
    public async Task GivenAnEnvelopeMemberThatNamesAPerAircraftType_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.EnvelopeNamingPerAircraftTypes)
            .GenerateAsync();

        var allowed = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.WireSurface)
            .GenerateAsync();

        // Then. Both clauses: the positional member, and any other member of the envelope.
        NothingFailedToCompile(allowed);
        reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .HaveCount(2)
            .And.OnlyContain(static diagnostic => diagnostic.Id == "TRN0008");
        reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Select(static diagnostic => TextAt(diagnostic))
            .Should()
            .BeEquivalentTo(["States", "First"], "each lands on the member that left the positional shape");
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .BeEmpty("an envelope holding the provider's own rows is what the claim asks for");
    }

    [Fact]
    public async Task GivenAContractMethodThatIsNotOnePerEndpointOrDoesNotTakeCancellationLast_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.ContractMethodsOfTheWrongShape)
            .GenerateAsync();

        var allowed = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.WireSurface)
            .GenerateAsync();

        // Then. A clause reported is a clause fixed; three of four green is the failure this guards.
        NothingFailedToCompile(allowed);
        var messages = reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0009")
            .Select(static diagnostic => diagnostic.GetMessage())
            .ToList();
        messages.Should().HaveCount(5, "two overloads, one return type, one missing token and one misplaced token");
        messages.Should().Contain(static message => message.Contains("more than one method"));
        messages.Should().Contain(static message => message.Contains("does not return Task<T>"));
        messages.Should().Contain(static message => message.Contains("takes no CancellationToken"));
        messages.Should().Contain(static message => message.Contains("does not take its CancellationToken last"));
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .NotContain(static diagnostic => diagnostic.Id == "TRN0009");
    }

    [Fact]
    public async Task GivenAContractNamingAnObservableCacheBoxIntervalOrCredential_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
            .AddSource("Options.cs", BoundaryTestData.OptionsAndCredentials)
            .AddSource("Contracts.cs", BoundaryTestData.ContractNamingWhatBelongsAboveIt)
            .GenerateAsync();

        var allowed = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.WireSurface)
            .GenerateAsync();

        // Then. One per forbidden kind the claim lists.
        NothingFailedToCompile(reported);
        reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0010")
            .Select(static diagnostic => TextAt(diagnostic))
            .Should()
            .BeEquivalentTo(
                ["Stream", "Count", "Inside", "Poll", "Authorize"],
                "the report lands on the declaration that named it, not on a call that uses it");
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .NotContain(static diagnostic => diagnostic.Id == "TRN0010");
    }

    [Fact]
    public async Task GivenASecondImplementationForOneTransportOrAPublicEndpointMethod_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.PublicContract)
            .AddSource("Http.cs", BoundaryTestData.ImplementationsOfTheWrongShape)
            .GenerateAsync();

        var allowed = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.PublicContract)
            .AddSource("Http.cs", BoundaryTestData.TheOneImplementationPerTransport)
            .GenerateAsync();

        // Then. B-007 is four conditions in one sentence; each gets its own report.
        NothingFailedToCompile(reported);
        NothingFailedToCompile(allowed);
        var messages = reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0011")
            .Select(static diagnostic => diagnostic.GetMessage())
            .ToList();
        messages.Should().HaveCount(4);
        messages.Should().Contain(static message => message.Contains("is not internal"));
        messages.Should().Contain(static message => message.Contains("is not sealed"));
        messages.Should().Contain(static message => message.Contains("as a public method rather than an explicit implementation"));
        messages.Should().Contain(static message => message.Contains("is a second implementation"));
        reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0011")
            .Select(static diagnostic => TextAt(diagnostic))
            .Should()
            .BeEquivalentTo(
                ["OpenSkyHttpApi", "OpenSkyHttpApi", "GetStates", "OpenSkyBackupApi"],
                "the clause about a member lands on that member, and the clauses about the type on the type");
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .NotContain(
                static diagnostic => diagnostic.Id == "TRN0011",
                "one internal sealed class implementing explicitly is what the claim asks for");
    }

    [Fact]
    public async Task GivenASnapshotMemberThatIsDerivedRatherThanReported_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Snapshot.cs", BoundaryTestData.SnapshotCarryingDerivedMembers)
            .GenerateAsync();

        var allowed = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
            .GenerateAsync();

        // Then. The three the claim names, and the "any other value derived" clause behind them.
        NothingFailedToCompile(reported);
        NothingFailedToCompile(allowed);
        reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0012")
            .Select(static diagnostic => TextAt(diagnostic))
            .Should()
            .BeEquivalentTo(
                ["IsStale", "GroupingKey", "DisplayLabel", "Age"],
                "a computed getter is derived whatever it is called, and the named kinds are derived even when stored");
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .BeEmpty("a member carrying what the provider reported is the whole point of the snapshot");
    }

    [Fact]
    public async Task GivenAPerTypeSeamWithASourceDescribingMember_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Seam.cs", BoundaryTestData.SeamDescribingItsSource)
            .GenerateAsync();

        var allowed = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Seam.cs", BoundaryTestData.Seam)
            .GenerateAsync();

        // Then
        NothingFailedToCompile(reported);
        NothingFailedToCompile(allowed);
        reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0013")
            .Select(static diagnostic => TextAt(diagnostic))
            .Should()
            .BeEquivalentTo(["SourceName", "ProviderUrl"]);
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .BeEmpty("the changeset is what the seam is for; where it came from is what it must not say");
    }

    [Fact]
    public async Task GivenAContractCarryingAVersionSuffixOrAMarkerAboveIt_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.ContractCarryingAVersion)
            .GenerateAsync();

        var allowed = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.WireSurface)
            .GenerateAsync();

        // Then. Both clauses of B-048, which are two ways of claiming a version that does not exist.
        NothingFailedToCompile(reported);
        var messages = reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0014")
            .Select(static diagnostic => diagnostic.GetMessage())
            .ToList();
        messages.Should().HaveCount(2);
        messages.Should().Contain(static message => message.Contains("carries the version suffix 'V2'"));
        messages.Should().Contain(static message => message.Contains("has the marker interface 'IOpenSkyApiMarker' above it"));
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .NotContain(static diagnostic => diagnostic.Id == "TRN0014");
    }

    [Fact]
    public async Task GivenAForbiddenShapeOnADeclaration_WhenAnalyzed_ThenItIsReportedOnThatDeclaration()
    {
        // Given, When. The declaration is in one file and the call that reads it in another, so
        // the span says which of the two the rule was evaluated against.
        var results = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Snapshot.cs", BoundaryTestData.SnapshotCarryingDerivedMembers)
            .AddSource("Client.cs", BoundaryTestData.ClientReadingADerivedMember)
            .GenerateAsync();

        // Then
        NothingFailedToCompile(results);
        var reported = results.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => TextAt(diagnostic) == "DisplayLabel")
            .Should()
            .ContainSingle("the declaration is reported, and the call site that reads it is not")
            .Subject;
        reported.Id.Should().Be("TRN0012");
        reported.Location.GetLineSpan().Path.Should().Be("Snapshot.cs");
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
