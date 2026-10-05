using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Transponder.Analyzers;

/// <summary>
/// Reports the structural and boundary claims <c>ADR-0006</c> assigns to an analyzer as compiler
/// diagnostics, at the line that violates them.
/// <para>
/// One analyzer carrying every rule rather than one class per rule: eighteen classes would hold
/// eighteen copies of the layer identification <see cref="Layers"/> owns, which is the part most
/// likely to move. The claims themselves live in
/// <c>features/aircraft-source/.spec/README.md</c> § 3, and which diagnostic enforces which claim
/// is <c>features/boundary-analyzer/.spec/README.md</c> § 7.
/// </para>
/// <para>
/// <b>One node, one diagnostic.</b> Several claims land on the same reference — a view model naming
/// a cache breaks B-041 and B-047 both — so a rule is selected by <i>what was named</i> first and
/// by <i>who named it</i> second, and every branch reports once. Two diagnostics on one line is two
/// things to suppress for one mistake.
/// </para>
/// </summary>
/// <remarks>
/// One analyzer, not one per rule: eighteen classes would copy <see cref="Layers"/> eighteen times.
/// Claims are <c>aircraft-source</c> § 3; the mapping is <c>boundary-analyzer</c> § 7.
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

        // B-009. Every mention of a type arrives as an identifier — a local's type, a `new`, a
        // cast, a generic argument at a call site, a `typeof` — so registering here is what makes
        // a reference inside a method body visible. A symbol action over signatures is the
        // mechanism ADR-0006 § Context rules out. B-044 is the one claim in this family that is
        // about a call rather than a type, so it has a registration of its own.
        //
        // RSA1007 reports both registrations — "use the Invoke() method to call functions instead
        // of using parentheses" — with an empty symbol name, the way RSA2011 reported with none in
        // RocketSurgeonsGuild/Airframe#403. Nothing here is a function called with parentheses:
        // RegisterSyntaxNodeAction takes the action and the compiler calls it later. Suppressed at
        // the line rather than for the project, which is what B-004 asks our own diagnostics to
        // make possible.
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
            // B-004, the broader of the two claims about the row: outside the integration it is
            // not visible at all.
            Report(context, Diagnostics.WireTypeOutsideIntegration, named.Name, provider, Containing(enclosing));

            return;
        }

        // B-045 is the narrower one: inside the integration, only the class implementing the
        // contract and the snapshot client may hold the envelope and the row. The contract's own
        // namespace is allowed because the contract declares the envelope as what it returns.
        if (Layers.IsTransport(enclosing) || Layers.IsProviderRoot(enclosing) || Layers.IsWireSurface(enclosing))
        {
            return;
        }

        Report(context, Diagnostics.EnvelopeOrRowBeyondItsTwoHolders, Containing(enclosing), named.Name);
    }

    private static void ReportSnapshotMention(SyntaxNodeAnalysisContext context, INamedTypeSymbol named, ISymbol enclosing)
    {
        // The client holds it, its cache stores it, the projection reads it, and a composition root
        // registers the cache it goes in. Everything else is past the projection it dies at.
        if (Layers.IsProviderRoot(enclosing) || Layers.IsTrackingLayer(enclosing) || Layers.IsCompositionRoot(enclosing))
        {
            return;
        }

        // A consumer of IFleetTracker naming a snapshot breaks B-047, which is a claim about the
        // consumer; anything else naming it breaks B-046, which is a claim about the snapshot.
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
            // B-041. A view model depends on IFleetTracker and names none of what fills it.
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
        // B-032 holds wherever the cache is typed: it stores what the provider reported, and the
        // domain is a layer above it. Reported before the referencer is considered, because a cache
        // of domain types is wrong even in the composition root that registers it.
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

        // The receiver's type, not the method's declaring type: ObservableCollection<T> inherits
        // Add, Clear and the rest from Collection<T>, so asking where the method was declared
        // answers Collection every time and the rule reports nothing.
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

        // B-044. Reported on the member being called rather than on the statement, so the
        // diagnostic lands on `Add` and not on a line that also holds what was added.
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

    private static void Report(SyntaxNodeAnalysisContext context, DiagnosticDescriptor descriptor, params object[] arguments) =>
        // B-004. The location is always the node that named the type and the message always names a
        // symbol: a diagnostic carrying neither is one nobody can fix at the violation.
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
