using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Transponder.CodeFixes;

/// <summary>Moves a contract method's <see cref="CancellationToken"/> to last — <c>aircraft-source</c> B-005.</summary>
/// <remarks>
/// The compliant order is the claim's, not a choice, which is why this one has a fix at all
/// (<c>boundary-analyzer</c> B-020 § 7). <c>TRN0009</c> reports four clauses and only this one is
/// mechanical, so the offer is made from the parameter list rather than from the message.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CancellationTokenLastFix))]
[Shared]
public sealed class CancellationTokenLastFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(Diagnostics.ContractMethodShape);

    /// <inheritdoc/>
    /// <returns>The batch fixer, so "fix all in file" moves every misplaced token.</returns>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    /// <param name="context">The fix context.</param>
    /// <returns>A task that completes once the offer has been registered, or declined.</returns>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root?.FindNode(context.Span).FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } method)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (model is null || IndexOfToken(method, model, context.CancellationToken) is var index && index < 0)
        {
            return;
        }

        // The other three clauses of B-005 — an overload, a return type, a method with no token at
        // all — are a design decision each, so nothing is offered for them.
        if (index == method.ParameterList.Parameters.Count - 1)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Move the CancellationToken to last",
                token => Moved(context.Document, root, method, index),
                equivalenceKey: nameof(CancellationTokenLastFix)),
            context.Diagnostics);
    }

    private static Task<Document> Moved(Document document, SyntaxNode root, MethodDeclarationSyntax method, int index)
    {
        var parameters = method.ParameterList.Parameters;
        var moved = parameters[index].WithoutTrivia();

        var reordered = method.ParameterList
            .WithParameters(parameters.RemoveAt(index).Add(moved))
            .WithAdditionalAnnotations(Formatter.Annotation);

        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(method.ParameterList, reordered)));
    }

    private static int IndexOfToken(MethodDeclarationSyntax method, SemanticModel model, CancellationToken cancellation)
    {
        var parameters = method.ParameterList.Parameters;

        for (var index = 0; index < parameters.Count; index++)
        {
            if (model.GetDeclaredSymbol(parameters[index], cancellation) is IParameterSymbol { Type.Name: nameof(CancellationToken) })
            {
                return index;
            }
        }

        return -1;
    }
}
