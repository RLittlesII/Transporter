---
title: "ADR-0002: Contract, client, cache, strategy, decorator, tracker"
description: "Put a typed API contract at the provider, caching in the client, domain projection in a per-type strategy behind ITrackerSource, the source swap in a decorator, and the pipeline in IFleetTracker."
type: adr
---

# ADR-0002: Contract, client, cache, strategy, decorator, tracker

**Status:** proposed

## Context

[`api-contract`](../../.skills/api-contract/SKILL.md) declared one seam —
`ITrackingSource.Snapshots`, of type
`IObservable<IReadOnlyCollection<TransportVehicle>>` — and said that
"everything else in the demo sits on one side of that line or the other." One
interface cannot carry that much, and the specific failures are three.

- **One word names three things.** OpenSky's positional `states` row is a
  snapshot of a server record. The set of domain vehicles a source emits is a
  snapshot of a fleet. What a cache holds is a third thing again. Only the
  middle one had a type, and it took the first one's name, so `Snapshots`
  returning domain objects reads backwards: a snapshot is the record you map
  _into_ a domain object.
- **The thing that varies had no owner.** What actually differs between an
  aircraft feed and a vessel feed is how a provider's record becomes a
  `TransportVehicle`. With the seam at the wire boundary, that projection was a
  step rather than a component — so it lands wherever something happens to hold
  both types, and a second source means editing that something.
- **There was nothing to version and nothing to fake.** A loose DTO with a
  `JsonConverter` gives no typed surface to version when a provider changes,
  and nothing a test can substitute except the HTTP transport. ADR-0001 already
  recorded why that last point bites: `HttpTest` intercepts through the logical
  asynchronous call context, so it does not follow a message into an actor on
  its own dispatcher.

Two further constraints bound the answer.
[`domain-model`](../../.skills/domain-model/SKILL.md)
forbids DynamicData types on the model, so neither a cache nor a projection can
live inside it. And
[`hot-swap-source`](../../.skills/hot-swap-source/SKILL.md) requires the
collection, the filters, the comparers, the groups and the bindings to survive
a swap untouched, which forbids any of them belonging to whichever source is
live.

One piece of the answer was already written down.
[`mapping`](../../.skills/mapping/SKILL.md) says "Two steps, each testable:
converter (array → named DTO), mapper (DTO → domain)". This record takes that
seriously and gives each step an owner.

## Decision drivers

- One word must not name three layers.
- The thing that varies per source — the domain projection — must be the
  Strategy, and the strategy seam must sit at the domain boundary.
- A component that stores must not also diff; a component that diffs must not
  also map.
- The provider's surface must be typed, versionable, and fakeable without HTTP.
- `EditDiff` must happen somewhere with a name, because it is the talk's
  headline.
- The collection and everything bound to it must outlive every source.
- The positional wire shape must be unreachable outside the integration code.

## Considered options

