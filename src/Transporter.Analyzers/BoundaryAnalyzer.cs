using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Transporter.Analyzers;

/// <summary>Reports the structural and boundary claims as compiler diagnostics, at the line that violates them.</summary>
/// <remarks>
/// One analyzer, not one per rule: eighteen classes would copy <see cref="Layers"/> eighteen times.
/// Claims are <c>aircraft-source</c> § 3; the mapping is <c>boundary-analyzer</c> § 7. One node
/// reports once — selected by what was named, then by who named it.
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

        // The seam is read once per compilation rather than per node: what it publishes decides
        // whether a concrete tracking type is an internal at all. RSA1007 reports every
        // registration with an empty symbol name, as RSA2011 did in Airframe#403.
#pragma warning disable RSA1007
        context.RegisterCompilationStartAction(static start =>
        {
            var published = Layers.PublishedByTheSeam(start.Compilation);

            // Three actions, because the claims are about three different things: a name (B-009), a
            // call (B-044) and a declaration (B-010).
            start.RegisterSyntaxNodeAction(context => AnalyzeTypeMention(context, published), SyntaxKind.IdentifierName, SyntaxKind.GenericName);
            start.RegisterSyntaxNodeAction(AnalyzeCollectionMutation, SyntaxKind.InvocationExpression);
            start.RegisterSyntaxNodeAction(
                AnalyzeTypeDeclaration,
                SyntaxKind.InterfaceDeclaration,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration,
                SyntaxKind.StructDeclaration);
            start.RegisterSyntaxNodeAction(AnalyzeCall, SyntaxKind.InvocationExpression);
        });
