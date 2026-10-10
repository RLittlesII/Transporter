using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Transporter.CodeFixes;

/// <summary>Registers the cache with the application's lifetime — <c>aircraft-source</c> B-031.</summary>
/// <remarks>
/// One call, one argument, and the lifetime the claim names rather than a choice
/// (<c>boundary-analyzer</c> § 7). <c>TRN0017</c>'s other clause — the same cache registered twice,
/// so two clients share one — has no fix: which registration survives is the author's call.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CacheLifetimeFix))]
[Shared]
public sealed class CacheLifetimeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(Diagnostics.CacheLifetime);

    /// <inheritdoc/>
    /// <returns>The batch fixer, so one pass settles every cache in a composition root.</returns>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    /// <param name="context">The fix context.</param>
    /// <returns>A task that completes once the offer has been registered, or declined.</returns>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root?.FindNode(context.Span) is not { } node
            || NameOf(node) is not { } name
            || Application(name.Identifier.ValueText) is not { } application)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Register the cache with the application's lifetime",
                token => Task.FromResult(
                    context.Document.WithSyntaxRoot(root.ReplaceNode(name, Renamed(name, application)))),
                equivalenceKey: nameof(CacheLifetimeFix)),
            context.Diagnostics);
    }

    /// <remarks>The identifier only: a type argument list rides along unchanged.</remarks>
    private static SimpleNameSyntax? NameOf(SyntaxNode node) =>
        node switch
        {
            SimpleNameSyntax name => name,
            MemberAccessExpressionSyntax member => member.Name,
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } => member.Name,
            _ => node.Parent is null ? null : NameOf(node.Parent),
        };

    /// <remarks>
    /// The application-lifetime spelling of a scoped or transient registration, or
    /// <see langword="null"/> when the name is neither — the clause with no mechanical fix.
    /// </remarks>
    private static string? Application(string registration) =>
        registration switch
        {
            "AddScoped" or "AddTransient" => "AddSingleton",
            "AddKeyedScoped" or "AddKeyedTransient" => "AddKeyedSingleton",
            "TryAddScoped" or "TryAddTransient" => "TryAddSingleton",
            _ => null,
        };

    private static SimpleNameSyntax Renamed(SimpleNameSyntax name, string application) =>
        name switch
        {
            GenericNameSyntax generic => generic
                .WithIdentifier(SyntaxFactory.Identifier(application))
                .WithTriviaFrom(generic)
                .WithAdditionalAnnotations(Formatter.Annotation),
            _ => SyntaxFactory.IdentifierName(application)
                .WithTriviaFrom(name)
                .WithAdditionalAnnotations(Formatter.Annotation),
        };
}