| Option                                                                 | Summary                                                                                                                   | Why not                                                                                                                                                                                                                                                                                                                                                                                                                           |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Today's single seam — a source emitting domain objects, no named cache | No new types. `EditDiff` stays row one of an operator table and nothing in the skills changes.                            | The collection every later feature depends on has no owner; `Snapshot` names both the provider's row and the domain set; the projection is a step rather than a component; and there is no typed provider surface to version or fake. The status quo, and what this record reverses.                                                                                                                                              |
| Loose DTOs plus a converter, no typed contract                         | A named DTO and a `JsonConverter`, with no interface at the provider.                                                     | Nothing to version when OpenSky changes, and nothing a test can replace but the HTTP transport — which ADR-0001's `HttpTest` constraint makes the least useful seam to own. A hand-written fake of a typed contract gives every layer above it a test with no HTTP at all.                                                                                                                                                        |
| A cache that stores **and** diffs                                      | One component owning the collection and the differential update.                                                          | Forces every source through one write strategy, so a push feed that already knows its deltas must assemble a full set to use them. Two responsibilities in one component, and the cache's behavior becomes a policy callers have to know about.                                                                                                                                                                                   |
| The strategy seam at the wire boundary, carrying snapshots             | What `ITrackingSource` did: every source emits snapshots and something downstream projects them.                          | Puts the seam where sources are _similar_ rather than where they _differ_. Both feeds produce records; what differs is the projection. A seam at the wire boundary therefore leaves the varying part unowned, which is the second failure above.                                                                                                                                                                                  |
| A cache per app, holding a common snapshot base                        | One cache, one stream, with `AircraftSnapshot` and `VesselSnapshot` deriving from a shared base.                          | Adds a second inheritance hierarchy beside `TransportVehicle`'s, mirroring it for no new reason, and the base can only carry what both feeds happen to share. Per-client caches cost nothing downstream, because the invariant that matters is one _domain_ collection.                                                                                                                                                           |
| `IStrategyResolver` plus `IStrategy`                                   | The conventional shape: a resolver knows the strategies and callers ask it which to use.                                  | The resolver is a second place the strategy set is known, and every caller has to ask. Registering every strategy as `ITrackerSource` and putting the selection in a decorator makes the swap invisible to callers — nobody asks anything.                                                                                                                                                                                        |
| Replay as a strategy with its own client and cache                     | Replay reads recorded envelopes through a replay client, parallel to the snapshot client.                                 | Needs B-045 widened — the envelope and the positional row are restricted to the contract's implementation and the snapshot client — and leaves a second positional row reader to keep correct against the same index table. Worse, it is a second place B-003 has to be honoured, and B-003 is what makes staleness behave on replay exactly as it does live. A boundary rule honoured in two implementations is one that drifts. |
| Replay at the contract, selection below the seam                       | A replay contract, but the source choice made where the contract is resolved.                                             | Keeps B-045 and B-007 untouched and still costs nothing in duplication, but replay stops being an `ITrackerSource` and therefore stops being a swap target — contradicting replay-source B-015 and `hot-swap-source`, which both require replay to be selected by the same mechanism as the live swap.                                                                                                                            |
| **Replay at the contract, selection at the seam**                      | **Chosen.** A second contract implementation; the live chain above it unchanged; the chain registers as `ITrackerSource`. | —                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| **Contract → client → cache → strategy → decorator → tracker**         | **Chosen.**                                                                                                               | —                                                                                                                                                                                                                                                                                                                                                                                                                                 |

## Decision

Each component has one responsibility.

1. **API types** — a response envelope mirroring what OpenSky sends: its
   reported time and its `states` rows, named as the provider names them. The
   rows stay positional, and the row type is unreachable outside the
   integration code. The envelope's reported time is the observed instant for
   everything downstream, so nothing reads an ambient clock to answer "as of
   when".
2. **The API contract** — a typed interface mirroring the provider's API: one
   method per endpoint, `Task<T>`, `CancellationToken` last, and no
   `IObservable`, cache, changeset, bounding box, interval or credential
   anywhere on it. Exactly one `internal sealed` class implements it **per
   transport**, with explicit interface implementation on every method and
   nothing public; DI aliases the contract to one implementation per
   constructed chain, and no implementing class is resolvable from outside. Its
   test double is a hand-written fake that throws naming the unset response.

    **Per transport, because this is where replay substitutes.** One
    implementation reaches OpenSky over HTTP; another reads a recording. The
    snapshot client, its cache and its projection are constructed over whichever
    one, unchanged, and that whole chain registers as an `ITrackerSource` — so
    replay is a swap target at the seam (B-038 – B-040) while standing in below
    the client. A recording is the provider's own envelope written verbatim
    ([ADR-0004](0004-ndjson-recording-format.md)), which is what makes this
    possible: the recorded envelope carries the provider's reported time, so the
    observed instant and staleness-on-replay come free, with no second
    implementation to honour them in. This is a deliberate departure from the
    versioned-contract pattern's "exactly one production class" — see the
    consequences.

    **No version suffix, and no marker interface above it.** The pattern's rule
    is that a suffix matches the _provider's_ major version, and OpenSky
    publishes none — there is no `/v1/` in its path — so an invented `V1` would
    be exactly the internal version number the rule forbids. A marker exists to
    make domain code version-agnostic; with no version to be agnostic about, a
    split with one member on each side buys nothing and costs a cast at every
    call site. The versioned layer is introduced the day OpenSky declares a
    version, and a provider that _does_ version gets the full pattern from its
    first line of code.

    It lives under `src/Transponder/Integrations/OpenSky/`, split
    `Contracts/`, `Http/` and `Container/` — **not** under `Features/`. A
    provider's code belongs to the provider, not to one feature, and two
    features can want the same provider. `AGENTS.md` governs feature layout and
    was silent on integrations; it gains that line in this change.

    **This layer is conditional.** One `Task<T>` per endpoint only describes a
    request/response provider, so a push provider has no contract at all and
    none is invented for it: a socket's subscribe frame is connection state, not
    a call with a return value. The vessel strategy is therefore shorter than
    this one, and that is fine — see the consequences.

