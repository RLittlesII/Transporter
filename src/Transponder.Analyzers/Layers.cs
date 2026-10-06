using System;
using Microsoft.CodeAnalysis;

namespace Transponder.Analyzers;

/// <summary>Which layer a symbol belongs to, read from its containing namespace.</summary>
/// <remarks>
/// The one place that answers it. Namespaces are the folders <c>transponder-conventions</c>
/// references/coding.md § "Project structure" fixes; <c>adr/0002</c> says why, and names the
/// hazard — a misfiled file silently changes which rules apply to it.
/// </remarks>
internal static class Layers
{
    /// <summary>The namespace every integration sits under; a provider is the next segment.</summary>
    internal const string Integrations = "Transponder.Integrations";

    /// <summary>The namespace the domain model sits under; it survives the provider being replaced.</summary>
    internal const string Model = "Transponder.Model";

    /// <summary>The namespace the per-type tracker sources and the swapping decorator sit under.</summary>
    internal const string Tracking = "Transponder.Tracking";

    /// <summary>The namespace a feature's own code sits under, view models included.</summary>
    internal const string Features = "Transponder.Features";

    /// <summary>The user interface host's root namespace.</summary>
    internal const string Host = "Gui";

    /// <summary>The namespace the test project sits under.</summary>
    /// <remarks>Tests name internals by design; B-004 names production layers.</remarks>
    internal const string Tests = "Transponder.UnitTests";

    /// <summary>The last namespace segment of every composition root.</summary>
    internal const string Registration = "Container";

    /// <summary>The provider whose integration a symbol was declared in, or <see langword="null"/> when it was not declared in one.</summary>
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

    /// <summary>Whether a symbol is one of a provider's wire data types.</summary>
    /// <remarks>The contract interface and the exception are not: B-008 and ADR-0008 let a consumer name both.</remarks>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a wire data type.</returns>
    internal static bool IsWireType(ISymbol symbol) =>
        IsWireSurface(symbol)
        && symbol is INamedTypeSymbol { TypeKind: not TypeKind.Interface } type
        && !IsException(type);

    /// <summary>Whether a symbol is a provider's API contract interface.</summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a contract interface.</returns>
    internal static bool IsContract(ISymbol symbol) =>
        IsWireSurface(symbol) && symbol is INamedTypeSymbol { TypeKind: TypeKind.Interface };

    /// <summary>Whether a symbol is a provider's positional row — the wire type that keeps the provider's own element order instead of naming its fields.</summary>
    /// <remarks>By suffix, as <see cref="IsSnapshot"/> and <see cref="IsClient"/> are: the order is the provider's, and no structural test separates a row from a type that merely has an indexer.</remarks>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a positional row.</returns>
    internal static bool IsPositionalRow(ISymbol symbol) =>
        IsWireType(symbol) && symbol.Name.EndsWith("Row", StringComparison.Ordinal);

    /// <summary>Whether a symbol is a provider's response envelope — the wire type a call returns, holding the positional rows and the instant they were reported at.</summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is an envelope.</returns>
    internal static bool IsEnvelope(ISymbol symbol) =>
        IsWireType(symbol)
        && !IsPositionalRow(symbol)
        && symbol.Name.EndsWith("Response", StringComparison.Ordinal);

    /// <summary>Whether a symbol sits in a provider's <c>Contracts</c> namespace at all.</summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is part of the wire surface.</returns>
    internal static bool IsWireSurface(ISymbol symbol) =>
        ProviderOf(symbol) is { } provider && NamespaceOf(symbol) == $"{Integrations}.{provider}.Contracts";

