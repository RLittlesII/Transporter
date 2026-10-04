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

**A decorator, registered as the seam.** Every strategy adheres to
`ITrackerSource` ([`api-contract`](../api-contract/SKILL.md)); so does the thing
that picks between them. A consumer resolves `ITrackerSource`, gets the
decorator, and never learns there was a choice to make.

```csharp
// Provisional: this shape is a first reading of the Rx/DynamicData docs.
internal sealed class SwappingTrackerSource : ITrackerSource
{
    private readonly BehaviorSubject<ITrackerSource> _selected;

    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        _selected.Select(static source => source.Connect()).Switch();
}
```

- `Switch` unsubscribes from the outgoing strategy's inner sequence and
  subscribes to the incoming one. `IFleetTracker` and everything it owns never
  know it happened.
- **There is no strategy resolver.** Nobody asks which strategy to use: the
  decorator *is* the registration, so the set of strategies is known in one
  place and no caller has to know it at all. Whether that registration needs a
  decoration package or a hand-written one is an open question on the
  aircraft-source specification (§ 11).
- Live OpenSky, live AISStream, a replay and a simulated source are all swap
  targets, because all four are strategies — including replay-as-fallback
  ([`api-mock`](../api-mock/SKILL.md)). **One switch, not two**: there is no
  separate offline mode and no second seam to bridge.
- The seam carries **domain vehicles**, not snapshots. That is the point: both
  feeds produce provider records, and what differs is the projection, so the
  place they genuinely look alike is after it. See
  [`dynamic-data-pipeline`](../dynamic-data-pipeline/SKILL.md) and
  [ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md).
- Each strategy brings its own client and its own cache, so a swap does not
  clear a shared collection — the outgoing cache simply stops being read. What
  the audience sees as a result is a rehearsal question (below, and § 11 Q2).

## What the audience sees on swap — a real decision

Per-strategy caches change the shape of this question. Nothing is cleared on a
swap: the outgoing cache stops being read, and the incoming strategy's
changeset replaces the fleet. This is a *stage effect*, so it is a rehearsal
call, not a correctness one:

| Option | What the audience sees | Cost |
|---|---|---|
| **A clean cut** (recommended) | The incoming strategy's first changeset removes the planes and adds the ships in one step. A legible swap. | The "nothing else changed" point has to be said, not just seen — the grid looks like it reloaded. |
| Let `ExpireAfter` age the planes out | Planes and ships coexist briefly, then planes drain away. Dramatic, and it showcases expiry. | A few seconds of mixed fleet, which can read as a bug to anyone not following closely. Needs the decorator to merge rather than switch. |

Pick in rehearsal and write the choice into the specification. Whichever is
chosen, it is **never a pipeline rebuild**.

## Disposal discipline

The swap is where leaks happen, and a leak on a projector looks like a bug in
DynamicData rather than in the demo.

- **Stop the outgoing strategy's client.** A `Switch` drops the subscription,
  but a poller that owns its own schedule keeps polling — and keeps spending
  OpenSky credits — unless it is stopped. See
  [`akka-actor`](../akka-actor/SKILL.md).
- Close the outgoing WebSocket, cancel its read loop, and do not let its
  reconnect logic race the new strategy by writing to its own cache behind the
  swap.
- Swapping back and forth repeatedly must be flat in memory and in credit
  burn. Rehearse the swap more than once in a row.

## What must not be rebuilt

The whole proof is that these survive untouched — and all of them live inside
`IFleetTracker`, which is why a swap cannot reach them:

- the tracker itself and its collection,
- `Filter` predicates and the search box wiring,
- `Sort` comparers and the user's chosen column,
- `Group` and the per-group aggregates,
- the `Bind` target and every view binding.

Each strategy's own cache is *not* on this list. It belongs to one strategy and
goes quiet with it; the continuity is downstream of the seam, not upstream.

If a swap requires re-creating any of them, the design has regressed and the
stretch goal is broken on paper. The UI's version of this rule is the swap
test in [`build-maui-ui`](../build-maui-ui/SKILL.md).

## Failure modes to rehearse

- **Swapping while a poll is in flight.** The in-flight response arrives after
  the switch. It lands in the outgoing strategy's own cache, which nothing is
  reading — so it is harmless by construction rather than by vigilance. Cancel
  it anyway: a response that arrives is a credit already spent, and a client
  still running is a client still polling.
- Swapping before the ships feed has authenticated: the grid empties and
  stays empty. Pre-connect, or swap only once the new source has emitted.
- A venue network that dies *during* the closing act. That is what the
  recorded vessel replay is for.
- Swapping twice quickly, because someone will.

## Never add

- A restart, reload or page rebuild to change sources.
- A second selection path for offline or replay mode.
- A strategy resolver that callers ask which strategy is live.
- A `Switch` without stopping the outgoing strategy's client.
- A pipeline rebuilt on swap.
- A consumer that can observe from the seam which strategy produced a change.
