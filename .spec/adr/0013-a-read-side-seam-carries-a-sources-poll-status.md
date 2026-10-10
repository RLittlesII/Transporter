---
title: "ADR-0013: A read-side seam carries a source's poll status"
description: "The instant a source's next poll is due, and a provider's refusal with its retry-after, travel from the poller to the tracker through a provider-agnostic read-side seam in Tracking/, beside ITrackerSource rather than through it."
type: adr
---

# ADR-0013: A read-side seam carries a source's poll status

**Status:** accepted

## Context

[`fleet-dashboard` decision 0002](../../src/Transporter/Features/Fleet/.spec/decisions/0002-cards-and-a-trail-map-replace-the-grid.md)
puts the next poll and a provider's refusal on the page (`fleet-dashboard`
B-036). Only the poller knows either: `aircraft-source` B-054 reports when the
next poll is due, and B-055 reports a `429` with the interval
`X-Rate-Limit-Retry-After-Seconds` gave. `fleet-pipeline` B-040 has the tracker
re-publish both, because a view model depends on `IFleetTracker` and nothing
below it (`aircraft-source` B-041).

The seam a swap selects cannot carry them. `ITrackerSource.Connect()` returns
`IObservable<IChangeSet<TransportVehicle, string>>` and nothing else
(`aircraft-source` B-033), a per-type interface may not widen it (B-037), and
`fleet-pipeline` B-023 forbids the pipeline naming the poller, a client or a
concrete source. `fleet-pipeline` § 11 row 7 asked which seam does. The person
answered on 2026-10-08: a new read-side one.

## Decision drivers

- The swap test: a push source that never polls must plug in with no change to
  the tracker, and after a swap the page must not show the outgoing poller's
  status.
- The tracker holds no timer and no clock read of its own (`fleet-pipeline`
  B-004, B-040).
- No seam another Feature published is widened.

## Considered options

| Option                                  | Summary                                                                                                 | Why not                                                                                                                                                                       |
| --------------------------------------- | ------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A read-side seam in `Tracking/`         | A provider-agnostic stream of poll status, written by whatever polls and read by the tracker            | Chosen.                                                                                                                                                                       |
| Widen `ITrackerSource`                  | `Connect()`'s element, or a second member on the seam, carries the status beside the changesets         | `aircraft-source` B-033 and B-037 forbid it, and every strategy — a push source included — would have to answer a question about polling.                                     |
| Ride the observed clock's seam          | `IObservedClockTicks` (ADR-0010) carries the due instant and the refusal beside the instant             | The clock stops being a clock. ADR-0010 is a read-side seam for one value, and a refusal is not a time; its readers — staleness above all — would receive values they ignore. |
| A notice kind on `fleet-pipeline` B-025 | The tracker raises a "throttled" notice                                                                 | Notices are derived from changesets, and a refused poll produces none; the tracker would need to learn about polling from somewhere, which is this question again.            |
| Withdraw the poll status                | B-036, B-040, B-054 and B-055 withdrawn; the top bar shows the observed instant and the refresh control | The person declined it: throttling stays invisible on screen, which is the failure a presenter most needs to see during a talk.                                               |

## Decision

A poll status is published through a **read-side seam in `Tracking/`**, named
for no provider, carrying an optional instant the next poll is due and an
optional refusal with the interval the provider asked for. It is the other half
of the pair ADR-0010 made for the clock:

- **Written by whatever polls.** For OpenSky that is the poller behind
  `aircraft-source` B-054 and B-055; it writes on the scheduler it already runs
  on, so the seam reads no clock.
- **Selected with the source.** The swap decorator
  ([ADR-0011](0011-the-swap-decorator-selects-among-registered-strategies.md))
  hands the tracker the live source's status and switches it on a swap, so a
  swapped-out poller's status cannot survive the swap. A source that does not
  poll is registered with none, and the tracker publishes none
  (`fleet-pipeline` B-040).
- **Read by the tracker only.** The tracker re-publishes it on `IFleetTracker`,
  the move `fleet-pipeline` B-031 made for the observed instant. Nothing in
  `Features/` or `src/Gui` names the seam.

The member names, where the writer half lives, and how the decorator selects it
are `fleet-pipeline` § 7's and `aircraft-source` § 7's to write.

## Consequences

- `fleet-pipeline` B-040 and `aircraft-source` B-054 and B-055 have a route,
  and `fleet-pipeline` § 11 row 7 closes.
- A third read-side seam joins `IObservedClock` and `IObservedClockTicks`. Each
  carries one kind of value; none is widened.
- The swap decorator gains a second thing to select. That is the cost of the
  swap test holding for status as well as for fleet changes, and
  `aircraft-source` B-039's "nothing in the stream reveals the swap" now has a
  second stream to keep honest.
- The replay source reports no cadence today, so under replay the page shows no
  next poll. If it should, that is `replay-source`'s claim to make.
- `BoundaryAnalyzer` sees a new type in `Tracking/`. A view model naming it is
  `fleet-dashboard` B-020's violation, and whether a rule reports it is
  `boundary-analyzer`'s to decide.