3. **The snapshot** — a named record: the server's values with names on them.
   Value equality over every member, keyed on `icao24`, converting and deriving
   nothing, and carrying no staleness flag or display label.
4. **The snapshot client** — takes the contract and its cache by constructor
   and constructs neither. Its first act is reading the positional rows into
   snapshots. It writes each fetched set to its cache as one differential
   update over the whole set, and exposes the resulting snapshot changeset
   stream. Bounding box, interval, token refresh, the credit header and `429`
   handling are all its concerns and appear nowhere else.
5. **The cache** — a plain keyed store of snapshots. No diff policy of its own,
   no projection, no clock. One per client, typed to that client's snapshot,
   with the application's lifetime rather than the client's. **The writer owns
   the write**: a polling client applies a differential update over a whole
   set; a push client adds and removes what it was told about. The cache cannot
   tell the difference and has no opinion either way.
6. **The tracker source strategy** — `ITrackerSource` declares the
   `TransportVehicle` changeset stream, so every strategy is substitutable
   through it without a cast. `IAircraftTrackerSource` and
   `IVesselTrackerSource` adhere to it, and each owns its own Mapperly
   projection from its snapshot to its domain subclass. **This is the first
   place a domain object exists, and the only place one is built.** Projection
   is the thing that varies per source, which is what makes this the Strategy.
7. **The swap decorator** — a decorator registered as `ITrackerSource`,
   selecting the live strategy at runtime. There is no resolver type and no
   caller asks which strategy to use; a consumer resolves the seam and gets the
   decorator. Swapping stops the outgoing source, so a swapped-out poller stops
   spending credits.
8. **`IFleetTracker`** — wraps `ITrackerSource` and is what view models depend
   on. It owns the collection pipeline — filtering, sorting, grouping,
   aggregates, property-change refresh, expiry and binding — constructed once
   and never rebuilt because the live source changed. It owns the injected
   clock, so staleness derives from last contact in exactly one place.

Two mappings, two owners, two mechanisms: **rows → snapshot** is hand-written
in the client, because positional arrays are not something Mapperly can map;
**snapshot → domain** is Mapperly in the strategy.

Behavioral detail — index-by-index reading, callsign trimming, squawk as a
string, absent versus unknown category, token refresh, retry-after handling —
is **not** in this record. Those are claims, in § 3 of
[`features/aircraft-source/.spec/README.md`](../../features/aircraft-source/.spec/README.md)
(B-001 – B-052).

## Consequences

- **Seven named things where there was one interface.** The cost is paid in
  indirection: following one aircraft from JSON to the grid now passes through
  seven components. Each is justified by a responsibility it does not share, or
  it should not exist.
- **The contract is fakeable without HTTP**, which is the one thing ADR-0001's
  `HttpTest` constraint could not give us. A hand-written fake of the contract
  lets the client, the cache, the strategy, the decorator and the tracker all be
  tested with no transport at all.
