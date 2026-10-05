using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Transponder.Analyzers;

/// <summary>The TRN descriptors, one per claim the analyzer enforces.</summary>
/// <remarks>
/// One place, so B-001 – B-008 are checked once over
/// <see cref="BoundaryAnalyzer.SupportedDiagnostics"/>. Ids run on their own sequence
/// (<c>adr/0001</c>); the claim in a title is a build message (B-003), not an index.
/// </remarks>
internal static class Diagnostics
{
    /// <summary>The category every boundary diagnostic is reported under.</summary>
    internal const string Category = "Transponder.Boundaries";

    /// <summary>The separator between a descriptor's claim reference and its summary.</summary>
    internal const string ClaimSeparator = " — ";

    /// <summary>A wire type named from outside the integration that declares it.</summary>
    internal static readonly DiagnosticDescriptor WireTypeOutsideIntegration = Rule(
        "TRN0001",
        "aircraft-source B-004",
        "the provider's wire types are not visible outside its integration",
        "'{0}' is part of the {1} wire surface, and '{2}' is outside that integration");

    /// <summary>The envelope or the row named by something other than the two types allowed to.</summary>
    internal static readonly DiagnosticDescriptor EnvelopeOrRowBeyondItsTwoHolders = Rule(
        "TRN0002",
        "aircraft-source B-045",
        "the envelope and the row are named only by the contract's implementation and the snapshot client",
        "'{0}' names '{1}', which only the contract's implementation and the snapshot client may name");

    /// <summary>A snapshot named downstream of the projection that consumes it.</summary>
    internal static readonly DiagnosticDescriptor SnapshotDownstreamOfTheProjection = Rule(
        "TRN0003",
        "aircraft-source B-046",
        "the snapshot dies at its projection",
        "'{0}' is downstream of the projection and names the snapshot '{1}'");

    /// <summary>Anything upstream of the tracker named from below it.</summary>
    internal static readonly DiagnosticDescriptor UpstreamTypeNamedBelowTheTracker = Rule(
        "TRN0004",
        "aircraft-source B-047",
        "nothing downstream of IFleetTracker names a contract, a client, a cache, a snapshot or a concrete source",
        "'{0}' is downstream of IFleetTracker and names '{1}'");

    /// <summary>A domain type named by the cache.</summary>
    internal static readonly DiagnosticDescriptor DomainTypeNamedByTheCache = Rule(
        "TRN0005",
        "aircraft-source B-032",
        "the cache holds snapshots and names no domain type",
        "the cache names the domain type '{0}'");

    /// <summary>A strategy, client, cache or decorator named by a view model.</summary>
    internal static readonly DiagnosticDescriptor SourceInternalsNamedByAViewModel = Rule(
        "TRN0006",
        "aircraft-source B-041",
        "a view model depends on IFleetTracker and names no strategy, client, cache or decorator",
        "the view model '{0}' names '{1}'");

    /// <summary>A bound collection mutated imperatively.</summary>
    internal static readonly DiagnosticDescriptor BoundCollectionMutatedImperatively = Rule(
        "TRN0007",
        "aircraft-source B-044",
        "every change to a bound collection arrives through the pipeline",
        "'{0}' changes a bound collection imperatively; the change belongs in the pipeline");

    /// <summary>An envelope member that names a per-aircraft type.</summary>
    internal static readonly DiagnosticDescriptor EnvelopeMemberLeavesThePositionalShape = Rule(
        "TRN0008",
        "aircraft-source B-002",
        "the envelope keeps the provider's positional shape",
        "the envelope member '{0}' names the per-aircraft type '{1}'");

    /// <summary>A contract method that is not one per endpoint, or does not take cancellation last.</summary>
    internal static readonly DiagnosticDescriptor ContractMethodIsNotOnePerEndpoint = Rule(
        "TRN0009",
        "aircraft-source B-005",
        "one contract method per endpoint, returning Task<T>, with CancellationToken last",
        "the contract method '{0}' {1}");

