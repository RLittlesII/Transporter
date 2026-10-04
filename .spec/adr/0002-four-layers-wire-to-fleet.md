---
title: "ADR-0002: Four layers from the wire to the fleet"
description: "Separate OpenSky's API types, an internal snapshot DTO, a cache that stores and diffs snapshots, and a tracker that projects the cache into domain vehicles — two mapping layers, one responsibility each."
type: adr
---

# ADR-0002: Four layers from the wire to the fleet

**Status:** proposed

## Context

[`api-contract`](../../.skills/api-contract/SKILL.md) declares one seam —
`ITrackingSource.Snapshots`, of type
`IObservable<IReadOnlyCollection<TransportVehicle>>` — and says that
"everything else in the demo sits on one side of that line or the other."
Three facts have made that one line carry more than it can.

- **One word names three different things.** OpenSky's positional `states` row
  is a snapshot of a server record. The set of domain vehicles the source
  emits is a snapshot of a fleet. What the cache holds is a third thing again.
  Only the middle one has a type, and it borrowed the first one's name — so a
  reader of this repository cannot tell from a signature which layer they are
  in. `Snapshots` returning domain objects is the specific confusion: a
  snapshot is the thing you map *into* a domain object, not the domain object
  itself.
- **The cache has no address.**
  [`dynamic-data-pipeline`](../../.skills/dynamic-data-pipeline/SKILL.md) lists
  `EditDiff` as the first row of an operator table, which makes the collection
  every downstream feature depends on a *step in a chain* rather than a
  component with a name and an owner. [`README.md`](../../README.md) § "Core
  idea" makes `EditDiff` the headline the audience is there to see; a headline
  with no address is hard to point at on a slide and harder to copy.
- **Two kinds of source must reach the same collection.** A polled source
  emits whole sets, so something must diff them. A push source already knows
  what changed. [`hot-swap-source`](../../.skills/hot-swap-source/SKILL.md)
  requires the cache, the filters, the comparers, the groups and the bindings
  to survive a swap untouched, which forbids a cache that belongs to whichever
  source is live.

Two further constraints bound the answer. Staleness must derive from an
injected clock, and replay must age items from the recording rather than the
wall clock — yet nothing in the current seam carries *when* a set was
observed, so a consumer deciding whether a row is stale has to invent that
instant. And
[`transponder-domain-model`](../../.skills/transponder-domain-model/SKILL.md)
forbids DynamicData types on the model, so the cache can be neither the model
nor inside it.

One piece of the answer was already written down.
[`mapping`](../../.skills/mapping/SKILL.md) says "Two steps, each testable:
converter (array → named DTO), mapper (DTO → domain)" — the two mapping layers
below are that sentence taken seriously, and given each of its products a
name.

## Decision drivers

- One word must not name three layers.
- `EditDiff` must happen somewhere with a name, because it is the talk's
  headline.
- A component that stores and diffs must not also map; a component that maps
  must not also store.
- Both kinds of source must land in **one** cache, so a swap is a cache clear
  and never a rebuild.
- The instant a set was observed must travel with the set.
- The positional wire shape must die at the converter and be reachable from
  nowhere else.
- A source that makes no network call must satisfy the seam with nothing
  stubbed.

## Considered options

| Option                                                                      | Summary                                                                                                                      | Why not                                                                                                                                                                                                                                                                                                                                                                                      |
| --------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Today's single seam — the source emits domain objects, the cache is unnamed downstream | No new types at all. `EditDiff` stays row one of the operator table the talk walks through in order, and nothing in the skills has to change. | The collection every later feature depends on has no name and no owner, so "the cache" stays a phrase rather than a thing. `Snapshot` goes on naming both the provider's row and the domain set, which is the complaint that opened this record. No observed instant travels with the set, so staleness and replay each guess at it separately. And the diff compares domain objects carrying `Option<T>` rather than records. This is the status quo, and it is the option this record reverses. |
| The cache stores domain objects; map inside the client                       | One mapping site and one fewer type. The cache is still a named component, just holding `TransportVehicle`.                   | The diff then compares `TransportVehicle`, so every domain type needs value equality over its optional members — on a type whose shape is driven by what the grid and the detail pane need, not by what the differ needs. The cache's stored shape also changes whenever the domain does, for reasons that have nothing to do with caching.                                                      |
| The cache lives inside the source and emits `IChangeSet` directly            | Each source owns its cache and publishes changes. `EditDiff` becomes a private detail nobody has to be told about.            | Puts a DynamicData type on the interface every source implements, so a replay or simulated source must construct a cache just to say "here are four aircraft". Worse, each source brings its own cache, so swapping the source replaces the cache and everything bound to it — precisely what `hot-swap-source` forbids. And the audience never meets `EditDiff`, which is the one thing they came for. |
| The cache stores snapshots **and** projects domain objects itself            | Three types instead of four. The cache is the only thing downstream code talks to.                                           | Two responsibilities in one component, and a cache whose stored type and returned type differ for no reason the cache itself needs. Splitting them costs one interface and buys a store-and-diff component that can be tested with no projection and a projection that can be tested with no cache.                                                                                             |
| Carry the wire types to the cache and map on the way out                     | The converter's output feeds the cache directly; the positional shape is the only wire concept.                               | The cache would reference the positional row, so a second source means either a second cache or a union type at the cache's door. `transponder-domain-model` already forbids wire-shaped positional data past the client boundary, and `mapping` already fixes the converter-then-mapper order. Rejected on both.                                                                               |
| **Four layers, two mappings**                                               | **Chosen.** API types, an internal snapshot, a store-and-diff cache, and a projecting tracker.                               | —                                                                                                                                                                                                                                                                                                                                                                                            |

