---
name: dynamic-data-pipeline
description: Turn polled snapshots into a reactive collection with EditDiff inside IVehicleCache, project them to domain vehicles with IFleetTracker, then filter, sort, group, aggregate and bind — the demo's headline lesson. Use when building or changing any part of the cache-to-UI pipeline.
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

The pipeline begins at `IFleetTracker`'s stream. Everything upstream of that —
the API types, the snapshot, the seam — belongs to
[`api-contract`](../api-contract/SKILL.md).

**`DynamicData` is not yet in
[`Directory.Packages.props`](../../Directory.Packages.props).** Adding it
belongs to the first issue that builds the cache; this file describes where
it is going, not code that exists.

## Where `EditDiff` lives

**Inside the `IVehicleCache` implementation.** Applying a snapshot set is what
runs it: the cache holds `AircraftSnapshot` keyed on `icao24`, and a new set
becomes adds, updates and removes by comparing records. Snapshots have value
equality over every member, so "unchanged" is a question the compiler answers
rather than one the differ has to be configured for.

This is the headline ([`README.md`](../../README.md) § "Core idea") and it now
has an address — a component with a name, rather than row one of an operator
table. `IFleetTracker` wraps the cache and projects its snapshot changesets
into `TransportVehicle` changesets; that projection is the second mapping layer
([`mapping`](../mapping/SKILL.md)) and the first place a domain object exists.
The reasoning is [ADR-0002](../../.spec/adr/0002-four-layers-wire-to-fleet.md).

## The spine

In roughly the order the audience meets it (README's operator table). The
differ is not in it — the spine is what the *pipeline* does, downstream of the
tracker:

| Stage | Operator | Note |
|---|---|---|
| Changeset in | `IFleetTracker`'s stream | Where the pipeline starts. The differ already ran, inside the cache. |
| Search and dropdowns | `Filter` with an observable predicate | The predicate is an observable so a keystroke re-filters without rebuilding. |
| Column sorting | `Sort` with a user-selected comparer | The comparer arrives as an observable too. |
| Grouped grid | `Group` | Origin country or category; flag or ship type for vessels. |
| Summary counts | `Count` and other aggregates | Per group, derived from the same stream. |
| Property changes | `AutoRefresh` | Re-evaluates filters and sorts when an item's own properties change. |
| Silent items | `ExpireAfter` / staleness | Items that stop reporting. |
| Into the UI | `Bind` | The collection the view binds to — see [`build-maui-ui`](../build-maui-ui/SKILL.md). |

`IVehicleCache` holds `AircraftSnapshot` keyed on `icao24`. `IFleetTracker`
emits the abstract `TransportVehicle` from
[`transponder-domain-model`](../transponder-domain-model/SKILL.md), keyed on
its key — `icao24` for aircraft, MMSI for vessels. One cache, one stored type,
one key type, and one stream of domain vehicles out, whichever source is live.

## Push sources use the same door

The ships stretch goal is a push feed, and that is the lesson's other half:

- A polled source emits whole snapshot sets, and the cache diffs them.
- A push source emits its current known set too, and the cache diffs that
  identically. **Push or pull does not matter to the cache**, which is what
  keeps one seam rather than two.
- **Everything after the cache is identical either way.** Same filters, same
  comparers, same groups, same bindings — now structurally so rather than by
  discipline, because neither the cache nor the tracker can tell which kind of
  source filled it. That equivalence is what the live swap demonstrates on
  stage — see [`hot-swap-source`](../hot-swap-source/SKILL.md).

Say this out loud in the talk; it is why the seam was built the way it was.

The cost is real and is not settled here: a push feed must accumulate its own
known set in order to emit one, which brushes the rule against a second
collection below. That is an open question on the aircraft-source
specification (§ 11), for the closing-act feature to answer.

## Staleness and expiry

- **Staleness is the tracker's, not the cache's.** The cache reads no clock at
  all, which is what makes it testable with no scheduler; `IFleetTracker`
  derives staleness from the snapshot's last contact against an **injected
  clock**, never `DateTime.UtcNow` read inline. Replay must age items the same
  way live does ([`api-mock`](../api-mock/SKILL.md)), and the observed instant
  carried on each snapshot set is what lets it.
- Two different treatments, and the demo shows both: a *stale indicator* keeps
  the row visible and marked; `ExpireAfter` removes it. Pick per source and
  state which.
- **Vessels go silent rather than disappearing**, so `ExpireAfter` matters at
  least as much on the ships feed as on aircraft. A silent ship that never
  expires is a dashboard slowly filling with ghosts.

## Keep the pipeline the thing that does the work

- Build the pipeline once. Filters, sorts, groups and bindings are
  constructed at startup and live for the app's lifetime; only their *inputs*
  change.
- No imperative list editing anywhere. If code is adding to or removing from
  the bound collection by hand, it is working around the pipeline.
- `IVehicleCache` owns collection state; `IFleetTracker` owns the projection
  and staleness; actors own time and failure
  ([`akka-actor`](../akka-actor/SKILL.md)).
- Dispose subscriptions deliberately. A pipeline rebuilt on every swap leaks,
  and a leak in a demo looks like a memory bug on a projector.

## Testing

Everything here is testable without a network or a real clock: feed snapshot
sets in, advance an injected scheduler, assert the changesets and the bound
collection. The cache needs no scheduler at all, because it reads no clock. See
[`test-from-scenarios`](../test-from-scenarios/SKILL.md). The differ deserves
its own test — one set, then a second with an item added, one updated and one
gone, asserting exactly three changes and no change for the untouched item.
That is claim B-025 of the aircraft-source specification.

## Never add

- An `EditDiff` outside the `IVehicleCache` implementation.
- A domain object stored in the cache, or a clock read inside it.
- A snapshot set constructed by a push source purely so it can reach the
  differ by the polled route — it already emits a set; that *is* the route.
- A second collection of tracked items alongside the cache.
- `DateTime.UtcNow` read inline in an expiry or staleness check.
- An imperative add or remove on the bound collection.
- A pipeline rebuilt because the source changed.
- A filter or sort re-evaluated by a UI event handler instead of by
  `AutoRefresh` and an observable input.
