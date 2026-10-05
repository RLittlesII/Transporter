using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Transponder.Analyzers;

/// <summary>
/// Reports the structural and boundary claims as compiler diagnostics, at the line that violates
/// them.
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

        // RSA1007 reports this registration with an empty symbol name, as RSA2011 did in
        // Airframe#403. Nothing here is called with parentheses.
#pragma warning disable RSA1007
        context.RegisterSyntaxNodeAction(AnalyzeTypeMention, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
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
