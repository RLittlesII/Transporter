using System;
using Microsoft.CodeAnalysis;

namespace Transponder.Analyzers;

/// <summary>
/// Which layer a symbol belongs to, read from its containing namespace.
/// <para>
/// The one place in the analyzer that answers that question, so the reference rules ask here
/// rather than each deciding what a layer is. The namespaces are the folders
/// <c>transponder-conventions</c> § "Project structure" fixes, which <c>.editorconfig</c>
/// requires a file's namespace to match — see <c>features/boundary-analyzer/.spec/adr/0002</c>
/// for why that convention is read instead of a marker attribute, and for the hazard it carries:
/// a misfiled file silently changes which rules apply to it.
/// </para>
/// </summary>
internal static class Layers
{
    /// <summary>
    /// The namespace every integration sits under. A provider's own namespace is the next
    /// segment after it.
    /// </summary>
    internal const string Integrations = "Transponder.Integrations";

    /// <summary>
    /// The namespace the test project sits under. Tests reach an integration's internals
    /// deliberately, through <c>InternalsVisibleTo</c>, so they are not a layer a boundary rule
    /// holds out — <c>aircraft-source</c> B-004 names a domain type, a cache, a strategy and a
    /// view, all of which are production layers.
    /// </summary>
    internal const string Tests = "Transponder.UnitTests";

    /// <summary>
    /// The provider whose integration a symbol was declared in, or <see langword="null"/> when it
    /// was not declared in one.
    /// </summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns>The provider's name, as the namespace segment spells it.</returns>
    internal static string? ProviderOf(ISymbol symbol)
    {
        var containing = NamespaceOf(symbol);

        if (!containing.StartsWith(Integrations + ".", StringComparison.Ordinal))
        {
            return null;
        }

        var rest = containing.Substring(Integrations.Length + 1);
        var separator = rest.IndexOf('.');

        return separator < 0 ? rest : rest.Substring(0, separator);
    }

    /// <summary>
    /// Whether a symbol is part of a provider's wire surface — the types its API contract's
    /// methods name, which live in that provider's <c>Contracts</c> namespace.
    /// </summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a wire type.</returns>
    internal static bool IsWireType(ISymbol symbol)
    {
        var provider = ProviderOf(symbol);

        return provider is not null
            && NamespaceOf(symbol) == $"{Integrations}.{provider}.Contracts";
    }

    /// <summary>
    /// Whether a symbol sits inside one provider's integration, and so may name that provider's
    /// wire types.
    /// </summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <param name="provider">The provider whose integration is being asked about.</param>
    /// <returns><see langword="true"/> when the symbol is inside that integration.</returns>
    internal static bool IsInsideIntegration(ISymbol symbol, string provider)
    {
        var containing = NamespaceOf(symbol);
        var integration = $"{Integrations}.{provider}";

        return containing == integration
            || containing.StartsWith(integration + ".", StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether a symbol belongs to the test project, which names an integration's internals by
    /// design and is held out of the boundary rules for that reason.
    /// </summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a test's.</returns>
    internal static bool IsTest(ISymbol symbol)
    {
        var containing = NamespaceOf(symbol);

        return containing == Tests
            || containing.StartsWith(Tests + ".", StringComparison.Ordinal);
    }

    private static string NamespaceOf(ISymbol symbol) =>
        symbol.ContainingNamespace is { IsGlobalNamespace: false } containing
            ? containing.ToDisplayString()
            : string.Empty;
}
