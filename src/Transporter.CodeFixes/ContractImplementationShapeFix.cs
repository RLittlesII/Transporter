using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Transporter.CodeFixes;

/// <summary>Adds the <c>internal</c> and <c>sealed</c> B-007 names, and makes an endpoint method an explicit implementation.</summary>
/// <remarks>
/// Every modifier here is the claim's own word, which is what makes it a fix rather than a guess
/// (<c>boundary-analyzer</c> B-020 § 7). The clause <c>TRN0011</c> reports that has no fix is the
/// second implementation for one transport: which of the two survives is the author's call.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ContractImplementationShapeFix))]
[Shared]
public sealed class ContractImplementationShapeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(Diagnostics.ContractImplementationShape);

    /// <inheritdoc/>
    /// <returns>The batch fixer, so one pass settles a declaration reported for several clauses.</returns>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    /// <param name="context">The fix context.</param>
    /// <returns>A task that completes once the offer has been registered, or declined.</returns>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        var node = root.FindNode(context.Span);

        if (node.FirstAncestorOrSelf<MethodDeclarationSyntax>() is { } method)
        {
            await RegisterExplicitImplementation(context, root, method).ConfigureAwait(false);

            return;
        }

        if (node.FirstAncestorOrSelf<ClassDeclarationSyntax>() is { } declaration)
        {
            RegisterModifiers(context, root, declaration);
        }
    }

    private static void RegisterModifiers(CodeFixContext context, SyntaxNode root, ClassDeclarationSyntax declaration)
    {
        var accessibility = declaration.Modifiers.Where(static modifier => Accessibility.Contains(modifier.Kind())).ToList();
        var sealedAlready = declaration.Modifiers.Any(SyntaxKind.SealedKeyword);
        var internalAlready = accessibility.Count == 1 && accessibility[0].IsKind(SyntaxKind.InternalKeyword);

        // Already internal and sealed, so the report is the one clause with no mechanical fix.
        if (sealedAlready && internalAlready)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Make the implementation internal and sealed",
                token => Task.FromResult(
                    context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, Narrowed(declaration)))),
                equivalenceKey: nameof(RegisterModifiers)),
            context.Diagnostics);
    }

    private static ClassDeclarationSyntax Narrowed(ClassDeclarationSyntax declaration)
    {
        var leading = declaration.Modifiers.Count > 0 ? declaration.Modifiers[0].LeadingTrivia : declaration.GetLeadingTrivia();

        var kept = declaration.Modifiers
            .Where(static modifier => !Accessibility.Contains(modifier.Kind()) && !modifier.IsKind(SyntaxKind.SealedKeyword))
            .Select(static modifier => modifier.WithoutTrivia());

        var modifiers = SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.InternalKeyword).WithLeadingTrivia(leading).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.Token(SyntaxKind.SealedKeyword).WithTrailingTrivia(SyntaxFactory.Space))
            .AddRange(kept.Select(static modifier => modifier.WithTrailingTrivia(SyntaxFactory.Space)));

        return (declaration.Modifiers.Count > 0 ? declaration : declaration.WithoutLeadingTrivia())
            .WithModifiers(modifiers)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static async Task RegisterExplicitImplementation(
        CodeFixContext context,
        SyntaxNode root,
        MethodDeclarationSyntax method)
    {
        if (method.ExplicitInterfaceSpecifier is not null)
        {
            return;
        }

        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (model is null
            || model.GetDeclaredSymbol(method, context.CancellationToken) is not { } declared
            || Implemented(declared, context.CancellationToken) is not { } endpoint)
        {
            return;
        }

        var name = SyntaxFactory.ParseName(endpoint.ContainingType.ToMinimalDisplayString(model, method.SpanStart));

        context.RegisterCodeFix(
            CodeAction.Create(
                "Implement the endpoint explicitly",
                token => Task.FromResult(
                    context.Document.WithSyntaxRoot(root.ReplaceNode(method, Explicit(method, name)))),
                equivalenceKey: nameof(RegisterExplicitImplementation)),
            context.Diagnostics);
    }

    private static MethodDeclarationSyntax Explicit(MethodDeclarationSyntax method, NameSyntax name)
    {
        var leading = method.Modifiers.Count > 0 ? method.Modifiers[0].LeadingTrivia : method.GetLeadingTrivia();

        // An explicit implementation carries no accessibility at all, so the modifiers the claim
        // forbids are removed rather than replaced.
        var kept = method.Modifiers
            .Where(static modifier => !Accessibility.Contains(modifier.Kind()))
            .Select(static modifier => modifier.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.Space))
            .ToList();

        var updated = method
            .WithModifiers(SyntaxFactory.TokenList(kept))
            .WithExplicitInterfaceSpecifier(SyntaxFactory.ExplicitInterfaceSpecifier(name));

        return (kept.Count > 0
                ? updated.WithModifiers(SyntaxFactory.TokenList(kept.Select((modifier, index) =>
                    index == 0 ? modifier.WithLeadingTrivia(leading) : modifier)))
                : updated.WithReturnType(updated.ReturnType.WithLeadingTrivia(leading)))
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static IMethodSymbol? Implemented(IMethodSymbol method, CancellationToken cancellation)
    {
        foreach (var contract in method.ContainingType.AllInterfaces)
        {
            cancellation.ThrowIfCancellationRequested();

            foreach (var endpoint in contract.GetMembers().OfType<IMethodSymbol>())
            {
                if (SymbolEqualityComparer.Default.Equals(
                        method.ContainingType.FindImplementationForInterfaceMember(endpoint),
                        method))
                {
                    return endpoint;
                }
            }
        }

        return null;
    }

    private static readonly ImmutableHashSet<SyntaxKind> Accessibility = ImmutableHashSet.Create(
        SyntaxKind.PublicKeyword,
        SyntaxKind.InternalKeyword,
        SyntaxKind.ProtectedKeyword,
        SyntaxKind.PrivateKeyword);
}
