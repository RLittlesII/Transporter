# Transporter.Analyzers

One Roslyn analyzer. It reports the structural and boundary claims of the
[aircraft-source specification](../Transporter/Integrations/OpenSky/.spec/README.md)
§ 3 as compiler diagnostics, at the line that violates them.

One analyzer class, not one per rule: eighteen classes would copy
[`Layers`](Layers.cs) eighteen times. The Feature that owns it is
[boundary-analyzer](../../features/boundary-analyzer/.spec/README.md), and the
decision to enforce the layers by analysis rather than by review is
[ADR-0006](../../.spec/adr/0006-an-analyzer-enforces-the-layer-boundaries.md).

## How it reaches every project

The root [`Directory.Build.props`](../../Directory.Build.props) adds this
project — and the fixes beside it — to every other project in the repository:

```xml
<ProjectReference Include="$(TransporterAnalyzerProject)"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false"/>
```

`OutputItemType="Analyzer"` with `ReferenceOutputAssembly="false"` is the whole
point: `csc` and the editor load the assembly, and the shipped application never
sees it (boundary-analyzer B-014). The item group is conditioned to exclude
`Transporter.Analyzers` and `Transporter.CodeFixes` themselves, so neither lands
in its own analyzer list or in the other's. The paths are anchored through
properties because MSBuild resolves a `ProjectReference` against the importing
project, not against the file that declares it.

No application project names a type from here. The test project is the only one
that references it as an assembly, and only in order to test it (B-015).

## Project settings, and why each is there

| Setting                                    | Why                                                                                |
| ------------------------------------------ | ---------------------------------------------------------------------------------- |
| `TargetFramework` = `netstandard2.0`       | Roslyn hosts analyzers as `netstandard2.0`; a `net10.0` one does not load at all   |
| `IsPackable` = `false`                     | It ships inside this repository's build, not as a package                          |
| `EnforceExtendedAnalyzerRules` = `true`    | Turns on the `RS` rules that keep an analyzer from doing what an analyzer must not |
| `Microsoft.CodeAnalysis.CSharp`            | `PrivateAssets="all"` — Roslyn is the host's, not ours to carry anywhere (B-015)   |
| `InternalsVisibleTo Transporter.UnitTests` | Tests reach internals through this rather than by widening the types               |
| `AnalyzerReleases.Unshipped.md`            | An `AdditionalFiles` item, which is what `RS2008` asks for                         |

## The files

| File                                                             | What it holds                                                                               |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| [`BoundaryAnalyzer.cs`](BoundaryAnalyzer.cs)                     | The one `DiagnosticAnalyzer`: the registrations, and the reporting for every rule           |
| [`Diagnostics.cs`](Diagnostics.cs)                               | The seventeen `TRN` descriptors, in one place so the conventions are checked over one array |
| [`Layers.cs`](Layers.cs)                                         | Which layer a symbol belongs to — the only thing that answers it                            |
| [`AnalyzerReleases.Unshipped.md`](AnalyzerReleases.Unshipped.md) | Every id ever handed out. An id is never renumbered and a retired one never reused (B-007)  |

## The rules

Seventeen diagnostics, every one in category `Transporter.Boundaries` at
`DiagnosticSeverity.Error` by default — the only way to weaken one is a severity
entry in [`.editorconfig`](../../.editorconfig) (boundary-analyzer B-006). Ids
run on their own sequence rather than tracking the claim numbers, for the reasons
in
[adr/0001](../../features/boundary-analyzer/.spec/adr/0001-trn-ids-on-their-own-sequence.md).

| Id        | Enforces                | What it reports                                                                                                      |
| --------- | ----------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `TRN0001` | `aircraft-source` B-004 | A wire type named from outside the integration that declares it                                                      |
| `TRN0002` | `aircraft-source` B-045 | The envelope or the row named by anything but the contract's implementation and the snapshot client                  |
| `TRN0003` | `aircraft-source` B-046 | A snapshot named downstream of the projection that consumes it                                                       |
| `TRN0004` | `aircraft-source` B-047 | A contract, client, cache, snapshot or concrete source named below `IFleetTracker`                                   |
| `TRN0005` | `aircraft-source` B-032 | A domain type named by the cache                                                                                     |
| `TRN0006` | `aircraft-source` B-041 | A strategy, client, cache or decorator named by a view model                                                         |
| `TRN0007` | `aircraft-source` B-044 | A bound collection mutated imperatively rather than through the pipeline                                             |
| `TRN0008` | `aircraft-source` B-002 | An envelope member that leaves the provider's positional shape                                                       |
| `TRN0009` | `aircraft-source` B-005 | A contract method that is not one per endpoint, does not return `Task<T>`, or does not take `CancellationToken` last |
| `TRN0010` | `aircraft-source` B-006 | A contract naming an observable, cache, changeset, bounding box, interval or credential                              |
| `TRN0011` | `aircraft-source` B-007 | A contract implementation that is not the one internal sealed explicit one per transport                             |
| `TRN0012` | `aircraft-source` B-014 | A snapshot member carrying a value derived rather than reported                                                      |
| `TRN0013` | `aircraft-source` B-037 | A per-type seam member describing where its data came from                                                           |
| `TRN0014` | `aircraft-source` B-048 | A version suffix on the contract, or an empty marker interface above it                                              |
| `TRN0015` | `aircraft-source` B-008 | An implementation type registered or resolved where the contract is what a consumer names                            |
| `TRN0016` | `aircraft-source` B-030 | A cache given a type, a projection or a policy of its own                                                            |
| `TRN0017` | `aircraft-source` B-031 | A cache registered at the wrong lifetime, or registered twice so two clients share one                               |

