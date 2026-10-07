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

    /// <summary>
    /// `0053`. The claim names "the class implementing the API contract", and a provider can have
    /// more than one — a transport over HTTP and a replay over a recording substitute at the same
    /// contract. The rule read the folder instead, so the second implementation was reported for
    /// naming the envelope its own signature returns. The pair is what makes the fix the contract
    /// rather than the namespace: a class beside it that implements nothing is still reported.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenASecondImplementationOfTheContractOutsideTheTransport_WhenAnalyzed_ThenTheEnvelopeIsItsToHoldAndANonImplementationBesideItIsNot()
    {
        // Given, When
        var implementation = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.WireSurface)
            .AddSource("Replay.cs", BoundaryTestData.ReplayImplementingTheContract)
            .GenerateAsync();
        var beside = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.WireSurface)
            .AddSource("Payloads.cs", BoundaryTestData.ReplayCodeImplementingNothing)
            .GenerateAsync();

        // Then
        implementation.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .BeEmpty("a second implementation of the contract is the class aircraft-source B-045 names");
        beside.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .ContainSingle("the folder is not what makes the envelope reachable")
            .Which.Id.Should()
            .Be("TRN0002");
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

    /// <summary>
    /// B-041 names a strategy, a client, a cache and a decorator, and the published element is none
    /// of them: the pipeline publishes changesets a consumer binds, so what the seam carries crosses
    /// it by design (ADR-0009). Reporting it would make every view model in <c>fleet-dashboard</c>
    /// uncompilable, which is what it did until the rule learned to read the seam.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAViewModelNamingWhatTheSeamPublishes_WhenAnalyzed_ThenItIsNotReported()
    {
        // Given, When
        var diagnostics = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Seam.cs", BoundaryTestData.TrackerSeam)
            .AddSource("ViewModel.cs", BoundaryTestData.ViewModelNamingWhatTheSeamPublishes)
            .GenerateAsync();

        // Then
        diagnostics.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .BeEmpty("the element and the description are what the tracker publishes for a consumer to bind");
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

    [Fact]
    public async Task GivenAnImplementationTypeRegisteredOrResolvedOrASecondAliasForTheContract_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(IServiceProvider))
            .AddSource("Container.cs", BoundaryTestData.ContainerStandIn)
            .AddSource("Contracts.cs", BoundaryTestData.WireSurface)
            .AddSource("Http.cs", BoundaryTestData.TwoTransports)
            .AddSource("Registration.cs", BoundaryTestData.ImplementationRegisteredResolvedAndAliasedTwice)
            .GenerateAsync();

        var allowed = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(IServiceProvider))
            .AddSource("Container.cs", BoundaryTestData.ContainerStandIn)
            .AddSource("Contracts.cs", BoundaryTestData.WireSurface)
            .AddSource("Http.cs", BoundaryTestData.TwoTransports)
            .AddSource("Registration.cs", BoundaryTestData.TheOneAliasPerChain)
            .GenerateAsync();

        // Then. Registered as itself, registered in a spelling the rule does not recognize,
        // aliased twice in one chain, and resolved — four clauses of B-008, one case each.
        NothingFailedToCompile(reported);
        NothingFailedToCompile(allowed);
        reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0015")
            .Should()
            .HaveCount(4, "a registration spelling the rule does not recognize is reported rather than passed");
        var spans = reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0015")
            .Select(static diagnostic => TextAt(diagnostic))
            .ToList();
        spans.Should().Contain("AddSingleton<OpenSkyHttpApi>", "registered as itself");
        spans.Should().Contain("Add", "a spelling the rule does not recognize, reported rather than passed");
        spans.Should().Contain("AddSingleton<IOpenSkyApi, OpenSkyBackupApi>", "the second alias in one chain");
        spans.Should().Contain("GetRequiredService<OpenSkyHttpApi>", "resolved by implementation type");
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .NotContain(
                static diagnostic => diagnostic.Id == "TRN0015",
                "aliasing the contract to one implementation is the shape the claim asks for");
    }

    [Fact]
    public async Task GivenACacheWrappedInATypeOfItsOwn_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var wrapped = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(IServiceProvider))
            .AddSource("Container.cs", BoundaryTestData.ContainerStandIn)
            .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
            .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
            .AddSource("Registration.cs", BoundaryTestData.CacheWrappedInATypeOfItsOwn)
            .GenerateAsync();

        var projected = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(IServiceProvider))
            .AddSource("Container.cs", BoundaryTestData.ContainerStandIn)
            .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
            .AddSource("Domain.cs", BoundaryTestData.Domain)
            .AddSource("Registration.cs", BoundaryTestData.CacheOfADomainTypeRegistered)
            .GenerateAsync();

        var allowed = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(IServiceProvider))
            .AddSource("Container.cs", BoundaryTestData.ContainerStandIn)
            .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
            .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
            .AddSource("Registration.cs", BoundaryTestData.TheOneCachePerClient)
            .GenerateAsync();

        var client = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(IServiceProvider))
            .AddSource("Container.cs", BoundaryTestData.ContainerStandIn)
            .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
            .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
            .AddSource("Client.cs", BoundaryTestData.ClientHoldingItsCache)
            .AddSource("Registration.cs", BoundaryTestData.ClientRegistered)
            .GenerateAsync();

        // Then. A wrapper and a projection are two of B-030's three clauses; the third — a policy
        // beside the key selector — needs a cache taking more than one argument, which the
        // stand-in's constructor cannot express, and § 9 records the claim as this rule's.
        NothingFailedToCompile(wrapped);
        NothingFailedToCompile(allowed);
        wrapped.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .ContainSingle()
            .Which.GetMessage()
            .Should()
            .Contain("a type of its own");
        projected.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0016")
            .Should()
            .ContainSingle()
            .Which.GetMessage()
            .Should()
            .Contain("a projection");
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .BeEmpty("a SourceCache of snapshots with no wrapper is what the claim asks for");
        NothingFailedToCompile(client);
        client.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .BeEmpty("the client holds the cache because B-015 says it must, and the writer above a cache is not a wrapper of it");
    }

    [Fact]
    public async Task GivenACacheRegisteredWithAnyLifetimeButTheApplicationsOrSharedBetweenClients_WhenAnalyzed_ThenItIsReported()
    {
        // Given, When
        var reported = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(IServiceProvider))
            .AddSource("Container.cs", BoundaryTestData.ContainerStandIn)
            .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
            .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
            .AddSource("Registration.cs", BoundaryTestData.CacheRegisteredScopedAndTwice)
            .GenerateAsync();

        var allowed = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(IServiceProvider))
            .AddSource("Container.cs", BoundaryTestData.ContainerStandIn)
            .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
            .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
            .AddSource("Registration.cs", BoundaryTestData.TheOneCachePerClient)
            .GenerateAsync();

        // Then. Both clauses: the lifetime, and one cache per client.
        NothingFailedToCompile(reported);
        NothingFailedToCompile(allowed);
        var messages = reported.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0017")
            .Select(static diagnostic => diagnostic.GetMessage())
            .ToList();
        messages.Should().HaveCount(2);
        messages.Should().Contain(static message => message.Contains("as scoped rather than with the application's lifetime"));
        messages.Should().Contain(static message => message.Contains("more than once"));
        allowed.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .NotContain(static diagnostic => diagnostic.Id == "TRN0017");
    }

    [Fact]
    public async Task GivenAForbiddenRegistration_WhenAnalyzed_ThenItIsReportedAtTheRegistrationCall()
    {
        // Given, When. The registration is in one file and the implementation's declaration in
        // another, so the span says which of the two the rule was evaluated against.
        var results = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(IServiceProvider))
            .AddSource("Container.cs", BoundaryTestData.ContainerStandIn)
            .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
            .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
            .AddSource("Registration.cs", BoundaryTestData.CacheRegisteredScopedAndTwice)
            .GenerateAsync();

        // Then
        NothingFailedToCompile(results);
        var reported = results.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.GetMessage().Contains("as scoped"))
            .Should()
            .ContainSingle()
            .Subject;
        reported.Location.GetLineSpan().Path.Should().Be("Registration.cs");
        TextAt(reported)
            .Should()
            .Be(
                "AddScoped<SourceCache<AircraftSnapshot, string>>",
                "the lifetime is an argument to the call, so the call is where the fix is");
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
