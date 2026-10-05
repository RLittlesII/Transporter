using System;
using Microsoft.CodeAnalysis;

namespace Transponder.Analyzers;

/// <summary>Which layer a symbol belongs to, read from its containing namespace.</summary>
/// <remarks>
/// The one place that answers it, so the reference rules ask here rather than each deciding what a
/// layer is. The namespaces are the folders <c>transponder-conventions</c> references/coding.md § "Project structure"
/// fixes; <c>adr/0002</c> says why the convention is read instead of a marker attribute, and
/// carries the hazard: a misfiled file silently changes which rules apply to it.
/// </remarks>
internal static class Layers
{
    /// <summary>The namespace every integration sits under; a provider is the next segment.</summary>
    internal const string Integrations = "Transponder.Integrations";

    /// <summary>The namespace the test project sits under.</summary>
    internal const string Tests = "Transponder.UnitTests";

    /// <summary>The provider whose integration declared a symbol, or <see langword="null"/>.</summary>
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

    /// <summary>Whether a symbol sits inside one provider's integration.</summary>
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

    /// <summary>Whether a symbol belongs to the test project, which names internals by design.</summary>
    /// <remarks>B-004 names a domain type, a cache, a strategy and a view — all production layers.</remarks>
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
