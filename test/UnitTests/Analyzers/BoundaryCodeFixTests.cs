using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Rocket.Surgery.Extensions.Testing.SourceGenerators;
using Transponder.Analyzers;
using Transponder.CodeFixes;

namespace Transponder.UnitTests.Analyzers;

/// <summary>The two fixes B-020 asks for, and the shape of a pass that applies one.</summary>
/// <remarks>
/// One action per pass, then re-analyze — the way an editor applies a fix, and the only way the
/// ordering dependency in a fix that works alone and nowhere else shows up. A document carrying
/// several diagnostics can pair a resolved fix with another rule's code actions
/// (<c>RocketSurgeonsGuild/Airframe#359</c>), so these assert the resulting text rather than which
/// rule a pass resolved.
/// </remarks>
public class BoundaryCodeFixTests
{
    [Fact]
    public async Task GivenAContractMethodTakingCancellationFirst_WhenTheFixIsApplied_ThenTheTokenIsLastAndNothingElseMoves()
    {
        // Given, When
        var results = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithCodeFix<CancellationTokenLastFix>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.ContractMethodTakingCancellationFirst)
            .GenerateAsync();

        // Then
        var fixedText = await Applied(results, typeof(CancellationTokenLastFix));
        fixedText.Should().Contain("GetStates(double lamin, CancellationToken cancellationToken)");
        fixedText.Should().Contain("Task<int>", "a fix changes only what the claim requires");
        fixedText.Should().Contain("internal interface IOpenSkyApi");
    }

    [Fact]
    public async Task GivenAFixedContractMethod_WhenItIsAnalyzedAgain_ThenNothingIsReportedAndNoSecondChangeIsOffered()
    {
        // Given, When. The fix's own output, fed back in.
        var results = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithCodeFix<CancellationTokenLastFix>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.ContractMethodTakingCancellationFirst)
            .GenerateAsync();

        var reanalyzed = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithCodeFix<CancellationTokenLastFix>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", await Applied(results, typeof(CancellationTokenLastFix)))
            .GenerateAsync();

        // Then
        reanalyzed.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .NotContain(static diagnostic => diagnostic.Id == "TRN0009", "the clause the fix answers is answered");
        Actions(reanalyzed, typeof(CancellationTokenLastFix))
            .Should()
            .BeEmpty("applying it twice offers no second change");
    }

    [Fact]
    public async Task GivenAPublicImplementation_WhenTheFixesAreApplied_ThenItIsInternalSealedAndImplementsExplicitly()
    {
        // Given, When
        var results = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithCodeFix<ContractImplementationShapeFix>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.PublicContract)
            .AddSource("Http.cs", BoundaryTestData.PublicImplementation)
            .GenerateAsync();

        // Then. Three clauses reported, and the two mechanical ones each offered their own action.
        results.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Where(static diagnostic => diagnostic.Id == "TRN0011")
            .Should()
            .HaveCount(3);

        var texts = await Task.WhenAll(
            Actions(results, typeof(ContractImplementationShapeFix))
                .Select(static action => TextOf(action)));

        texts.Should().Contain(static text => text.Contains("internal sealed class OpenSkyHttpApi"));
        texts.Should().Contain(static text => text.Contains("IOpenSkyApi.GetStates("));
        texts.Should().AllSatisfy(static text =>
            text.Should().Contain("Task.FromResult", "a fix rewrites the declaration, never the body under it"));
    }

    [Fact]
    public async Task GivenAnImplementationTheClaimAllows_WhenItIsAnalyzed_ThenNoFixIsOffered()
    {
        // Given, When
        var results = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithCodeFix<ContractImplementationShapeFix>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddSource("Contracts.cs", BoundaryTestData.PublicContract)
            .AddSource("Http.cs", BoundaryTestData.TheOneImplementationPerTransport)
            .GenerateAsync();

        // Then
        results.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .NotContain(static diagnostic => diagnostic.Id == "TRN0011");
        Actions(results, typeof(ContractImplementationShapeFix)).Should().BeEmpty();
    }

    [Fact]
    public async Task GivenACacheRegisteredScoped_WhenTheFixIsApplied_ThenItTakesTheApplicationsLifetime()
    {
        // Given, When
        var results = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithCodeFix<CacheLifetimeFix>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(IServiceProvider))
            .AddSource("Container.cs", BoundaryTestData.ContainerStandIn)
            .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
            .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
            .AddSource("Registration.cs", BoundaryTestData.CacheRegisteredScoped)
            .GenerateAsync();

        // Then. One call, one name: the type argument rides along unchanged.
        var fixedText = await Applied(results, typeof(CacheLifetimeFix));
        fixedText.Should().Contain("AddSingleton<SourceCache<AircraftSnapshot, string>>");
        fixedText.Should().NotContain("AddScoped");
    }

    [Fact]
    public async Task GivenACacheRegisteredTwice_WhenItIsAnalyzed_ThenNoFixIsOffered()
    {
        // Given, When
        var results = await GeneratorTestContextBuilder
            .Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithCodeFix<CacheLifetimeFix>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(IServiceProvider))
            .AddSource("Container.cs", BoundaryTestData.ContainerStandIn)
            .AddSource("Cache.cs", BoundaryTestData.CacheStandIn)
            .AddSource("Snapshot.cs", BoundaryTestData.Snapshot)
            .AddSource("Registration.cs", BoundaryTestData.CacheRegisteredTwice)
            .GenerateAsync();

        // Then. Which of the two registrations survives is a design decision, so the rule reports
        // and offers nothing — the half of TRN0017 § 7 records as unfixable.
        results.AnalyzerResults[typeof(BoundaryAnalyzer)]
            .Diagnostics
            .Should()
            .ContainSingle()
            .Which.GetMessage()
            .Should()
            .Contain("more than once");
        Actions(results, typeof(CacheLifetimeFix)).Should().BeEmpty();
    }

    [Fact]
    public void GivenTheShippedFixes_WhenTheirFixableIdsAreRead_ThenEachIsOneTheSpecificationMarksFixable()
    {
        // Given
        var shipped = new CodeFixProvider[]
        {
            new CancellationTokenLastFix(),
            new ContractImplementationShapeFix(),
            new CacheLifetimeFix(),
        };

        // When
        var ids = shipped.SelectMany(static provider => provider.FixableDiagnosticIds.AsEnumerable()).ToArray();

        // Then
        ids.Should().OnlyHaveUniqueItems("one fix per diagnostic, so an editor never offers two answers to one claim");
        ids.Should().BeSubsetOf(
            FixableIds(),
            "a fix for a diagnostic § 7 records as having none is a fix guessing at a design decision");
    }

    private static IEnumerable<string> FixableIds() =>
        SpecificationTables
            .Rows(Specification, SpecificationTables.CodeFixRow)
            .Where(static cells => cells[1] == "Yes")
            .SelectMany(static cells => SpecificationTables.Ids(cells[0]));

    private static async Task<string> Applied(GeneratorTestResults results, Type fix)
    {
        var action = Actions(results, fix).Should().ContainSingle("one clause here is mechanical, and one action answers it").Subject;

        return await TextOf(action);
    }

    private static IEnumerable<CodeActionTestResult> Actions(GeneratorTestResults results, Type fix) =>
        results.CodeFixResults.TryGetValue(fix, out var resolved)
            ? resolved.ResolvedFixes.SelectMany(static entry => entry.CodeActions)
            : [];

    /// <summary>Extracts the text of the applied code action.</summary>
    /// <remarks>
    /// The action is applied here rather than read off <c>Changes</c>, which can carry another
    /// diagnostic's edit when one document holds several (<c>RocketSurgeonsGuild/Airframe#359</c>).
    /// <c>TargetDocument</c> is the document the action was offered on, before the fix.
    /// </remarks>
    private static async Task<string> TextOf(CodeActionTestResult action)
    {
        var operations = await action.CodeAction.GetOperationsAsync(CancellationToken.None);

        var changed = operations
            .OfType<ApplyChangesOperation>()
            .Select(operation => operation.ChangedSolution.GetDocument(action.TargetDocument.Id))
            .FirstOrDefault(static document => document is not null);

        return (await (changed ?? action.TargetDocument).GetTextAsync()).ToString();
    }

    private const string SpecificationPath = "features/boundary-analyzer/.spec/README.md";

    private static readonly string Specification = SpecificationTables.Read(SpecificationPath);
}
