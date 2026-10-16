---
title: "ADR-0018: A registered entry carries its fleet description"
description: "TrackerSourceEntry holds the source's fleet description beside its strategy, name and poll status, and the swap decorator publishes the selected entry's description, so vehicles and the columns they are read through swap together."
type: adr
---

# ADR-0018: A registered entry carries its fleet description

**Status:** accepted

**Accepted 2026-10-16.** The person's call for `vessel-source` § 7, taking
recommendation A. It is built by
[`0102`](../../features/vessel-source/.issue/0102-vessel-strategy-on-the-seam.yml).
It supersedes
[ADR-0015](0015-the-swap-actor-answers-the-registered-targets.md), which still
holds in every part except what an entry carries.

## Context

`vessel-source` B-026 needs the fleet's description to change when the source
changes: the groupings, columns and selectors a vessel is read through are not
an aircraft's (`vessel-source` B-019).

Today the description is one `BehaviorSubject<FleetSourceDescription>` that
`OpenSkyRegistration` seeds with the aircraft's description, and nothing
swaps it. Under that design a vessel fleet would be shown through aircraft
columns. ADR-0015 made `TrackerSourceEntry` the one place registration records
what belongs to a source, but that entry holds only the strategy and its name.

## Decision drivers

- The vehicles and their description must never disagree, not even for one
  changeset.
- A new source brings its description by registering it, with no view, view
  model or actor edit (`fleet-dashboard` B-021).
- No seam another Feature published is widened (`aircraft-source` B-037).

## Considered options

| Option                                              | Summary                                                                                                                            | Why not                                                                                                                                               |
| --------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| **The entry carries it; the decorator switches it** | `TrackerSourceEntry` gains a `Description`, and `SwappingTrackerSource` publishes the selected entry's, switched like its `Status` | **Chosen.**                                                                                                                                           |
| A second subject the swap actor writes              | The actor writes the description to its own subject on each swap                                                                   | There are then two things to keep in step, and a window in which the vehicles and the description disagree. The actor also learns about presentation. |

## Decision

**The entry holds the description, and one switch decides both the vehicles
and their description.**

- `TrackerSourceEntry` holds the strategy, the name a swap control shows, the
  poll status, and the `FleetSourceDescription` the source's fleet is read
  through.
- `SwappingTrackerSource` publishes the selected entry's description through
  the same switch that selects its strategy and its status.
- The description's registration moves from `OpenSkyRegistration` to
  tracking. Each source's registration supplies its own description with its
  entry, and the aircraft entries, live and replay, supply the aircraft's.

What ADR-0015 decided still holds: registration names a source, and the swap
actor answers the list as names and opaque positional handles. The entry
gaining a member changes none of that.

## Consequences

- **The two cannot disagree.** A swap changes the vehicles and their
  description in one step, because one entry is selected.
- **A new source is still one registration.** It now registers a description
  as well as a name.
- **`aircraft-source` owes a matching claim.** Its B-057 claims only the name
  beside each strategy, so it must also claim the description, and its § 12
  row reopens.
- **The single seeded subject goes.** Anything that read
  `AircraftFleetDescription.Offered` through it now reads the decorator's
  published description.
