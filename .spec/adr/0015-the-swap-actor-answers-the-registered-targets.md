---
title: "ADR-0015: The swap actor answers the registered targets"
description: "Registration pairs each strategy with the name a swap control shows, and the swap actor answers the list as names and opaque handles, so a picker offers every registered source without naming a strategy or widening the seam."
type: adr
---

# ADR-0015: The swap actor answers the registered targets

**Status:** accepted

**Accepted 2026-10-09, built by `0080`.** The route was the person's choice the
same day, recorded in that item's `decisions`.

## Context

The person chose a picker over the registered sources for `fleet-dashboard`'s
swap control (`fleet-dashboard` B-040), not one button per source: one button
per source fails `fleet-dashboard` B-021's swap test, because adding a source
would edit markup. A picker needs the list of what it can select, and nothing
published one.

What [ADR-0011](0011-the-swap-decorator-selects-among-registered-strategies.md)
built does not reach a view model. The decorator holds the strategies, and the
actor named one by its per-type seam — a `Type` the message carried. Three
constraints close the obvious routes:

- the seam may not carry a name or a kind (`aircraft-source` B-037, § 5 row
  13);
- a view model may depend on actors and on what `IFleetTracker` publishes, and
  not on a strategy (`fleet-dashboard` B-020, `aircraft-source` B-041);
- `fleet-pipeline` was approved the day this was asked, and its tracker
  re-publishes only what its own claims name.

`aircraft-source` B-057 claims the list; this record is where it lives and how
it travels.

## Decision drivers

- A new source adds a picker choice by registering, and touches no view, view
  model or actor (`fleet-dashboard` B-021, ADR-0011's one-line promise).
- Nothing above the actor holds a strategy type, by name or in spirit.
- No seam another Feature published is widened or reopened.
- A strategy that is not registered cannot be offered — including a replay
  source a run names no recording for (`replay-source` B-024).

## Considered options

| Option                                    | Summary                                                                                                                        | Why not                                                                                                                                                                                                                |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A name on the seam                        | A per-type interface, or `ITrackerSource`, declares a display name the picker reads                                            | B-037 forbids it, and its analyzer rule reports it. The seam becomes a place to look things up rather than a place to substitute, and every strategy — a push source included — answers a question about presentation. |
| A view model enumerating strategies       | The view model takes `IEnumerable<ITrackerSourceStrategy>`, or a `Type` list, and names each                                   | It puts a strategy, or a strategy's type, in a view model: `fleet-dashboard` B-020 fails outright, or passes by name and fails in spirit with a `Type` list. The names would live in the view model, one per source.   |
| The tracker re-publishing the targets     | `IFleetTracker` publishes the list beside the poll status, so the view model reads it where it reads everything else           | **The person declined it on 2026-10-09.** It reopens `fleet-pipeline`, approved that day, for a value that never changes during a run — and a subscribed stream for a constant is the wrong shape.                     |
| **Registration names; the actor answers** | Each registration records a name beside its strategy; the swap actor answers names and opaque handles, and takes a handle back | **Chosen.**                                                                                                                                                                                                            |

## Decision

**The name is registration's, and the list is the swap actor's to answer.**

- **Registration pairs each strategy with its name.** `TrackerSourceEntry`, an
  internal record in `Tracking/Sources/`, holds the strategy and the name a
  swap control shows, and is registered beside each `ITrackerSourceStrategy`
  alias. The name is a constant in the registration method that registers the
  strategy, so a new source brings its own.
- **The decorator selects among entries.** `SwappingTrackerSource` takes the
  container's `IEnumerable<TrackerSourceEntry>` in place of the strategies and
  switches over the entry's strategy; `Connect()` is unchanged in behaviour.
  ADR-0011's two seams stand: the consumer seam is still the decorator's alone.
- **The actor answers and is told.** `SourceSwapActor` answers `GetSwapTargets`
  with `SwapTargets` — one `SwapTarget` per entry, in registration order,
  carrying the name and the entry's index. The index is an opaque handle: a view
  model shows the name and hands the target back in `SwapSource`, and the actor
  maps the handle to the entry and selects it. The list is asked once, with an
  explicit timeout, because registration fixes it for a run.

## Consequences

- **The swap test holds for the control.** A new source is one registration
  with a name, and the picker offers it with no markup, view model or actor
  edit.
- **Nothing above the actor holds a strategy type.** `SwapSource` no longer
  carries a `Type`, which is the half of ADR-0011's walkthrough this changes:
  its step 2 had the actor select by per-type seam, and the actor now selects
  by entry. The per-type seams remain, because registration resolves each
  strategy by its seam to pair it with its entry.
- **What is not registered is not offered, by construction.** A run naming no
  recording registers no replay strategy and so no replay entry; there is no
  filter to forget.
- **A name is registration's data, not the seam's.** B-037's analyzer rule is
  untouched, and an entry is unreachable from `ITrackerSource`.
- **The handle is positional.** It is valid only for the run that answered it,
  which holds because registration does not change during a run; a handle from
  another run is not a concept anything here has.
- **Which target is live is still not asked.** The actor answers what can be
  selected, never what is selected, so ADR-0011's "told, never asked" holds.
