---
name: dynamic-data-pipeline
description: Turn polled snapshots into a reactive collection with EditDiff in the client, project them to domain vehicles in a per-type strategy, then filter, sort, group, aggregate and bind inside IFleetTracker — the demo's headline lesson. Use when building or changing any part of the cache-to-UI pipeline.
---

# The DynamicData pipeline

This is the demo's headline: **polled data can still be reactive**
([`README.md`](../../README.md) § "Core idea"). Many developers believe a
reactive collection needs a push source. Poll a REST endpoint for a full
snapshot, feed successive snapshots through `EditDiff`, and from there on it
is an ordinary DynamicData pipeline — which is exactly the point being made to
an audience of line-of-business developers whose data lives behind
request/response services.

This file covers **the pipeline this demo builds and the order the audience
meets it in**. For the operator reference and `SourceCache` detail, see
[reactivemarbles/DynamicData](https://github.com/reactivemarbles/DynamicData).

The pipeline lives **inside `IFleetTracker`**. Everything upstream of it — the
API types, the contract, the snapshot, the client, the cache, the strategies and
the swap decorator — belongs to
[`api-contract`](../api-contract/SKILL.md).

**`DynamicData` is not yet in
[`Directory.Packages.props`](../../Directory.Packages.props).** Adding it
belongs to the first issue that builds a cache; this file describes where it is
going, not code that exists.

## Where `EditDiff` lives

**In the client, writing to its injected cache.** A fetch returns a whole set of
snapshots, and the client applies that set as one differential update: adds,
updates and removes fall out of comparing records. Snapshots have value equality
over every member, so "unchanged" is a question the compiler answers rather than
one the differ has to be configured for.

This is the headline ([`README.md`](../../README.md) § "Core idea") and it has
an address: the component that fetched the data is the component that works out
what changed. The cache itself is a plain store with no diff policy — **the
writer owns the write** — which is what lets a push client add and remove what
it was told about instead of assembling a full set so a shared differ can run.

A domain object appears later and elsewhere: a per-type strategy
(`IAirplaneTrackerSource`, `IVesselTrackerSource`) projects snapshot changesets
into `TransportVehicle` changesets with Mapperly
([`mapping`](../mapping/SKILL.md)). The reasoning for the whole arrangement is
[ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md).

## The spine

In roughly the order the audience meets it (README's operator table). The differ
is not in it — the spine is what **`IFleetTracker`** does with the vehicles a
strategy hands it:

| Stage | Operator | Note |
|---|---|---|
| Changeset in | `ITrackerSource.Connect()` | Where the tracker starts. The differ ran in the client; the projection ran in the strategy. |
| Search and dropdowns | `Filter` with an observable predicate | The predicate is an observable so a keystroke re-filters without rebuilding. |
| Column sorting | `Sort` with a user-selected comparer | The comparer arrives as an observable too. |
| Grouped grid | `Group` | Origin country or category; flag or ship type for vessels. |
| Summary counts | `Count` and other aggregates | Per group, derived from the same stream. |
| Property changes | `AutoRefresh` | Re-evaluates filters and sorts when an item's own properties change. |
| Silent items | `ExpireAfter` / staleness | Items that stop reporting. |
| Into the UI | `Bind` | The collection the view binds to — see [`build-maui-ui`](../build-maui-ui/SKILL.md). |

**Built once, at startup, and never rebuilt because the live source changed.**
Each client has its own cache of its own snapshot type, so "one cache" is not
literally true — the invariant that matters is one *domain* collection, inside
the tracker, which is the only thing anything was ever bound to. It holds the
abstract `TransportVehicle` from
[`transponder-domain-model`](../transponder-domain-model/SKILL.md), keyed on its
key — `icao24` for aircraft, MMSI for vessels.

## Push sources write what they know

The ships stretch goal is a push feed, and that is the lesson's other half:

- A polling client fetches a whole set and applies it as a differential update.
- A push client already knows what changed, so it adds and removes directly on
  its own cache. **Neither has to pretend to be the other**, because the cache
  has no diff policy to conform to.
- **Everything downstream is identical either way.** Both land on
  `ITrackerSource` as a changeset of `TransportVehicle`, which is the one place
  the two feeds genuinely look alike — so the filters, comparers, groups and
  bindings inside the tracker cannot tell them apart. That equivalence is what
  the live swap demonstrates on stage — see
  [`hot-swap-source`](../hot-swap-source/SKILL.md).

Say this out loud in the talk; it is why the seam sits at the domain boundary
rather than the wire one.

## Staleness and expiry

- **The clock belongs to `IFleetTracker`, and to nothing else.** No cache, no
  client and no strategy reads one — which is what makes all three testable with
  no scheduler at all. The tracker derives staleness from a vehicle's last
  contact against an **injected clock**, never `DateTime.UtcNow` read inline.
  Replay must age items the same way live does
  ([`api-mock`](../api-mock/SKILL.md)), and the provider's own reported time,
  carried on the response envelope, is what lets it.
- Two treatments exist: a *stale indicator* keeps the row visible and marked;
  `ExpireAfter` removes it. **Aircraft are marked and kept** — threshold
  configurable, five minutes by default (B-051) — so `ExpireAfter` has no role
  in the aircraft demo. A row vanishing mid-sentence reads as a bug; a row
  flagged as stale reads as information.
- **Vessels go silent rather than disappearing**, so `ExpireAfter` may still
  earn its place on the ships feed, where a silent ship that never ages out is a
  dashboard slowly filling with ghosts. That is the closing act's call, per
  source, and it is why the operator stays on the README's list.

## Keep the pipeline the thing that does the work

- Build the pipeline once. Filters, sorts, groups and bindings are
  constructed at startup and live for the app's lifetime; only their *inputs*
  change.
- No imperative list editing anywhere. If code is adding to or removing from
  the bound collection by hand, it is working around the pipeline.
- Each cache owns its own snapshot state; each strategy owns its projection;
  `IFleetTracker` owns the pipeline and the clock; actors own time and failure
  ([`akka-actor`](../akka-actor/SKILL.md)).
- Dispose subscriptions deliberately. A pipeline rebuilt on every swap leaks,
  and a leak in a demo looks like a memory bug on a projector.

## Testing

Everything here is testable without a network or a real clock: feed snapshot
sets into a client, advance an injected scheduler, assert the changesets and the
bound collection. Caches and strategies need no scheduler at all, because they
read no clock. See [`test-from-scenarios`](../test-from-scenarios/SKILL.md). The
differ deserves its own test — one set, then a second with an item added, one
updated and one gone, asserting exactly three changes and no change for the
untouched item. That is claim B-023 of the aircraft-source specification.

## Never add

- An `EditDiff` anywhere but a client writing to its own cache.
- A cache with a diff policy, a projection, or a clock.
- A domain object built anywhere but a strategy's projection.
- A push client assembling a full set purely so a shared differ can run — it
  knows what changed; writing that *is* the route.
- A second collection of tracked vehicles alongside the tracker's.
- `DateTime.UtcNow` read inline in an expiry or staleness check.
- An imperative add or remove on the bound collection.
- A pipeline rebuilt because the source changed.
- A filter or sort re-evaluated by a UI event handler instead of by
  `AutoRefresh` and an observable input.
