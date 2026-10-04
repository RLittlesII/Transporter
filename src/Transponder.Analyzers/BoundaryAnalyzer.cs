using System.Collections.Immutable;
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
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BoundaryAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Diagnostics.All;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        // B-005. A diagnostic on a line nobody wrote cannot be acted on and cannot be suppressed
        // where it lands, so it gets the rule switched off for the whole project instead —
        // RocketSurgeonsGuild/Airframe#403 is that failure, observed in this repository's build.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // B-009. Every mention of a type arrives as an identifier — a local's type, a `new`, a
        // cast, a generic argument at a call site, a `typeof` — so registering here is what makes
        // a reference inside a method body visible. A symbol action over signatures is the
        // mechanism ADR-0006 § Context rules out.
        // RSA1007 reports this registration — "use the Invoke() method to call functions instead
        // of using parentheses" — with an empty symbol name, the way RSA2011 reported with none in
        // RocketSurgeonsGuild/Airframe#403. There is no function being called with parentheses
        // here: RegisterSyntaxNodeAction takes the action and the compiler calls it later. It is
        // suppressed at the line rather than for the project, which is what B-004 asks our own
        // diagnostics to make possible.
#pragma warning disable RSA1007
        context.RegisterSyntaxNodeAction(AnalyzeTypeMention, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
#pragma warning restore RSA1007
    }

    private static void AnalyzeTypeMention(SyntaxNodeAnalysisContext context)
    {
        // `var` resolves to the inferred type but does not name it, and reporting on it would put a
        // second diagnostic on the same line as the `new`. B-009's claim is about a type being
        // named — a local's declared type, a `new`, a cast, a generic argument — so an inferred
        // local is the one mention that is not one.
        if (context.Node is IdentifierNameSyntax { IsVar: true })
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is not INamedTypeSymbol named)
        {
            return;
        }

        if (!Layers.IsWireType(named))
        {
            return;
        }

        var provider = Layers.ProviderOf(named);
        var enclosing = context.SemanticModel.GetEnclosingSymbol(context.Node.SpanStart, context.CancellationToken);

        if (provider is null || enclosing is null)
        {
            return;
        }

        if (Layers.IsInsideIntegration(enclosing, provider) || Layers.IsTest(enclosing))
        {
            return;
        }

        // B-004. The location is the identifier that named the type, and the message names the
        // symbol: a diagnostic carrying neither is one nobody can fix at the violation.
        context.ReportDiagnostic(
            Diagnostic.Create(
                Diagnostics.WireTypeOutsideIntegration,
                context.Node.GetLocation(),
                named.Name,
                provider,
                Containing(enclosing)));
    }

    private static string Containing(ISymbol symbol) =>
        symbol.ContainingType is { } type ? type.ToDisplayString() : symbol.ToDisplayString();
}