`TRN0018` is retired, with boundary-analyzer B-012 and `aircraft-source` B-010.
Its id is reserved and never reused.

> [`boundary-analyzer`](../../features/boundary-analyzer/.spec/README.md) § 7 is
> the only store for which diagnostic enforces which claim (B-019). The table
> above is orientation for a reader opening this folder; when a claim changes,
> § 7 is what is edited. A claim's text lives in `aircraft-source` § 3 and the
> test that proves it in that specification's § 9, so neither is copied here.

Three rules ship a code fix — `TRN0009`, `TRN0011` and `TRN0017`, the ones whose
compliant form is determined rather than a design decision. § 7 records, rule by
rule, why the other fourteen have none.

## How a symbol's layer is decided

[`Layers`](Layers.cs) reads the containing namespace, which is the folder
structure the `transporter-conventions` skill fixes. That is a deliberate choice
with a named hazard — a misfiled file silently changes which rules apply to it —
argued in
[adr/0002](../../features/boundary-analyzer/.spec/adr/0002-layers-are-identified-by-namespace.md).

| Layer               | How it is recognised                                                        |
| ------------------- | --------------------------------------------------------------------------- |
| Integration         | `Transporter.Integrations.<Provider>`; the provider is the next segment     |
| Wire surface        | `Transporter.Integrations.<Provider>.Contracts`                             |
| Transport           | `Transporter.Integrations.<Provider>.Http` and anything under it            |
| Domain model        | `Transporter.Model`                                                         |
| Tracking            | `Transporter.Tracking` — interfaces are seams, classes are concrete         |
| Feature code        | `Transporter.Features`; a view model is a `*.ViewModels` namespace under it |
| User interface host | `Gui`                                                                       |
| Composition root    | Any namespace whose last segment is `Container`                             |
| Tests               | `Transporter.UnitTests` — excluded from every rule, by design               |

Roles that survive a folder reorganisation are read from the type's name instead:
`*Snapshot`, `*Client`, `*Row` and `*Response`. A cache is matched by the names
DynamicData gives one — `SourceCache`, `ISourceCache`, `IObservableCache` — so
the analyzer takes no dependency the application takes.

`Layers.PublishedByTheSeam` walks `IFleetTracker` and everything its members
carry, transitively. A concrete tracking type reachable that way is published by
design and is not a source internal however concrete it is, so `TRN0004` and
`TRN0006` do not fire on it.

## How it runs

`Initialize` turns off generated-code analysis and turns on concurrent
execution, then reads the seam once per compilation in a
`RegisterCompilationStartAction` — what the seam publishes decides whether a
concrete tracking type counts as an internal at all, and that answer is the same
for every node in the compilation.

Four syntax-node actions, because the claims are about four different things:

| Action                      | Nodes                                           | Claims about          |
| --------------------------- | ----------------------------------------------- | --------------------- |
| `AnalyzeTypeMention`        | `IdentifierName`, `GenericName`                 | A name (B-009)        |
| `AnalyzeCollectionMutation` | `InvocationExpression`                          | A call (B-044)        |
| `AnalyzeTypeDeclaration`    | interface, class, record, record struct, struct | A declaration (B-010) |
| `AnalyzeCall`               | `InvocationExpression`                          | A call site (B-011)   |

Identifiers, not a symbol walk. That is what makes a reference inside a method
body visible, which is
[ADR-0006](../../.spec/adr/0006-an-analyzer-enforces-the-layer-boundaries.md)
§ Context's argument against the reflection test it replaced.

A registration or a resolution is matched by the method's name **and** its
receiver type — `IServiceCollection` for one, `IServiceProvider` or
`IKeyedServiceProvider` for the other. The name alone is not enough:
`new FlurlClientCache().Add(...)` is an `Add` and no registration at all.
A static member read off an implementation — `OpenSkyHttpApi.ClientName` in a
factory — names the type without making it resolvable, and reporting it was
`TRN0015`'s first false positive, so only a type argument, a `typeof` or a
construction counts as naming an implementation.

## Exemptions

Every rule is excluded inside a test: tests name internals by design, and the
claims are about production layers.

A composition root is exempt from the rules about naming internals — registration
names concrete types on purpose — but not from `TRN0015`, `TRN0016` or `TRN0017`,
which are the claims about what registration itself may do.

## Related

- [`src/Transporter.CodeFixes`](../Transporter.CodeFixes/) — the fixes, in a
  project of their own because a `CodeFixProvider` needs
  `Microsoft.CodeAnalysis.Workspaces` and an analyzer must not carry that into
  the compiler's load path. There is no project reference between the two; the
  ids are literals on both sides and a test holds the two lists in step.
- [`test/UnitTests/Analyzers`](../../test/UnitTests/Analyzers/) —
  `BoundaryAnalyzerTests`, `BoundaryCodeFixTests` and
  `BoundaryAnalyzerDescriptorTests`, with their sources in `BoundaryTestData`.
- [`.skills/analyzer-testing`](../../.skills/analyzer-testing/SKILL.md) — the
  harness, the assertions, and the traps each one was paid for.
- [`features/boundary-analyzer/.spec/README.md`](../../features/boundary-analyzer/.spec/README.md)
  — the Feature: § 7 for the mapping and the fixes, § 9 for traceability.