    /// <summary>Whether a symbol is a provider's transport — code in the <c>Http</c> namespace or under it, where implementations and converters live.</summary>
    /// <remarks>Transport code is identified by namespace prefix (anywhere under .../Http) rather than by exact folder depth, so folder reorganization under the provider (adding subfolders like .../Http/Api) does not silently disable transport-specific rules.</remarks>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is transport code.</returns>
    internal static bool IsTransport(ISymbol symbol)
    {
        // Try the symbol as a type first (most common case: a method or property inside a transport type)
        var type = EffectiveType(symbol);
        if (type is not null && ProviderOf(type) is { } provider)
        {
            var typeNs = NamespaceOf(type);
            if (typeNs.StartsWith($"{Integrations}.{provider}.Http", StringComparison.Ordinal))
            {
                return true;
            }
        }

        // For namespace symbols (rare case: GetEnclosingSymbol on a class-level declaration may return the namespace)
        if (symbol is INamespaceSymbol ns && ProviderOf(symbol) is { } provider2)
        {
            var nsStr = ns.ToDisplayString();
            if (nsStr.StartsWith($"{Integrations}.{provider2}.Http", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a symbol is a home for the snapshot and snapshot client — a location where they are allowed to reference contracts and other provider internals that are forbidden downstream.</summary>
    /// <remarks>Identified by the containing type's name suffix (Snapshot/Client) rather than namespace, so folder reorganization under the provider does not disable role-specific rules. Used to allow a method inside the client to reference wire types and snapshots without triggering boundary violations.</remarks>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is inside a snapshot or client type.</returns>
    internal static bool IsClientHome(ISymbol symbol) =>
        EffectiveType(symbol) is { } type && IsClient(type);

    /// <summary>Whether a symbol is a snapshot — a type carrying what the provider reported, which dies at its projection (<c>aircraft-source</c> B-046).</summary>
    /// <remarks>Identified by the provider and a "Snapshot" name suffix rather than by namespace, so folder reorganization under the provider does not silently disable snapshot-related rules.</remarks>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a snapshot.</returns>
    internal static bool IsSnapshot(ISymbol symbol) =>
        ProviderOf(symbol) is not null && symbol.Name.EndsWith("Snapshot", StringComparison.Ordinal);

    /// <summary>Whether a symbol is a snapshot client — a type that fills a cache with snapshots from the provider.</summary>
    /// <remarks>Identified by the provider and a "Client" name suffix rather than by namespace, so folder reorganization under the provider does not silently disable client-related rules.</remarks>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a client.</returns>
    internal static bool IsClient(ISymbol symbol) =>
        ProviderOf(symbol) is not null && symbol.Name.EndsWith("Client", StringComparison.Ordinal);

    /// <summary>Whether a symbol is a keyed reactive cache, by the names DynamicData gives them. Matched by name rather than by package so the analyzer takes no dependency the application takes.</summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a cache.</returns>
    internal static bool IsCache(ISymbol symbol) =>
        symbol is INamedTypeSymbol { IsGenericType: true, Name: "SourceCache" or "ISourceCache" or "IObservableCache" };

    /// <summary>Whether a symbol is a class implementing one of a provider's API contracts.</summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a contract implementation.</returns>
    internal static bool IsContractImplementation(ISymbol symbol) =>
        symbol is INamedTypeSymbol { TypeKind: TypeKind.Class } type && ContractOf(type) is not null;

    /// <summary>The API contract a class implements, or <see langword="null"/> when it implements none.</summary>
    /// <param name="type">The type to read.</param>
    /// <returns>The contract interface.</returns>
    internal static INamedTypeSymbol? ContractOf(INamedTypeSymbol type)
    {
        foreach (var candidate in type.AllInterfaces)
        {
            if (IsContract(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Whether a symbol is a domain type — one that survives the provider being replaced.</summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol belongs to the domain model.</returns>
    internal static bool IsDomain(ISymbol symbol) => In(symbol, Model);

    /// <summary>Whether a symbol is a concrete per-type tracker source or the swapping decorator — a class in the tracking namespace, as opposed to the interface consumers depend on.</summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a concrete tracking type.</returns>
    internal static bool IsConcreteTracking(ISymbol symbol) =>
        In(symbol, Tracking) && symbol is INamedTypeSymbol { TypeKind: not TypeKind.Interface };

    /// <summary>Whether a symbol sits in the tracking layer — a projection, a source, or the decorator.</summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is tracking code.</returns>
    internal static bool IsTrackingLayer(ISymbol symbol) => In(symbol, Tracking);

    /// <summary>Whether a symbol is a per-type tracker source seam — the interface a consumer depends on, as opposed to the class behind it.</summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a seam interface.</returns>
    internal static bool IsPerTypeSeam(ISymbol symbol) =>
        In(symbol, Tracking) && symbol is INamedTypeSymbol { TypeKind: TypeKind.Interface };

    /// <summary>Whether a symbol is a view model.</summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol's namespace ends in <c>ViewModels</c> under a feature.</returns>
    internal static bool IsViewModel(ISymbol symbol) =>
        In(symbol, Features) && NamespaceOf(symbol).EndsWith(".ViewModels", StringComparison.Ordinal);

    /// <summary>Whether a symbol is downstream of <c>IFleetTracker</c> — a feature's own code, or the user interface host. A composition root is not downstream: see <see cref="IsCompositionRoot"/>.</summary>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is a consumer.</returns>
    internal static bool IsDownstream(ISymbol symbol) =>
        !IsCompositionRoot(symbol) && (In(symbol, Features) || In(symbol, Host));

    /// <summary>Whether a symbol sits in a composition root — any <c>Container</c> namespace.</summary>
    /// <remarks>Registration names concrete types on purpose; what it may name is B-008 — <c>TRN0015</c>.</remarks>
    /// <param name="symbol">The symbol to classify.</param>
    /// <returns><see langword="true"/> when the symbol is registration code.</returns>
    internal static bool IsCompositionRoot(ISymbol symbol) =>
        NamespaceOf(symbol).EndsWith("." + Registration, StringComparison.Ordinal);

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
    internal static bool IsTest(ISymbol symbol) => In(symbol, Tests);

    private static bool In(ISymbol symbol, string layer)
    {
        var containing = NamespaceOf(symbol);

        return containing == layer || containing.StartsWith(layer + ".", StringComparison.Ordinal);
    }

    /// <summary>Resolves the concrete type whose name and interfaces determine the symbol's role: the symbol itself if it is a type, or its containing type if it is a member.</summary>
    /// <remarks>Used to allow role predicates (client, snapshot, transport) to work on method/property symbols by examining their containing type.</remarks>
    /// <param name="symbol">The symbol to resolve.</param>
    /// <returns>The concrete type to examine, or <see langword="null"/> when no type is available.</returns>
    private static INamedTypeSymbol? EffectiveType(ISymbol symbol) =>
        symbol as INamedTypeSymbol ?? symbol.ContainingType;

    private static bool IsException(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == nameof(Exception))
            {
                return true;
            }
        }

        return false;
    }

    private static string NamespaceOf(ISymbol symbol) =>
        symbol.ContainingNamespace is { IsGlobalNamespace: false } containing
            ? containing.ToDisplayString()
            : string.Empty;
}
