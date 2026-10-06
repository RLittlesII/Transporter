---
title: "ADR-0010: A third seam carries the observed clock's ticks"
description: "Noticing that the observed instant advanced is a new read-side interface, IObservedClockTicks, rather than a member added to the IObservedClock that aircraft-source published."
type: adr
---

# ADR-0010: A third seam carries the observed clock's ticks

**Status:** proposed

## Context

[ADR-0007](0007-the-live-source-advances-the-observed-clock.md) put one observed
clock behind two interfaces: `IObservedClockWriter`, which the integration
writes through, and `IObservedClock`, which exposes `Current` and is all a
consumer gets. The split is the point of that record — a consumer holding the
read side cannot advance time.

[`src/Transponder/Tracking`](../../src/Transponder/Tracking/.spec/README.md)
B-018 needs more than `Current`. A vehicle must become stale **with no new data
arriving for it**, which means the pipeline has to notice the instant advancing,
and `Current` cannot be noticed — it can only be read, by something that already
decided to look. That Feature's § 4 row 3 recorded the gap, and its § 7 closed
it by adding an `Instant` observable to `IObservedClock`.

`aircraft-source` published that interface. Widening it from a second Feature is
exactly the kind of edit ADR-0007's split exists to make deliberate, and its
§ 11 row 4 concern 4 held the question open for review.

## Decision drivers

- ADR-0007's read/write separation survives unchanged. A consumer that can watch
  time pass still cannot set it.
- A Feature does not widen an interface another Feature delivered without a
  record saying so.
- A test must be able to push ticks without standing up a clock that also has to
  answer `Current` consistently.
- No speculative generality: one observed clock, as ADR-0007 decided.

## Considered options

| Option                                                            | Summary                                                                                                                                   | Why not                                                                                                                                                                                                                                                       |
| ----------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A third interface on the read side **(chosen)**                   | `IObservedClockTicks` exposes `IObservable<DateTimeOffset> Instant`. `ObservedClock` implements all three; `IObservedClock` is untouched. | —                                                                                                                                                                                                                                                             |
| Add `Instant` to `IObservedClock`, as `fleet-pipeline` § 7 had it | One read seam answering everything about the clock, and no new type.                                                                      | A second Feature edits an interface the first delivered, and every implementer and test double in the repository gains a member whether it needed one or not. The read side also stops being the minimal thing ADR-0007 made it.                              |
| No clock observable — the pipeline ticks itself                   | An interval on the injected scheduler, reading `Current` on each tick. No interface changes anywhere.                                     | The re-evaluation cadence becomes the pipeline's own policy rather than the clock's, and a paused replay keeps ticking — so a loaded recording ages while nothing is being replayed, which is the failure ADR-0007 was written to prevent in the first place. |

## Decision

A third interface on the read side:

```csharp
public interface IObservedClockTicks
{
    /// <summary>Gets the observed instant, and every advance of it.</summary>
    IObservable<DateTimeOffset> Instant { get; }
}
```

`ObservedClock` implements `IObservedClock`, `IObservedClockWriter` and
`IObservedClockTicks`. Neither of the first two changes. A consumer that needs
to notice time passing takes the third; one that only needs the current instant
keeps taking `IObservedClock` and gains nothing to ignore.

## Consequences

- ADR-0007 stands unamended: this is an addition beside it, not an edit to it.
  That record is still `proposed`, so amending it would have been permitted —
  the reason not to is that its read/write split is the thing being relied on,
  and a seam added beside it keeps the split provable rather than widening the
  surface it was drawn around.
- `fleet-pipeline` § 4 row 3's impact changes from "§ 7 adds a member to the
  read side" to "a third read-side seam carries it", and B-018's claim text
  stops naming a specific interface.
- Three interfaces now describe one object, and a reader has to know that
  `ObservedClock` is the single implementation behind all of them. The
  registration is where that is visible, so it is the registration's business to
  state it in one place.
- A test for staleness takes the ticks seam alone and pushes instants, with no
  `Current` to keep consistent — which is the arrangement B-018's scenario
  already describes.
- Under replay, ticks come from the recording like every other instant. Nothing
  derives a tick from the wall clock or from a scheduler, so a paused replay does
  not age the fleet.
