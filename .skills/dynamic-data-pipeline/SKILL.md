---
name: dynamic-data-pipeline
description: Turn polled snapshots into a reactive collection with EditDiff, then filter, sort, group, aggregate and bind — the demo's headline lesson. Use when building or changing any part of the cache-to-UI pipeline.
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

**`DynamicData` is not yet in
[`Directory.Packages.props`](../../Directory.Packages.props).** Adding it
belongs to the first issue that builds the pipeline; this file describes where
it is going, not code that exists.

## The spine

In roughly the order the audience meets it (README's operator table):

| Stage | Operator | Note |
|---|---|---|
| Snapshot → changeset | `EditDiff` | **The headline.** Successive full snapshots become add/update/remove changesets. |
| Search and dropdowns | `Filter` with an observable predicate | The predicate is an observable so a keystroke re-filters without rebuilding. |
| Column sorting | `Sort` with a user-selected comparer | The comparer arrives as an observable too. |
| Grouped grid | `Group` | Origin country or category; flag or ship type for vessels. |
| Summary counts | `Count` and other aggregates | Per group, derived from the same stream. |
| Property changes | `AutoRefresh` | Re-evaluates filters and sorts when an item's own properties change. |
| Silent items | `ExpireAfter` / staleness | Items that stop reporting. |
| Into the UI | `Bind` | The collection the view binds to — see [`build-maui-ui`](../build-maui-ui/SKILL.md). |

The cache holds the abstract `TransportVehicle` from
[`transponder-domain-model`](../transponder-domain-model/SKILL.md), keyed on
its key — `icao24` for aircraft, MMSI for vessels. One cache, one item type,
one key type, whichever source is live.

## Push sources skip only the first step

The ships stretch goal is a push feed, and that is the lesson's other half:

- A polled source emits whole snapshots → `EditDiff` derives the changeset.
- A push source already knows what changed → `AddOrUpdate` / `Remove`
  straight onto the cache.
- **Everything after the cache is identical either way.** Same filters, same
  comparers, same groups, same bindings. That equivalence is what the live
  swap demonstrates on stage — see
  [`hot-swap-source`](../hot-swap-source/SKILL.md).

Say this out loud in the talk; it is why the seam was built the way it was.

## Staleness and expiry

- Staleness derives from last contact against an **injected clock**, never
  `DateTime.UtcNow` read inline. Replay must age items the same way live does
  ([`api-mock`](../api-mock/SKILL.md)).
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
- The cache owns collection state; actors own time and failure
  ([`akka-actor`](../akka-actor/SKILL.md)).
- Dispose subscriptions deliberately. A pipeline rebuilt on every swap leaks,
  and a leak in a demo looks like a memory bug on a projector.

## Testing

Everything here is testable without a network or a real clock: feed snapshots
in, advance an injected scheduler, assert the changesets and the bound
collection. See [`test-from-scenarios`](../test-from-scenarios/SKILL.md). The
`EditDiff` behavior deserves its own test — one snapshot, then a second with
an item added, one updated and one gone, asserting exactly three changes.

## Never add

- A second collection of tracked items alongside the cache.
- `DateTime.UtcNow` read inline in an expiry or staleness check.
- An imperative add or remove on the bound collection.
- A pipeline rebuilt because the source changed.
- A filter or sort re-evaluated by a UI event handler instead of by
  `AutoRefresh` and an observable input.