## Decision

Four components, one responsibility each:

1. **API types** — `StatesResponse` and the positional row it carries, mirroring
   what OpenSky actually sends. Read by `AircraftSnapshotConverter` against the
   index table in [`README.md`](../../README.md) § "Response shape". This is
   **mapping layer 1**, and it goes straight to the snapshot: there is no
   intermediate raw-row type, because a type whose members are
   `double?[0]`…`double?[17]` is less readable than the converter that replaces
   it. The converter is the only code that reads an element by index.
2. **The internal snapshot** — `AircraftSnapshot`, a named record: the server's
   values with names on them. It converts no unit, derives no value and
   interprets no field, carries no staleness flag or display label, is keyed on
   `icao24`, and has **value equality over every member**. That last property
   is what makes "did this aircraft change" a question the compiler answers.
3. **The cache** — `IVehicleCache`, a `SourceCache<AircraftSnapshot, string>`
   keyed on `icao24`. Applying a set runs the differ, and **that is its entire
   job**: it holds no domain type, constructs no domain object, and reads no
   clock. `EditDiff` lives here, at a boundary with a name, which is the
   address the headline was missing.
4. **The tracker** — `IFleetTracker`, wrapping the cache and projecting its
   snapshot changesets into
   `IObservable<IChangeSet<TransportVehicle, string>>`. This is **mapping
   layer 2** and the first point at which a domain object exists. Staleness
   derivation against an injected clock lives on this side of the line, not in
   the cache.

Between the converter and the cache sits the existing seam, `ITrackingSource`,
now emitting sets of snapshots together with **the instant the set was
observed** — the poll's own observation for a live source, the recorded
instant for a replay. "As of when" becomes data at the seam rather than a
question a consumer answers with an ambient clock. A recorded fixture is
literally a set of snapshots, with nothing to re-derive on replay.

Behavioral detail — index-by-index field reading, callsign trimming, squawk as
a string, absent versus unknown category, token refresh, retry-after handling
— is **not** in this record. Those are claims, in § 3 of
[`features/aircraft-source/.spec/README.md`](../../features/aircraft-source/.spec/README.md)
(B-001 – B-036).

## Consequences

- **Two types and two interfaces more than the status quo.**
  [`implementer`](../../.agents/implementer.md) allows an interface only at a
  real external boundary or where a second implementation exists, and
  `IVehicleCache` and `IFleetTracker` each have one implementation today. The
  justification is written down rather than assumed: the cache is the
  substitution point a tracker test needs, and the tracker is the substitution
  point a pipeline test needs. If that stops being true, this record is
  superseded, not amended.
- **The differ compares records, not domain objects.** A snapshot with value
  equality makes "unchanged" a compiler-checked fact. Had the cache stored
  `TransportVehicle`, the same question would depend on equality over
  `Option<T>` members of a type shaped by the UI's needs.
- **The cache always diffs, so a push source is not a special case.** One seam
  survives, which keeps `api-contract`'s "never add a second seam" rule
  verbatim and means a swap never bridges two source interfaces. The cost is
  real: a push feed must accumulate its own known set in order to emit one,
  which brushes `dynamic-data-pipeline`'s rule against a second collection of
  tracked items. That is the closing act's problem, recorded as § 11 Q5 of the
  aircraft-source specification and claimed neither way there.
- **Staleness and expiry move downstream of the cache.** A cache that reads no
  clock is testable with no scheduler at all, and replay then ages items
  exactly as live does — which is what makes the network contingency a real
  contingency instead of a different code path.
- **Six files are wrong the day this lands, and are corrected in the same
  change**: `api-contract`, `dynamic-data-pipeline`, `hot-swap-source`,
  `ais-stream`, `mapping` and `http-client`, plus `README.md` § "Demo
  resilience". A prescription that contradicts the specification it governs is
  worse than no prescription — the same reason
  [ADR-0001](0001-flurl-for-http.md) updated `README.md` so a reversed plan
  would not survive anywhere as the current one.
- **Two reversals inside those edits are worth naming**, because they overturn
  text rather than extend it: `dynamic-data-pipeline`'s "push sources skip only
  the first step" and `ais-stream`'s "this source does not use `EditDiff`; it
  writes `AddOrUpdate` / `Remove` to the cache directly". Under this record
  both sources emit a set and the cache diffs it.
- **`StateVectorConverter` is renamed `AircraftSnapshotConverter`**, so
  `http-client` changes with it. `StatesResponse` keeps its name: it is
  OpenSky's envelope and mirrors the provider.
- **The talk gains a slide**: four boxes, the two mappings labelled on the
  arrows into boxes two and four, and `EditDiff` labelled on the arrow into box
  three. That is the shape the complaint which opened this record was asking
  for.
- **Cost paid in indirection.** Following one aircraft from JSON to the grid
  now passes through four named types. For an audience who will copy the
  boundary, naming it is worth the extra hop; for someone skimming one file, it
  is three more files.
