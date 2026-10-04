---
name: dynamic-data-pipeline
description: Turn polled snapshots into a reactive collection with EditDiff in the client, then filter, sort, group, aggregate and bind downstream of one seam. Use when building or changing any part of the cache-to-UI pipeline.
---

# The DynamicData pipeline

**Polled data can still be reactive.** A reactive collection does not need a
push source: poll for a full snapshot, feed successive snapshots through
`EditDiff`, and from there on it is an ordinary DynamicData pipeline.

This file covers **the pipeline this repository builds**. For the operator
reference and `SourceCache` detail, see
[reactivemarbles/DynamicData](https://github.com/reactivemarbles/DynamicData).

The pipeline lives **downstream of the tracker seam**. Everything upstream of it
— the API types, the contract, the snapshot, the client, the cache, the
strategies and the swap decorator — belongs to
[`api-contract`](../api-contract/SKILL.md), whose arrangement
[ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md) records.

## Where `EditDiff` lives

**In the client, writing to its injected cache.** A fetch returns a whole set of
snapshots, and the client applies that set as one differential update: adds,
updates and removes fall out of comparing records. Snapshots have value equality
over every member, so "unchanged" is a question the compiler answers rather than
one the differ has to be configured for.

The rule has an address: **the component that fetched the data is the component
that works out what changed.** The cache itself is a plain store with no diff
policy — **the writer owns the write** — which is what lets a push client add
and remove what it was told about instead of assembling a full set so a shared
differ can run.

A domain object appears later and elsewhere: a per-type strategy projects
snapshot changesets into domain changesets
([`mapping`](../mapping/SKILL.md)).

## The spine

What the tracker does with the items a strategy hands it. The differ is not in
it — it ran in the client, and the projection ran in the strategy:

| Stage | Operator | Note |
|---|---|---|
| Changeset in | the strategy seam's `Connect()` | Where the tracker starts. |
| Search and filters | `Filter` with an observable predicate | The predicate is an observable so a keystroke re-filters without rebuilding. |
| Column sorting | `Sort` with a user-selected comparer | The comparer arrives as an observable too. |
| Grouped view | `Group` | By whatever grouping key the domain base exposes. |
| Summary counts | `Count` and other aggregates | Per group, derived from the same stream. |
| Property changes | `AutoRefresh` | Re-evaluates filters and sorts when an item's own properties change. |
| Silent items | `ExpireAfter` / staleness | Items that stop reporting. |
| Into the UI | `Bind` | The collection the view binds to ([`maui-ui`](../maui-ui/SKILL.md)). |

**Built once, at startup, and never rebuilt because the live source changed.**
Each client has its own cache of its own snapshot type, so "one cache" is not
literally true; the invariant that matters is **one domain collection**, inside
the tracker, which is the only thing anything was ever bound to. It holds the
abstract domain base ([`domain-model`](../domain-model/SKILL.md)), keyed on its
key.

## Push sources write what they know

- A polling client fetches a whole set and applies it as a differential update.
- A push client already knows what changed, so it adds and removes directly on
  its own cache. **Neither has to pretend to be the other**, because the cache
  has no diff policy to conform to.
- **Everything downstream is identical either way.** Both land on the seam as a
  changeset of domain items, which is the one place the two kinds of feed
  genuinely look alike — so the filters, comparers, groups and bindings cannot
  tell them apart. That equivalence is what makes a live source swap possible
  ([`hot-swap-source`](../hot-swap-source/SKILL.md)).

It is also why the seam sits at the domain boundary rather than the wire one.

## Staleness and expiry

- **The clock belongs to the tracker, and to nothing else.** No cache, no client
  and no strategy reads one — which is what makes all three testable with no
  scheduler at all. The tracker derives staleness from an item's last contact
  against an **injected clock**, never an ambient `UtcNow` read inline.
- A recorded source must age items the same way a live one does, and the
  provider's own reported time, carried on the response, is what lets it.
- **Two treatments, and they are not interchangeable.** A *stale indicator*
  keeps the row visible and marked; `ExpireAfter` removes it. A row vanishing
  unannounced reads as a bug; a row flagged as stale reads as information. Which
  a source gets is that source's decision, recorded in its specification, and
  the threshold is configurable.

## Keep the pipeline the thing that does the work

- Build the pipeline once. Filters, sorts, groups and bindings are constructed
  at startup and live for the application's lifetime; only their *inputs*
  change.
- No imperative list editing anywhere. If code is adding to or removing from the
  bound collection by hand, it is working around the pipeline.
- Each cache owns its own snapshot state; each strategy owns its projection; the
  tracker owns the pipeline and the clock; actors own time and failure
  ([`akka-actor`](../akka-actor/SKILL.md)).
- Dispose subscriptions deliberately. A pipeline rebuilt on every swap leaks.

## Testing

Everything here is testable without a network or a real clock: feed snapshot
sets into a client, advance an injected scheduler, assert the changesets and the
bound collection. Caches and strategies need no scheduler at all, because they
read no clock. The differ deserves its own test — one set, then a second with an
item added, one updated and one gone, asserting exactly three changes and no
change for the untouched item
([`test-from-scenarios`](../test-from-scenarios/SKILL.md)).

## Never add

- An `EditDiff` anywhere but a client writing to its own cache.
- A cache with a diff policy, a projection, or a clock.
- A domain object built anywhere but a strategy's projection.
- A push client assembling a full set purely so a shared differ can run — it
  knows what changed; writing that *is* the route.
- A second collection of tracked items alongside the tracker's.
- An ambient `UtcNow` read inline in an expiry or staleness check.
- An imperative add or remove on the bound collection.
- A pipeline rebuilt because the source changed.
- A filter or sort re-evaluated by a UI event handler instead of by
  `AutoRefresh` and an observable input.
