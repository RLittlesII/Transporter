---
name: hot-swap-source
description: Swap the live data source mid-demo — planes to ships — without rebuilding the cache, filters, sorts, groups or bindings. Use when building the source selector, the swap control, or anything that disposes a source.
---

# Hot-swapping the live source

The closing act ([`README.md`](../../README.md) § "Closing act"): replace the
polled aircraft feed with a push vessel feed and show that everything
downstream of the cache is unchanged. The ambition here is sharper than a
restart — **swap the stream in the running app, on stage**, and let the
audience watch the same grid, the same filters, the same sorts and groups
refill with ships.

If that works, the talk's claim is proved in front of people instead of
asserted. So this is the design constraint every other skill bends around,
not a feature to bolt on at the end.

## The mechanism

One observable of "which source is live", and `Switch` doing the work:

```csharp
// Provisional: this shape is a first reading of the Rx/DynamicData docs.
private readonly BehaviorSubject<ITrackingSource> _selected = new(initialSource);

IObservable<SnapshotSet<AircraftSnapshot>> snapshots =
    _selected.Select(static source => source.Snapshots).Switch();
```

- `Switch` unsubscribes from the outgoing source's inner sequence and
  subscribes to the incoming one. The cache, the tracker and the pipeline
  *after* this point never know it happened.
- The selector holds `ITrackingSource` from
  [`api-contract`](../api-contract/SKILL.md), so live OpenSky, live AISStream,
  a replay and a simulated source are all swap targets — including
  replay-as-fallback ([`api-mock`](../api-mock/SKILL.md)). **One switch, not
  two**: there is no separate offline mode, and no second source interface for
  the `Switch` to bridge.
- Every source emits a snapshot set, and **the cache diffs every set it is
  given** — a push feed included. That is what keeps one seam here rather than
  two, and it is why the swap is a cache clear at most. See
  [`dynamic-data-pipeline`](../dynamic-data-pipeline/SKILL.md) and
  [ADR-0002](../../.spec/adr/0002-four-layers-wire-to-fleet.md). How the vessel
  feed assembles its own full set is an open question on the aircraft-source
  specification (§ 11), for the closing-act feature.

## What the cache does on swap — a real decision

Two defensible answers, and this is a *stage effect*, so it is a rehearsal
call, not a correctness one:

| Option | What the audience sees | Cost |
|---|---|---|
| **Clear and refill** (recommended) | Grid empties, then fills with ships. A clean, legible cut. | A second or two of empty grid; the "nothing else changed" point has to be said, not just seen. |
| Let `ExpireAfter` age the planes out | Planes and ships coexist briefly, then planes drain away. Dramatic, and it showcases expiry. | A few seconds of mixed fleet, which can read as a bug to anyone not following closely. |

Recommend clear-and-refill; pick in rehearsal and write the choice into the
specification. Whichever is chosen, it is a **cache clear, not a pipeline
rebuild**.

## Disposal discipline

The swap is where leaks happen, and a leak on a projector looks like a bug in
DynamicData rather than in the demo.

- **Stop the outgoing source actor.** A `Switch` drops the subscription, but a
  poller actor that owns its own schedule keeps polling — and keeps spending
  OpenSky credits — unless it is stopped. See
  [`akka-actor`](../akka-actor/SKILL.md).
- Close the outgoing WebSocket, cancel its read loop, and do not let its
  reconnect logic race the new source back onto the cache.
- Swapping back and forth repeatedly must be flat in memory and in credit
  burn. Rehearse the swap more than once in a row.

## What must not be rebuilt

The whole proof is that these survive untouched:

- the cache itself,
- the tracker that projects it,
- `Filter` predicates and the search box wiring,
- `Sort` comparers and the user's chosen column,
- `Group` and the per-group aggregates,
- the `Bind` target and every view binding.

If a swap requires re-creating any of them, the design has regressed and the
stretch goal is broken on paper. The UI's version of this rule is the swap
test in [`build-maui-ui`](../build-maui-ui/SKILL.md).

## Failure modes to rehearse

- **Swapping while a poll is in flight.** The in-flight response arrives
  after the switch — it must not land in the cache. Cancel on swap and drop
  late results by checking the result against the current source, not by
  hoping about timing.
- Swapping before the ships feed has authenticated: the grid empties and
  stays empty. Pre-connect, or swap only once the new source has emitted.
- A venue network that dies *during* the closing act. That is what the
  recorded vessel replay is for.
- Swapping twice quickly, because someone will.

## Never add

- A restart, reload or page rebuild to change sources.
- A second selection path for offline or replay mode.
- A `Switch` without stopping the outgoing source.
- A cache or pipeline rebuilt on swap.
- An in-flight response applied after its source stopped being current.