#pragma warning restore RSA1007
    }

    private static void AnalyzeTypeMention(SyntaxNodeAnalysisContext context, ImmutableHashSet<INamedTypeSymbol> published)
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
        else if (Layers.IsClient(named) || Layers.IsContract(named) || (Layers.IsConcreteTracking(named) && !published.Contains(named)))
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
            // B-004: outside the integration the row is not visible at all.
            Report(context, Diagnostics.WireTypeOutsideIntegration, named.Name, provider, Containing(enclosing));

            return;
        }

        // B-045: inside, only a class implementing the contract and the client. The contract
        // declares the envelope. Read from the contract rather than from the folder: a provider
        // reached over HTTP and the same provider replayed from a recording are two
        // implementations of one contract, and the claim names the class by what it implements
        // (replay-source B-022).
        if (Layers.IsContractHome(enclosing)
            || Layers.IsTransport(enclosing)
            || Layers.IsClientHome(enclosing)
            || Layers.IsWireSurface(enclosing))
        {
            return;
        }

        Report(context, Diagnostics.EnvelopeOrRowBeyondItsTwoHolders, Containing(enclosing), named.Name);
    }

    private static void ReportSnapshotMention(SyntaxNodeAnalysisContext context, INamedTypeSymbol named, ISymbol enclosing)
    {
        // Client, cache, projection, composition root. Everything else is past the projection.
        if (Layers.IsClientHome(enclosing) || Layers.IsTrackingLayer(enclosing) || Layers.IsCompositionRoot(enclosing))
        {
            return;
        }

        // A consumer breaks B-047, a claim about the consumer; anything else breaks B-046.
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
            // B-041.
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
        // B-032 holds wherever the cache is typed, composition root included.
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

        // The receiver's type, not the declaring one: Add and Clear come from Collection<T>.
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

        // B-044, on the member called rather than the statement.
        context.ReportDiagnostic(
            Diagnostic.Create(
                Diagnostics.BoundCollectionMutatedImperatively,
                member.Name.GetLocation(),
                Containing(enclosing)));
    }

    /// <remarks>
    /// B-011: a lifetime and an alias are arguments to a call, and the registered type's own
    /// declaration is legal in every case these rules report, so the diagnostic goes where the
    /// answer is. Every rule here is excluded inside a test, which was not true while TRN0018
    /// existed — its subject was a call only a test makes, and it is retired (B-012, Withdrawn).
    /// </remarks>
    private static void AnalyzeCall(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol called)
        {
            return;
        }

        var enclosing = context.SemanticModel.GetEnclosingSymbol(context.Node.SpanStart, context.CancellationToken);

        if (enclosing is null || Layers.IsTest(enclosing))
        {
            return;
        }

        // The name alone is not enough: `new FlurlClientCache().Add(...)` is an `Add` and no
        // registration at all. What makes a call a registration is what it is called on.
        if (Resolutions.Contains(called.Name) && Receives(called, "IServiceProvider", "IKeyedServiceProvider"))
        {
            ReportResolution(context, invocation, called, enclosing);

            return;
        }

        if (!Registrations.Contains(called.Name) || !Receives(called, "IServiceCollection"))
        {
            return;
        }

        ReportRegisteredImplementation(context, invocation, called, enclosing);
        ReportCacheShape(context, invocation, called, enclosing);
        ReportCacheLifetime(context, invocation, called);
    }

    private static void ReportResolution(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol called,
        ISymbol enclosing)
    {
        // B-008, first clause: resolving the implementation is what "not resolvable from outside"
        // forbids, and a composition root is no exception — it is the place that would do it.
        if (called.TypeArguments.FirstOrDefault(Layers.IsContractImplementation) is not { } implementation)
        {
            return;
        }

        ReportAt(
            context,
            Diagnostics.ImplementationTypeIsReachable,
            NameOf(invocation).GetLocation(),
            Containing(enclosing),
            implementation.Name);
    }

    private static void ReportRegisteredImplementation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol called,
        ISymbol enclosing)
    {
        var implementation = NamedImplementations(context, invocation, called).FirstOrDefault();

        if (implementation is null)
        {
            return;
        }

        // B-008, second clause: aliasing the contract to one implementation is the shape it asks
        // for. The service type comes first, whether as a type argument or as a typeof.
        var service = ServiceTypeOf(context, invocation, called);

        if (service is not null
            && Layers.IsContract(service)
            && SymbolEqualityComparer.Default.Equals(Layers.ContractOf(implementation), service)
            && !AliasedTwice(context, invocation, service))
        {
            return;
        }

        ReportAt(
            context,
            Diagnostics.ImplementationTypeIsReachable,
            NameOf(invocation).GetLocation(),
            Containing(enclosing),
            implementation.Name);
    }

    private static void ReportCacheShape(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol called,
        ISymbol enclosing)
    {
        var service = ServiceTypeOf(context, invocation, called);

        if (service is null)
        {
            return;
        }

        // B-030, clause by clause. A wrapper is read here rather than on its own declaration,
        // because the declaration of a class holding a cache is legal until it is the cache.
        // The snapshot client is the exception, and not a weakening: B-015 requires it to take the
        // cache by constructor, so a client holding one is the claim beside this one being obeyed.
        // What B-030 forbids is a type that *is* the cache, which the writer above it never is.
        if (!Layers.IsCache(service) && !Layers.IsClient(service) && WrappedCache(service) is not null)
        {
            ReportAt(
                context,
                Diagnostics.CacheIsMoreThanAKeyedStore,
                NameOf(invocation).GetLocation(),
                Containing(enclosing),
                "a type of its own");

            return;
        }

        if (!Layers.IsCache(service))
        {
            return;
        }

        if (service.TypeArguments.FirstOrDefault() is { } stored && !Layers.IsSnapshot(stored))
        {
            ReportAt(
                context,
                Diagnostics.CacheIsMoreThanAKeyedStore,
                NameOf(invocation).GetLocation(),
                Containing(enclosing),
                "a projection, by typing it to '" + stored.Name + "' rather than to a snapshot");

            return;
        }

        // A keyed store takes its key selector and nothing else: an expiry or a clock beside it is
        // a policy, and the policy is the client's.
        foreach (var creation in invocation.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not INamedTypeSymbol created
                || !Layers.IsCache(created)
                || creation.ArgumentList is null or { Arguments.Count: <= 1 })
            {
                continue;
            }

            ReportAt(
                context,
                Diagnostics.CacheIsMoreThanAKeyedStore,
                NameOf(invocation).GetLocation(),
                Containing(enclosing),
                "a policy of its own, beside the key selector");

            return;
        }
    }

    private static void ReportCacheLifetime(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol called)
    {
        if (ServiceTypeOf(context, invocation, called) is not { } service || !Layers.IsCache(service))
        {
            return;
        }

        // B-031. The application's lifetime, and one cache per client — a second registration of
        // the same cache is two clients sharing one.
        if (Lifetimes.TryGetValue(called.Name, out var lifetime))
        {
            ReportAt(
                context,
                Diagnostics.CacheLifetimeIsNotTheApplications,
                NameOf(invocation).GetLocation(),
                "as " + lifetime + " rather than with the application's lifetime");

            return;
        }

        if (RegisteredTwice(context, invocation, service))
        {
            ReportAt(
                context,
                Diagnostics.CacheLifetimeIsNotTheApplications,
                NameOf(invocation).GetLocation(),
                "more than once for one snapshot, so two clients share one cache");
        }
    }

    private static bool AliasedTwice(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, INamedTypeSymbol service) =>
        SiblingRegistrations(context, invocation, service).FirstOrDefault() is { } first
        && first != invocation;

    private static bool RegisteredTwice(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, INamedTypeSymbol service) =>
        SiblingRegistrations(context, invocation, service).Skip(1).Any()
        && SiblingRegistrations(context, invocation, service).First() != invocation;

    /// <remarks>One constructed chain is one method: that is where a composition root assembles it.</remarks>
    private static IEnumerable<InvocationExpressionSyntax> SiblingRegistrations(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol service)
    {
        if (invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } chain)
        {
            return [];
        }

        return chain.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(sibling =>
                context.SemanticModel.GetSymbolInfo(sibling, context.CancellationToken).Symbol is IMethodSymbol called
                && Registrations.Contains(called.Name)
                && SymbolEqualityComparer.Default.Equals(ServiceTypeOf(context, sibling, called), service));
    }

    private static bool Receives(IMethodSymbol called, params string[] names) =>
        called.ReceiverType is INamedTypeSymbol receiver && names.Contains(receiver.Name);

    /// <remarks>
    /// Registered, not merely mentioned: a type argument, a <c>typeof</c>, or a construction. A
    /// static member read off the implementation — <c>OpenSkyHttpApi.ClientName</c> in a factory —
    /// names the type without making it resolvable, and reporting it was this rule's first
    /// false positive.
    /// </remarks>
    private static IEnumerable<INamedTypeSymbol> NamedImplementations(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol called) =>
        called.TypeArguments
            .Concat(invocation.DescendantNodes()
                .Where(static node => node is TypeOfExpressionSyntax or ObjectCreationExpressionSyntax)
                .Select(node => context.SemanticModel.GetTypeInfo(
                        node is TypeOfExpressionSyntax typed ? typed.Type : node,
                        context.CancellationToken)
                    .Type)
                .Where(static type => type is not null)
                .Select(static type => type!))
            .OfType<INamedTypeSymbol>()
            .Where(Layers.IsContractImplementation);

    /// <remarks>The service a registration exposes: its first type argument, or the first <c>typeof</c> it names.</remarks>
    private static INamedTypeSymbol? ServiceTypeOf(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol called)
    {
        if (called.TypeArguments.FirstOrDefault() is INamedTypeSymbol argument)
        {
            return argument;
        }

        foreach (var typed in invocation.DescendantNodes().OfType<TypeOfExpressionSyntax>())
        {
            if (context.SemanticModel.GetTypeInfo(typed.Type, context.CancellationToken).Type is INamedTypeSymbol named)
            {
                return named;
            }
        }

        // A factory with no type argument registers what it returns.
        foreach (var creation in invocation.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is INamedTypeSymbol created)
            {
                return created;
            }
        }

        return null;
    }

    private static ITypeSymbol? WrappedCache(INamedTypeSymbol type) =>
        type.GetMembers()
            .Select(static member => member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null,
            })
            .FirstOrDefault(static held => held is not null && Layers.IsCache(held));

    private static SyntaxNode NameOf(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name,
            _ => invocation.Expression,
        };

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

    /// <remarks>
    /// B-010: a claim about a declaration's shape is read on that declaration and reported on it,
    /// not at a call site that uses it. A claim with an "and" in it gets a case per clause — three
    /// of these messages take a clause phrase for exactly that reason, and a rule satisfying three
    /// clauses of four must not leave the fourth unreported.
    /// </remarks>
    private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not TypeDeclarationSyntax declaration)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol declared
            || Layers.IsTest(declared))
        {
            return;
        }

        if (Layers.IsEnvelope(declared))
        {
            ReportEnvelopeMembers(context, declaration);
        }
        else if (Layers.IsContract(declared))
        {
            ReportContractShape(context, declaration, declared);
        }
        else if (Layers.IsSnapshot(declared))
        {
            ReportDerivedSnapshotMembers(context, declaration);
        }
        else if (Layers.IsPerTypeSeam(declared))
        {
            ReportSeamMembers(context, declaration);
        }

        ReportImplementationShape(context, declaration, declared);
    }

    private static void ReportEnvelopeMembers(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax declaration)
    {
        foreach (var member in declaration.Members)
        {
            if (DeclaredTypeSyntax(member) is not { } syntax)
            {
                continue;
            }

            if (context.SemanticModel.GetTypeInfo(syntax, context.CancellationToken).Type is not { } type)
            {
                continue;
            }

            // B-002: the positional row is the shape the provider sent. Anything else with names
            // on it is a per-aircraft type, and reading the row is the client's job, not here.
            if (NamedPerAircraftType(ElementTypeOf(type)) is not { } named)
            {
                continue;
            }

            ReportAt(
                context,
                Diagnostics.EnvelopeMemberLeavesThePositionalShape,
                LocationOf(member),
                NameOf(member),
                named.Name);
        }
    }

    private static void ReportContractShape(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax declaration,
        INamedTypeSymbol declared)
    {
        // B-048, first clause: a suffix the provider has not earned.
        if (VersionSuffixOf(declared.Name) is { } suffix)
        {
            ReportAt(
                context,
                Diagnostics.ContractCarriesAVersion,
                declaration.Identifier.GetLocation(),
                declared.Name,
                "carries the version suffix '" + suffix + "'");
        }

        // B-048, second clause: a marker is an interface with nothing on it, which is the whole
        // point of one. An interface above the contract that declares members is a different
        // design, and a different conversation.
        foreach (var above in declaration.BaseList?.Types ?? default)
        {
            if (context.SemanticModel.GetSymbolInfo(above.Type, context.CancellationToken).Symbol
                is not INamedTypeSymbol { TypeKind: TypeKind.Interface } marker
                || !marker.GetMembers().IsEmpty)
            {
                continue;
            }

            ReportAt(
                context,
                Diagnostics.ContractCarriesAVersion,
                above.Type.GetLocation(),
                declared.Name,
                "has the marker interface '" + marker.Name + "' above it");
        }

        foreach (var member in declaration.Members)
        {
            ReportContractMemberNames(context, member);

            if (member is MethodDeclarationSyntax method
                && context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is { } declaredMethod)
            {
                ReportContractMethodShape(context, method, declared, declaredMethod);
            }
        }
    }

    private static void ReportContractMemberNames(SyntaxNodeAnalysisContext context, MemberDeclarationSyntax member)
    {
        foreach (var syntax in member.DescendantNodesAndSelf().OfType<TypeSyntax>())
        {
            if (context.SemanticModel.GetTypeInfo(syntax, context.CancellationToken).Type is not { } type)
            {
                continue;
            }

            // B-006. The contract is the shape of one endpoint; a stream, a store, a box, an
            // interval and a credential all belong to something that calls it.
            if (ForbiddenOnAContract(type) is not { } forbidden)
            {
                continue;
            }

            ReportAt(context, Diagnostics.ContractNamesSomethingBeyondItsEndpoint, LocationOf(member), forbidden.Name);

            return;
        }
    }

    private static void ReportContractMethodShape(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax syntax,
        INamedTypeSymbol declared,
        IMethodSymbol method)
    {
        var location = syntax.Identifier.GetLocation();

        // B-005, clause by clause. Each is its own report, so a method breaking two says so twice
        // rather than being fixed once and still wrong.
        if (declared.GetMembers(method.Name).Length > 1)
        {
            ReportAt(
                context,
                Diagnostics.ContractMethodIsNotOnePerEndpoint,
                location,
                method.Name,
                "shares its name with another declaration, so one endpoint has more than one method");
        }

        if (method.ReturnType is not INamedTypeSymbol { Name: "Task", IsGenericType: true })
        {
            ReportAt(context, Diagnostics.ContractMethodIsNotOnePerEndpoint, location, method.Name, "does not return Task<T>");
        }

        var token = IndexOfCancellationToken(method);

        if (token < 0)
        {
            ReportAt(context, Diagnostics.ContractMethodIsNotOnePerEndpoint, location, method.Name, "takes no CancellationToken");
        }
        else if (token != method.Parameters.Length - 1)
        {
            ReportAt(
                context,
                Diagnostics.ContractMethodIsNotOnePerEndpoint,
                location,
                method.Name,
                "does not take its CancellationToken last");
        }
    }

    private static void ReportImplementationShape(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax declaration,
        INamedTypeSymbol declared)
    {
        if (declared.TypeKind != TypeKind.Class
            || declared.AllInterfaces.FirstOrDefault(Layers.IsContract) is not { } contract)
        {
            return;
        }

        var location = declaration.Identifier.GetLocation();

        // B-007, clause by clause.
        if (declared.DeclaredAccessibility != Accessibility.Internal)
        {
            ReportAt(context, Diagnostics.ContractImplementationIsNotTheOnePerTransport, location, declared.Name, "is not internal");
        }

        if (!declared.IsSealed)
        {
            ReportAt(context, Diagnostics.ContractImplementationIsNotTheOnePerTransport, location, declared.Name, "is not sealed");
        }

        // One per transport, and a transport is a namespace (adr/0002). The first declaration in
        // metadata order keeps the slot, so the report lands on the one that arrived second.
        var siblings = declared.ContainingNamespace
            .GetTypeMembers()
            .Where(type => type.TypeKind == TypeKind.Class && type.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default))
            .ToImmutableArray();

        if (siblings.Length > 1 && !SymbolEqualityComparer.Default.Equals(siblings[0], declared))
        {
            ReportAt(
                context,
                Diagnostics.ContractImplementationIsNotTheOnePerTransport,
                location,
                declared.Name,
                "is a second implementation of '" + contract.Name + "' for this transport");
        }

        foreach (var endpoint in contract.GetMembers().OfType<IMethodSymbol>())
        {
            if (declared.FindImplementationForInterfaceMember(endpoint) is not IMethodSymbol implementation
                || !SymbolEqualityComparer.Default.Equals(implementation.ContainingType, declared)
                || !implementation.ExplicitInterfaceImplementations.IsEmpty)
            {
                continue;
            }

            ReportAt(
                context,
                Diagnostics.ContractImplementationIsNotTheOnePerTransport,
                DeclarationOf(declaration, implementation, context) ?? location,
                declared.Name,
                implementation.DeclaredAccessibility == Accessibility.Public
                    ? "declares '" + implementation.Name + "' as a public method rather than an explicit implementation"
                    : "implements '" + implementation.Name + "' implicitly rather than explicitly");
        }
    }

    private static void ReportDerivedSnapshotMembers(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax declaration)
    {
        foreach (var member in declaration.Members)
        {
            if (member is not PropertyDeclarationSyntax property)
            {
                continue;
            }

            // B-014. A computed getter is derived by construction; the kinds the claim names are
            // reported by name as well, because storing one does not make it reported.
            if (!IsComputed(property) && !Describes(property.Identifier.ValueText, Derived))
            {
                continue;
            }

            ReportAt(context, Diagnostics.SnapshotMemberIsDerived, property.Identifier.GetLocation(), property.Identifier.ValueText);
        }
    }

    private static void ReportSeamMembers(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax declaration)
    {
        foreach (var member in declaration.Members)
        {
            // B-037, second clause. What strategies share is the changeset; where it came from is
            // the one thing a consumer of the seam must not be able to ask.
            if (!Describes(NameOf(member), SourceDescribing))
            {
                continue;
            }

            ReportAt(context, Diagnostics.PerTypeSeamDescribesItsSource, LocationOf(member), NameOf(member));
        }
    }

    private static ITypeSymbol? NamedPerAircraftType(ITypeSymbol type) =>
        (Layers.IsWireType(type) && !Layers.IsPositionalRow(type)) || Layers.IsSnapshot(type) || Layers.IsDomain(type)
            ? type
            : null;

    /// <remarks>
    /// The type itself, unwrapped from nothing: the caller walks every type named in the
    /// declaration, so <c>Task&lt;IObservable&lt;T&gt;&gt;</c> arrives here three times and the
    /// forbidden one is seen on its own turn rather than unwrapped past.
    /// </remarks>
    private static ITypeSymbol? ForbiddenOnAContract(ITypeSymbol element)
    {
        if (Layers.IsCache(element))
        {
            return element;
        }

        return element.Name switch
        {
            "IObservable" or "IObservableList" or "IChangeSet" or "ChangeSet" or "TimeSpan" => element,
            "CancellationToken" => null,
            var name when name.EndsWith("BoundingBox", StringComparison.Ordinal) => element,
            var name when name.IndexOf("Credential", StringComparison.Ordinal) >= 0 => element,
            var name when name.EndsWith("Token", StringComparison.Ordinal) => element,
            _ => null,
        };
    }

    private static ITypeSymbol ElementTypeOf(ITypeSymbol type) =>
        type switch
        {
            IArrayTypeSymbol array => ElementTypeOf(array.ElementType),
            INamedTypeSymbol { IsGenericType: true } generic when generic.TypeArguments.Length == 1 && !Layers.IsCache(generic) =>
                ElementTypeOf(generic.TypeArguments[0]),
            _ => type,
        };

    private static string? VersionSuffixOf(string name)
    {
        var digits = name.Length;

        while (digits > 0 && char.IsDigit(name[digits - 1]))
        {
            digits--;
        }

        return digits > 1 && digits < name.Length && name[digits - 1] == 'V' ? name.Substring(digits - 1) : null;
    }

    private static int IndexOfCancellationToken(IMethodSymbol method)
    {
        for (var index = 0; index < method.Parameters.Length; index++)
        {
            if (method.Parameters[index].Type.Name == "CancellationToken")
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsComputed(PropertyDeclarationSyntax property) =>
        property.ExpressionBody is not null
        || property.AccessorList?.Accessors.Any(static accessor =>
            accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
            && (accessor.Body is not null || accessor.ExpressionBody is not null)) == true;

    private static bool Describes(string name, ImmutableArray<string> words) =>
        words.Any(word => name.IndexOf(word, StringComparison.Ordinal) >= 0);

    private static TypeSyntax? DeclaredTypeSyntax(MemberDeclarationSyntax member) =>
        member switch
        {
            PropertyDeclarationSyntax property => property.Type,
            FieldDeclarationSyntax field => field.Declaration.Type,
            _ => null,
        };

    private static Location? DeclarationOf(TypeDeclarationSyntax declaration, ISymbol symbol, SyntaxNodeAnalysisContext context)
    {
        foreach (var member in declaration.Members)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken),
                    symbol))
            {
                return LocationOf(member);
            }
        }

        return null;
    }

    private static Location LocationOf(MemberDeclarationSyntax member) =>
        member switch
        {
            PropertyDeclarationSyntax property => property.Identifier.GetLocation(),
            MethodDeclarationSyntax method => method.Identifier.GetLocation(),
            EventDeclarationSyntax @event => @event.Identifier.GetLocation(),
            IndexerDeclarationSyntax indexer => indexer.ThisKeyword.GetLocation(),
            FieldDeclarationSyntax field when field.Declaration.Variables.Count > 0 =>
                field.Declaration.Variables[0].Identifier.GetLocation(),
            _ => member.GetLocation(),
        };

    private static string NameOf(MemberDeclarationSyntax member) =>
        member switch
        {
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            EventDeclarationSyntax @event => @event.Identifier.ValueText,
            IndexerDeclarationSyntax => "this[]",
            FieldDeclarationSyntax field when field.Declaration.Variables.Count > 0 =>
                field.Declaration.Variables[0].Identifier.ValueText,
            _ => string.Empty,
        };

    /// <summary>Reports at a location of the rule's choosing, which a declaration rule needs — B-010.</summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="descriptor">The rule reporting.</param>
    /// <param name="location">Where the violation is written.</param>
    /// <param name="arguments">The message arguments.</param>
    private static void ReportAt(
        SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor descriptor,
        Location location,
        params object[] arguments) =>
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, arguments));

    /// <summary>Reports at the node, naming the symbol — B-004.</summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="descriptor">The rule reporting.</param>
    /// <param name="arguments">The message arguments.</param>
    private static void Report(SyntaxNodeAnalysisContext context, DiagnosticDescriptor descriptor, params object[] arguments) =>
        context.ReportDiagnostic(Diagnostic.Create(descriptor, context.Node.GetLocation(), arguments));

    private static string Containing(ISymbol symbol) =>
        symbol.ContainingType is { } type ? type.ToDisplayString() : symbol.ToDisplayString();

    /// <remarks>
    /// The spellings this repository uses, in <c>src/Gui/Container</c> and
    /// <c>Integrations/OpenSky/Container</c>. A registration naming an implementation in any other
    /// shape falls through to a report rather than to silence, which is what `0024` asks for: a
    /// rule keyed to one spelling misses the others while the § 9 row flips to `Verified` anyway.
    /// </remarks>
    private static readonly ImmutableHashSet<string> Registrations = ImmutableHashSet.Create(
        "Add",
        "AddSingleton",
        "AddScoped",
        "AddTransient",
        "AddKeyedSingleton",
        "AddKeyedScoped",
        "AddKeyedTransient",
        "TryAdd",
        "TryAddSingleton",
        "TryAddScoped",
        "TryAddTransient",
        "Decorate");

    private static readonly ImmutableHashSet<string> Resolutions = ImmutableHashSet.Create(
        "GetService",
        "GetRequiredService",
        "GetKeyedService",
        "GetRequiredKeyedService",
        "Resolve");

    private static readonly ImmutableDictionary<string, string> Lifetimes = ImmutableDictionary
        .CreateRange(
        [
            new KeyValuePair<string, string>("AddScoped", "scoped"),
            new KeyValuePair<string, string>("AddTransient", "transient"),
            new KeyValuePair<string, string>("AddKeyedScoped", "scoped"),
            new KeyValuePair<string, string>("AddKeyedTransient", "transient"),
            new KeyValuePair<string, string>("TryAddScoped", "scoped"),
            new KeyValuePair<string, string>("TryAddTransient", "transient"),
        ]);

    private static readonly ImmutableArray<string> Derived = ImmutableArray.Create(
        "Stale",
        "Label",
        "Display",
        "Caption",
        "Group",
        "Formatted");

    private static readonly ImmutableArray<string> SourceDescribing = ImmutableArray.Create(
        "Source",
        "Provider",
        "Origin",
        "Feed",
        "Transport",
        "Endpoint");

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