- **A narrow conflict with `AGENTS.md`**, and it must be written down rather
  than discovered: the versioned-contract pattern bans a mocking framework for
  the contract's test double, while `AGENTS.md` mandates NSubstitute. Resolved
  narrowly — the contract's double is hand-written; NSubstitute stays correct
  everywhere else. Neither document is wrong; their scopes differ.
- **A second, wider departure from that pattern: "exactly one production
  class" becomes one per transport.** The rule exists so a caller cannot pick
  an implementation, and that purpose survives intact — DI aliases the contract
  per constructed chain and no consumer is offered a choice. What changes is the
  count, because replay is a transport rather than a caller. Recorded here and
  as a constraint on the specification, not left to be discovered the first time
  someone counts implementors.
- **Aircraft replay and vessel replay substitute at different depths, and that
  asymmetry is accepted.** Aircraft replay stands in below the snapshot client,
  at the contract. A push provider has no contract to stand in for, so vessel
  replay must substitute higher — at the strategy itself. This follows from
  _this_ record's reading rather than from anything about the vessel feed, which
  is why it is stated here. The symmetry that matters is unchanged: both land on
  `ITrackerSource` and both are swap targets.
- **Only the contract layer claims conformance to that pattern.** The pattern is
  silent on caching, observables and streaming, and defines no layer above the
  contract except a CQRS handler. The client, the cache, the strategies, the
  decorator and the tracker are this repository's own design, and the
  specification says so instead of implying otherwise.
- **"One cache" is no longer literally true.** Caches are per client and typed
  to their snapshot. The invariant that survives is one _domain_ collection,
  downstream of `IFleetTracker` — which is the only one anything was ever bound
  to.
- **The push source stops being a special case in a different way.** It is not
  forced to assemble a full set so a shared differ can run; it writes what it
  knows to its own cache. The seam it shares with the polled source is
  `ITrackerSource`, at the domain boundary, where both genuinely look alike.
- **`dynamic-data-pipeline` changes character.** What it described as a
  free-standing operator chain is now what one component does internally, and
  `EditDiff` moves out of it entirely — into the client, where the writer is.
- **Six files stated the previous design and are corrected in the same change.**
  A prescription that contradicts the specification it governs is worse than no
  prescription — the same reason [ADR-0001](0001-flurl-for-http.md) updated
  `README.md` rather than leaving a reversed plan standing as current.
- **Two dependencies join the list**, neither yet in
  `Directory.Packages.props`: DynamicData for the caches and the pipeline, and
  Scrutor for the decorator's registration
  ([ADR-0003](0003-scrutor-for-decorator-registration.md)). Each is added by the
  first work item that needs it, not ahead of one.
- **Strategies are not required to be the same shape, and that is the design
  working rather than fraying.** The vessel strategy has no contract layer,
  because a socket has no endpoint to declare, and no hand-written rows step,
  because its JSON has names. Two components fewer. The temptation is to add a
  fake contract so the two paths diagram identically; that buys symmetry on a
  slide and a lie in the code. What makes the strategies substitutable is
  `ITrackerSource`, at the domain boundary — the only place both feeds genuinely
  look alike, and the reason the seam is there and not at the wire.
- **No questions are left open.** The three that blocked the first file — the
  version suffix, the integration layout, and whether a push provider needs a
  contract — are settled above. The four that did not have since been answered
  and recorded: the bounding box and interval in the Feature's `decisions/0001`,
  what the audience sees on a swap in its `decisions/0002`, staleness as marked
  and kept in claim B-051, and the decoration package in
  [ADR-0003](0003-scrutor-for-decorator-registration.md). Where replay
  substitutes was asked and answered after that, and is settled in this record.
- **`AGENTS.md` gains a rule rather than losing an argument.** It governs
  feature layout and said nothing about integrations, so there was no conflict
  to resolve — only a gap, now filled in the same change as the specification
  that needed it.
- **The talk now has seven boxes, which is more than a slide wants.** What the
  audience is shown and what the repository contains are no longer the same
  diagram, and that is a presentation decision rather than a design one.
