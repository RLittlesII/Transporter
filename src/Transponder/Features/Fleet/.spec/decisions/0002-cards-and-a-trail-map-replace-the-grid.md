---
title: "Decision 0002: Cards and a trail map replace the grid, on a dark theme read without colour"
description: "The fleet is shown as cards that say where each aircraft is and how far it moved, the detail pane draws the path it flew, and the page is one dark theme whose every status carries an icon and a word as well as a colour."
type: decision
---

# Decision 0002: Cards and a trail map replace the grid, on a dark theme read without colour

**Status:** decided

**Date:** 2026-10-08
**Made by:** the person

## The call

The fleet is shown as **cards**, one per vehicle, in columns that follow the
page's width, and each card says where its aircraft is over and how far it moved
since the last poll and since it was first seen. The detail pane draws the
**path the aircraft flew** on a map, coloured by altitude and broken where the
feed went quiet. The page is **one dark theme** for all-day viewing, and every
status on it is told apart by an icon and a word as well as a colour.

The look is the "Transponder Flight Deck" design system, published as a private
artifact on 2026-10-08 and extracted from this codebase. It sets the tokens,
the components and their states; the claims below set what the application
owes.

## Why

The grid was too compact to show what is changing, which is the one thing the
demo exists to show. A row of callsign, country, last contact and raw
coordinates reads as a static table even while the feed runs: nothing says a
position moved, and nothing says where it moved to. A card has room for the
values that change every poll, and a trail makes the stream visible over time
rather than only at the instant of the last changeset.

Every one of those values is derived — a distance needs the previous position, a
trail needs every position, a place needs a table — and `fleet-dashboard` B-018
forbids a view model deriving anything. So the call lands mostly in
`fleet-pipeline`, as stages the audience can read beside the counts they already
see (its B-032 – B-040), and this Feature binds them (B-029 – B-037). That is
the demo's own argument made once more: a derived value is a stage, not a loop.

A dark theme was asked for because the screen is watched for hours, in
rehearsal as much as on stage. Status without colour was asked for because the
room will hold people who cannot tell the stale orange from the fresh blue, and
a mark only some of the audience can see is B-008 failing for them.

One call is the specification author's rather than the person's, proposed here
for the person to confirm or reverse in review, because B-031 cannot be written
without it: **subtracting a vehicle's last contact from the observed
instant, to show its age, is display formatting** under B-019, and is the only
arithmetic over instants a view model may perform. The alternative was a stage
publishing an age per vehicle per tick, which is a timer the tracker would hold
while nothing is bound — `fleet-pipeline` B-004's teardown, defeated for a label.

## Rejected

- **Keep the grid and add columns.** Cheaper, and every column B-007 needs
  already comes from the description. It does not show a trail, and a grid wide
  enough to show distance, place and age beside the existing four is the
  compactness the person asked to be rid of.
- **A map of the whole fleet as the main view.** It shows where everything is
  and hides everything else — the counts, the stale mark, the readouts — and it
  is the most expensive way to prove a changeset arrived (`aircraft-source` § 5
  row 8). One vehicle's trail, in the detail pane, is the map the brief asked
  for.
- **A light theme, or a switch between two.** Two palettes are two sets of
  contrast and colour-vision checks, for a demo run on one machine the presenter
  controls; § 5 row 5 keeps them out.
- **Reverse geocoding over a network** for the place. It spends a second
  provider's budget on every vehicle on every poll, and fails on exactly the
  stage network the replay source exists to survive. `fleet-pipeline` B-038
  compiles a table in instead, and its § 11 row 8 asks which.

## Affects

- **§ 3**: B-006 and B-008 amended, "row" to "card", and B-008's mark to B-032's
  rule; B-029 – B-037 added.
- **§ 2**: needs 7 – 9 added.
- **§ 4**: rows 12 – 14 added — the map package, drawn surfaces proved by
  review, and nothing ticking between arrivals.
- **§ 5**: row 3 narrowed from "the map view" to a map of the whole fleet; row 5
  narrowed to a light theme, a theme switch and accessibility beyond B-032 and
  B-033.
- **§ 11**: rows 7 and 8 opened — whether anything may count down, and whether a
  readout shows its change.
- **`fleet-pipeline`**: B-032 – B-040, § 4 rows 15 – 17, § 5 rows 3 and 10 – 12,
  § 11 rows 7 and 8.
- **`aircraft-source`**: B-054 and B-055, § 4 row 21, § 5 row 14.
