---
title: "Decision 0001: Houston is the bounding box, and the interval starts at 15 seconds"
description: "Poll a box covering IAH, HOU, the ship channel and Galveston Bay at a configurable 15-second interval — one geography that serves both the aircraft demo and the vessel closing act."
type: decision
---

# Decision 0001: Houston is the bounding box, and the interval starts at 15 seconds

**Status:** decided

**Date:** 2026-10-04
**Made by:** project owner

## The call

Poll a bounding box over Houston — roughly latitude 28.8 to 30.4, longitude
-96.0 to -94.2 — covering both international airports (IAH and HOU), the
Houston Ship Channel and Galveston Bay. The polling interval is configurable and
starts at 15 seconds. The box is configuration too, with no default compiled in
(B-050).

## Why

**One geography serves both fleets.** The box contains two busy airports *and*
one of the busiest ports in the United States, so the vessel closing act
subscribes AISStream to the same coordinates. The swap becomes "same box, same
grid, different fleet" rather than also moving the audience to a different part
of the world — which is the point being demonstrated, so anything that muddies
it costs more than it looks like it does.

**It is alive whenever the talk happens.** Houston is in the presenter's own
timezone. A European box has better origin-country diversity and denser traffic,
but is a night sky during a United States afternoon, which would make the live
demo depend on the talk's slot.

**Origin-country grouping still works.** The first concern about a United States
metro box was that `Group` by origin country would be monotone. IAH is a United
hub with heavy Latin American traffic plus European and Gulf carriers, so the
grouped view gets Mexico, Panama, Colombia, Germany, the United Kingdom, the
Netherlands, Qatar and Turkey. Not London, but not one bar either.

**The interval is no longer a budget decision.** This reverses an assumption
carried through several drafts of the specification. At 15 seconds a poll runs
240 times an hour, and a box under 25 square degrees costs 1 credit, so an hour
costs 240 credits against a registered account's 4,000 per day. A 45-minute talk
spends about 180. The Houston box is roughly 2.9 square degrees — comfortably
inside the 1-credit tier — so **credits bound the box, not the interval**, and
rehearsal is effectively unconstrained. The earlier framing ("box and interval
are one credit-budget decision") was true at the 10-second interval the README
sketched and a box large enough to leave the first tier; it is not true here.

15 seconds also sits above the registered tier's 5-second resolution limit with
room to spare, so a user lowering it in configuration cannot accidentally
out-run the provider.

## Rejected

**A European box** — London South-East or the Benelux/Rhine corridor. Denser
traffic and much better origin-country diversity. Rejected because it is quiet
or dark during likely talk hours, which would turn the live demo into a
dependency on scheduling, and because it separates the aircraft geography from
the port the closing act needs.

**A larger box for more traffic.** Anything past 25 square degrees doubles the
credit cost per poll and buys aircraft that are nowhere near the two airports
the story is about. The 1-credit tier is a real ceiling and this box is nowhere
near it.

**Picking the box at rehearsal.** Defensible, since the box is configuration
either way. Rejected because the vessel subscription has to match it, and
leaving both open would mean two things to decide later instead of none.

## Affects

- B-050 — the interval's default and the box being configuration.
- § 4 row 8 — the credit arithmetic, restated at 15 seconds.
- `README.md` § "Open items" — "Pick the bounding box and polling interval" is
  now done.
- `ais-stream` — the vessel subscription uses this same box, which is the
  reason the geography was chosen.

## Reversal

Not applicable — this record has not been reversed.
