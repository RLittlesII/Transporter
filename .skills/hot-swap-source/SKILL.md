---
name: hot-swap-source
description: Swap the live data source in the running application without rebuilding the cache, filters, sorts, groups or bindings. Use when building the source selector, the swap control, or anything that disposes a source.
---

# Hot-swapping the live source

Replace one live feed with another **in the running application**, and
everything downstream of the seam is unchanged. That is a design constraint
every other skill bends around, not a feature to bolt on at the end: if a swap
would require editing a view, a filter or a binding, the design has regressed.

## The mechanism

**A decorator, registered as the seam.** Every strategy adheres to the tracker
seam ([`api-contract`](../api-contract/SKILL.md)); so does the thing that picks
between them. A consumer resolves the seam, gets the decorator, and never learns
there was a choice to make.

```csharp
internal sealed class SwappingTrackerSource : ITrackerSource
{
    private readonly BehaviorSubject<ITrackerSource> _selected;

    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        _selected.Select(static source => source.Connect()).Switch();
}
```

- `Switch` unsubscribes from the outgoing strategy's inner sequence and
  subscribes to the incoming one. The tracker and everything it owns never know
  it happened.
- **There is no strategy resolver.** Nobody asks which strategy to use: the
  decorator _is_ the registration, so the set of strategies is known in one
  place and no caller has to know it at all.
- **Two seams, and that is what makes the set resolvable.** A strategy registers
  as the _strategy_ seam; the decorator is the only thing registered as the seam
  consumers resolve, and it takes the strategies as an `IEnumerable<>` of the
  first
  ([ADR-0011](../../.spec/adr/0011-the-swap-decorator-selects-among-registered-strategies.md)).
  Adding a source is one registration line. **A strategy registered as the
  consumer seam is handed to consumers in place of the decorator**, and the swap
  then silently does nothing.
- **Selection names a strategy by its per-type seam**, so no member carrying a
  name or a kind is added to the seam to support the swap. The decorator is
  _told_ what is live by the actor behind the control; nothing asks it.
- **Every source is a swap target, live or recorded**, because all of them reach
  the seam. **One switch, not two**: there is no separate offline mode and no
  second seam to bridge. They do not all reach the seam at the same depth, and
  they do not have to
  ([ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md)).
- The seam carries **domain items**, not snapshots. That is the point: both
  kinds of feed produce provider records, and what differs is the projection, so
  the place they genuinely look alike is after it.
- Each strategy brings its own client and its own cache, so a swap clears no
  shared collection — the outgoing cache simply stops being read.

## There is a gap, and something must occupy it

**No cache is cleared on a swap, but the consumer's collection is.** `Switch`
over a changeset stream is DynamicData's operator, not Rx's — `using
DynamicData` is what decides it — and that one emits a remove per item the
outgoing source had before the incoming source's changes arrive. So the outgoing
strategy's own cache is untouched and simply stops being read, while everything
downstream of the seam sees the old fleet leave the way it would if those items
had gone out of range. That is the wanted behaviour: the alternative, Rx's
`Switch`, leaves aircraft in the collection among the ships. Between the removes
and the first incoming change there is a gap of up to one poll interval.

**What a viewer sees during that gap is a decision, recorded per source in the
specification, not improvised in the code.** An unexplained empty view reads as
a crash; a mixed collection reads as a bug; pre-connecting both sources costs
two live connections and two budgets. Whichever is chosen, it is **never a
pipeline rebuild**.

## Disposal discipline

A swap is where leaks happen, and a leak looks like a bug in the reactive
library rather than in the code that misused it.

- **Give the subscription the poll, rather than giving the decorator a
  disposal.** A strategy's `Connect()` starts its own poller and hands back the
  thing that stops it — `Observable.Using(() => client.Poll(), …)` — so `Switch`
  dropping the subscription stops the poll and subscribing again starts it. The
  alternative, a decorator that disposes the outgoing source, leaves a
  swapped-away singleton unusable and so needs a factory per strategy to swap
  back at all. A poller that owns its own schedule and is not reachable from a
  subscription keeps spending the provider's budget
  ([`akka-actor`](../akka-actor/SKILL.md)).
- Close the outgoing socket, cancel its read loop, and do not let its reconnect
  logic race the new strategy by writing to its own cache behind the swap.
- **Swapping back and forth repeatedly must be flat** in memory and in whatever
  the provider meters. Exercise the swap more than once in a row.

## What must not be rebuilt

The whole proof is that these survive untouched — and all of them live inside
the tracker, which is why a swap cannot reach them:

- the tracker itself and its collection,
- `Filter` predicates and the search wiring,
- `Sort` comparers and the user's chosen column,
- `Group` and the per-group aggregates,
- the `Bind` target and every view binding.

A strategy's own cache is _not_ on this list. It belongs to one strategy and
goes quiet with it; the continuity is downstream of the seam, not upstream. The
UI's version of this rule is the swap test in
[`maui-ui`](../maui-ui/SKILL.md).

## Failure modes to exercise

- **Swapping while a request is in flight.** The late response arrives after the
  switch. It lands in the outgoing strategy's own cache, which nothing is
  reading — so it is harmless by construction rather than by vigilance. Cancel
  it anyway: a response that arrives is budget already spent, and a client still
  running is a client still polling.
- **Swapping before the incoming source has authenticated or emitted**: the view
  empties and stays empty. Swap only once the new source has emitted, or
  pre-connect.
- **The network dying mid-swap** — which is what a recorded source exists for.
- **Swapping twice quickly**, because someone will.

## Never add

- A restart, reload or page rebuild to change sources.
- A second selection path for an offline or recorded mode.
- A strategy resolver that callers ask which strategy is live.
- A `Switch` without stopping the outgoing strategy's client.
- A pipeline rebuilt on swap.
- A consumer that can observe from the seam which strategy produced a change.
