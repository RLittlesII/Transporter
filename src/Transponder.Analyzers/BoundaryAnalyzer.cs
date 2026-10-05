using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Transponder.Analyzers;

/// <summary>Reports the structural and boundary claims as compiler diagnostics, at the line that violates them.</summary>
/// <remarks>
/// One analyzer, not one per rule: eighteen classes would copy <see cref="Layers"/> eighteen times.
/// Claims are <c>aircraft-source</c> § 3; the mapping is <c>boundary-analyzer</c> § 7. One node
/// reports once — selected by what was named, then by who named it.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BoundaryAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Diagnostics.All;

    /// <inheritdoc/>
    /// <remarks>
    /// Generated code is excluded (B-005). Identifiers, not symbols (B-009): that is what makes a
    /// reference inside a method body visible, which ADR-0006 § Context argues reflection cannot.
    /// </remarks>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // B-044 is about a call rather than a type, so it registers separately. RSA1007 reports
        // both registrations with an empty symbol name, as RSA2011 did in Airframe#403.
#pragma warning disable RSA1007
        context.RegisterSyntaxNodeAction(AnalyzeTypeMention, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
        context.RegisterSyntaxNodeAction(AnalyzeCollectionMutation, SyntaxKind.InvocationExpression);
#pragma warning restore RSA1007
    }

    private static void AnalyzeTypeMention(SyntaxNodeAnalysisContext context)
    {
        // `var` resolves to the inferred type without naming it; reporting on it would double up
        // on the `new`.
        if (context.Node is IdentifierNameSyntax { IsVar: true })
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is not INamedTypeSymbol named)
        {
            return;
        }

        var enclosing = context.SemanticModel.GetEnclosingSymbol(context.Node.SpanStart, context.CancellationToken);

        if (enclosing is null || Layers.IsTest(enclosing))
        {
            return;
        }

        if (Layers.IsCache(named))
        {
            ReportCacheMention(context, named, enclosing);
        }
        else if (Layers.IsWireType(named))
        {
            ReportWireTypeMention(context, named, enclosing);
        }
        else if (Layers.IsSnapshot(named))
        {
            ReportSnapshotMention(context, named, enclosing);
        }
        else if (Layers.IsClient(named) || Layers.IsContract(named) || Layers.IsConcreteTracking(named))
        {
            ReportSourceInternalsMention(context, named, enclosing);
        }
    }

    private static void ReportWireTypeMention(SyntaxNodeAnalysisContext context, INamedTypeSymbol named, ISymbol enclosing)
    {
        if (Layers.ProviderOf(named) is not { } provider)
        {
            return;
        }

        if (!Layers.IsInsideIntegration(enclosing, provider))
        {
            // B-004: outside the integration the row is not visible at all.
            Report(context, Diagnostics.WireTypeOutsideIntegration, named.Name, provider, Containing(enclosing));

            return;
        }

        // B-045: inside, only the transport and the client. The contract declares the envelope.
        if (Layers.IsTransport(enclosing) || Layers.IsProviderRoot(enclosing) || Layers.IsWireSurface(enclosing))
        {
            return;
        }

        Report(context, Diagnostics.EnvelopeOrRowBeyondItsTwoHolders, Containing(enclosing), named.Name);
    }

    private static void ReportSnapshotMention(SyntaxNodeAnalysisContext context, INamedTypeSymbol named, ISymbol enclosing)
    {
        // Client, cache, projection, composition root. Everything else is past the projection.
        if (Layers.IsProviderRoot(enclosing) || Layers.IsTrackingLayer(enclosing) || Layers.IsCompositionRoot(enclosing))
        {
            return;
        }

        // A consumer breaks B-047, a claim about the consumer; anything else breaks B-046.
        Report(
            context,
            Layers.IsDownstream(enclosing)
                ? Diagnostics.UpstreamTypeNamedBelowTheTracker
                : Diagnostics.SnapshotDownstreamOfTheProjection,
            Containing(enclosing),
            named.Name);
    }

    private static void ReportSourceInternalsMention(SyntaxNodeAnalysisContext context, INamedTypeSymbol named, ISymbol enclosing)
    {
        if (Layers.IsCompositionRoot(enclosing))
        {
            return;
        }

        if (Layers.IsViewModel(enclosing))
        {
            // B-041.
            Report(context, Diagnostics.SourceInternalsNamedByAViewModel, Containing(enclosing), named.Name);

            return;
        }

        if (Layers.IsDownstream(enclosing))
        {
            Report(context, Diagnostics.UpstreamTypeNamedBelowTheTracker, Containing(enclosing), named.Name);
        }
    }

    private static void ReportCacheMention(SyntaxNodeAnalysisContext context, INamedTypeSymbol named, ISymbol enclosing)
    {
        // B-032 holds wherever the cache is typed, composition root included.
        if (named.TypeArguments.FirstOrDefault(Layers.IsDomain) is { } domain)
        {
            Report(context, Diagnostics.DomainTypeNamedByTheCache, domain.Name);

            return;
        }

        ReportSourceInternalsMention(context, named, enclosing);
    }

    private static void AnalyzeCollectionMutation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member })
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(member, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        // The receiver's type, not the declaring one: Add and Clear come from Collection<T>.
        var receiver = context.SemanticModel.GetTypeInfo(member.Expression, context.CancellationToken).Type as INamedTypeSymbol;

        if (!Mutators.Contains(method.Name) || !IsBoundCollection(receiver))
        {
            return;
        }

        var enclosing = context.SemanticModel.GetEnclosingSymbol(context.Node.SpanStart, context.CancellationToken);

        if (enclosing is null || Layers.IsTest(enclosing))
        {
            return;
        }

        // B-044, on the member called rather than the statement.
        context.ReportDiagnostic(
            Diagnostic.Create(
                Diagnostics.BoundCollectionMutatedImperatively,
                member.Name.GetLocation(),
                Containing(enclosing)));
    }

    private static bool IsBoundCollection(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name is "ObservableCollection" or "ReadOnlyObservableCollection")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports at the node, naming the symbol — B-004.</summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="descriptor">The rule reporting.</param>
    /// <param name="arguments">The message arguments.</param>
    private static void Report(SyntaxNodeAnalysisContext context, DiagnosticDescriptor descriptor, params object[] arguments) =>
        context.ReportDiagnostic(Diagnostic.Create(descriptor, context.Node.GetLocation(), arguments));

    private static string Containing(ISymbol symbol) =>
        symbol.ContainingType is { } type ? type.ToDisplayString() : symbol.ToDisplayString();

    private static readonly ImmutableHashSet<string> Mutators = ImmutableHashSet.Create(
        "Add",
        "AddRange",
        "Clear",
        "Insert",
        "InsertRange",
        "Remove",
        "RemoveAt",
        "RemoveRange");
}