    /// <summary>A contract declaration naming something the contract may not name.</summary>
    internal static readonly DiagnosticDescriptor ContractNamesSomethingBeyondItsEndpoint = Rule(
        "TRN0010",
        "aircraft-source B-006",
        "the contract names no observable, cache, changeset, bounding box, interval or credential",
        "the contract names '{0}', which belongs above it rather than on it");

    /// <summary>A second implementation for one transport, or one that is not sealed, internal and explicit.</summary>
    internal static readonly DiagnosticDescriptor ContractImplementationIsNotTheOnePerTransport = Rule(
        "TRN0011",
        "aircraft-source B-007",
        "one internal sealed implementation per transport, implemented explicitly",
        "the contract implementation '{0}' {1}");

    /// <summary>A snapshot member carrying a value derived rather than reported.</summary>
    internal static readonly DiagnosticDescriptor SnapshotMemberIsDerived = Rule(
        "TRN0012",
        "aircraft-source B-014",
        "the snapshot carries what was reported and nothing derived from it",
        "the snapshot member '{0}' is derived rather than reported");

    /// <summary>A per-type seam widened with a member describing its source.</summary>
    internal static readonly DiagnosticDescriptor PerTypeSeamDescribesItsSource = Rule(
        "TRN0013",
        "aircraft-source B-037",
        "a per-type tracker source is not widened with source-describing members",
        "the seam member '{0}' describes where its data came from");

    /// <summary>A version suffix on the contract, or a marker interface above it.</summary>
    internal static readonly DiagnosticDescriptor ContractCarriesAVersion = Rule(
        "TRN0014",
        "aircraft-source B-048",
        "the contract carries no version suffix and no marker interface above it",
        "'{0}' {1} while the provider publishes no API version");

    /// <summary>An implementation type registered or resolved from outside the integration.</summary>
    internal static readonly DiagnosticDescriptor ImplementationTypeIsReachable = Rule(
        "TRN0015",
        "aircraft-source B-008",
        "a consumer names the contract and never selects between implementations",
        "'{0}' names the implementation type '{1}'; the contract is what a consumer resolves");

    /// <summary>A cache wrapped in a type of its own, or carrying a policy.</summary>
    internal static readonly DiagnosticDescriptor CacheIsMoreThanAKeyedStore = Rule(
        "TRN0016",
        "aircraft-source B-030",
        "the cache is a plain keyed store of snapshots",
        "'{0}' gives the cache {1}, which belongs to the client above it");

    /// <summary>A cache registered at the wrong lifetime, or shared between clients.</summary>
    internal static readonly DiagnosticDescriptor CacheLifetimeIsNotTheApplications = Rule(
        "TRN0017",
        "aircraft-source B-031",
        "one cache per client, at the application's lifetime",
        "the cache is registered {0}");

    /// <summary>Every descriptor the analyzer supports, in id order.</summary>
    internal static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
        WireTypeOutsideIntegration,
        EnvelopeOrRowBeyondItsTwoHolders,
        SnapshotDownstreamOfTheProjection,
        UpstreamTypeNamedBelowTheTracker,
        DomainTypeNamedByTheCache,
        SourceInternalsNamedByAViewModel,
        BoundCollectionMutatedImperatively,
        EnvelopeMemberLeavesThePositionalShape,
        ContractMethodIsNotOnePerEndpoint,
        ContractNamesSomethingBeyondItsEndpoint,
        ContractImplementationIsNotTheOnePerTransport,
        SnapshotMemberIsDerived,
        PerTypeSeamDescribesItsSource,
        ContractCarriesAVersion,
        ImplementationTypeIsReachable,
        CacheIsMoreThanAKeyedStore,
        CacheLifetimeIsNotTheApplications);

    private static DiagnosticDescriptor Rule(string id, string claim, string summary, string message) =>
        new(
            id,
            claim + ClaimSeparator + summary,
            message + " (" + claim + ")",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: claim + ClaimSeparator + summary
                         + ". The claim's text is in that specification's § 3; the diagnostic it is enforced by is in its § 7.");
}
