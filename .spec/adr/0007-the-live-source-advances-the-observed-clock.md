---
title: "ADR-0007: The live source advances the clock the fleet tracker reads"
description: "A single observed clock carries the provider's reported instant from the integration to IFleetTracker, written through one interface and read through another, so no seam widens and no consumer reads an ambient clock."
type: adr
---

# ADR-0007: The live source advances the clock the fleet tracker reads

**Status:** proposed

## Context

Two claims in [`src/Transponder/Integrations/OpenSky`](../../src/Transponder/Integrations/OpenSky/.spec/README.md)
§ 3 point at each other and meet nowhere. B-003 makes the provider's reported
time the observed instant for everything downstream and bans a consumer from
reading an ambient clock to supply one. B-043 puts the clock in `IFleetTracker`
and makes staleness derive from it. Neither says how the first value reaches the
second, and § 7 of that specification ruled out the route anyone reaches for
first: carried on the snapshot, the instant changes on every poll, so the differ
emits a change for every vehicle every interval and the mechanism the talk is
about produces nothing but churn.

The question is not local to that Feature.
[`features/replay-source`](../../features/replay-source/.spec/README.md) is the
reason B-003 exists at all — a recording's time comes from the recording, and a
replayed fleet that ages against the wall clock is stale the moment it is
loaded. Whatever answers this has to answer it for both, or
[ADR-0002](0002-contract-client-strategy-tracker.md)'s substitution at the
contract stops being transparent.

## Decision drivers

- The strategy seam keeps exactly one member. It is the one declaration the
  whole arrangement rests on.
- Replay and live age items by the same mechanism, or the swap is observable.
- Nothing downstream of `IFleetTracker` learns that an integration exists.
- No speculative generality: one live source at a time, which is what
  [`hot-swap-source`](../../.skills/hot-swap-source/SKILL.md) already
  guarantees.

## Considered options

| Option                                                                                   | Summary                                                                                                                                                | Why not                                                                                                                                                                                                                                                                                                                |
| ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A single observed clock, written by the integration and read by the tracker **(chosen)** | One object behind two interfaces: the chain reports each envelope's instant to it, and the tracker reads the current value.                            | —                                                                                                                                                                                                                                                                                                                      |
| A second member on the strategy seam                                                     | `ITrackerSource` carries the observed instant beside its changeset, so each source states its own time and two sources can never fight over one value. | It widens the one seam every strategy is substitutable through, and B-037's ban on widening a per-type seam sits immediately beside it. The arrangement's whole claim is that a source is interchangeable through a single declaration; a second member makes every future source implement a concept it may not have. |
| The instant on each projected vehicle                                                    | Each `TransportVehicle` carries the instant it was observed at. No seam change, no clock, and no shared state.                                         | B-011 means a vehicle whose snapshot did not change is never re-emitted, so the carried instant goes stale on precisely the vehicles staleness is about — the ones that stopped reporting. It is the first thing anyone proposes and it fails on the only case that matters.                                           |
| A system clock behind the same interface                                                 | Inject `DateTimeOffset.UtcNow` and be done.                                                                                                            | It is what B-003 forbids, spelled as a dependency. Under replay it reads the wall clock while the data reads the recording, so every replayed vehicle is stale on arrival.                                                                                                                                             |

## Decision

**One clock object, written through a write-only interface and read through a
read-only one.** The integration reports the instant each envelope carries; the
tracker reads the current value and never writes it.

```csharp
public interface IObservedClock
{
    DateTimeOffset Current { get; }
}

internal interface IObservedClockWriter
{
    void Observe(DateTimeOffset instant);
}
```

1. **The pair is the enforcement.** A consumer holding `IObservedClock` cannot
   advance time and an integration holding `IObservedClockWriter` cannot read
   it, so B-003's ban is a matter of which interface a constructor names rather
   than a rule someone remembers. One `internal sealed` class implements both
   and is registered once, aliased to each.
2. **`Observe` sets; it does not take the later of the two.** A recording that
   restarts moves time backwards, and a clock that only ever advances would hold
   the end of the previous run against the start of the next. Monotonicity is
   the live source's property, not the clock's.
3. **The component that receives the envelope is the one that reports it**, so
   the value never travels through the snapshot, the cache, or the seam. Which
   component that is, for the aircraft chain, is
   [`src/Transponder/Integrations/OpenSky`](../../src/Transponder/Integrations/OpenSky/.spec/README.md)
   § 7's to name.
4. **Before any source has reported, `Current` is `DateTimeOffset.MinValue`.**
   Nothing is stale against it, which is the right answer when nothing has been
   observed — and the collection is empty at that point, so nothing reads it.
   A zero value that made everything stale would paint a full grid red on the
   first frame.
5. **`IObservedClock` is public and `IObservedClockWriter` is not.** The read
   side is what a view model's staleness display will eventually bind through;
   the write side belongs to the integrations and nothing above them may name
   it.

## Consequences

- **The clock is shared mutable state, and exactly one source feeds it.**
  That is not a convention: `hot-swap-source` § "Disposal discipline" stops the
  outgoing source as part of the swap, so two live sources writing one clock is
  a state the design already excludes. The cost that would otherwise make this
  option undefined is bounded by a rule that exists for another reason.
- **Replay needs no second mechanism.** A recorded envelope carries the instant
  it was recorded with, and the same call reports it, which is what makes
  substitution at the contract transparent all the way to staleness.
- **`src/Transponder/Integrations/OpenSky` `0007` is unblocked** — B-043's clock now has a
  type to be injected with, and its § 11 question is answered by this record.
- **A test of anything time-derived observes an instant first.** That is the
  injected-clock discipline
  [`test-from-scenarios`](../../.skills/test-from-scenarios/SKILL.md) already
  asks for, now with one object to advance instead of a mock per test.
- **Two more declarations in the tracking surface**, one of them public. The
  alternative was a second member on the seam, which is one declaration fewer
  and binds every future source; this binds only the sources that have a
  reported time, and a source with none simply never calls `Observe`.
- **A source that reports no time leaves the clock where it is.** For a push
  feed whose frames carry no timestamp, staleness would then measure from the
  last timed observation — a real gap, and the Feature that adds such a source
  is where it gets answered rather than here.
