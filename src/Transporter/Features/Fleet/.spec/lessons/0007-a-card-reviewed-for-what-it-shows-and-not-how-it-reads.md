---
title: "Lesson 0007: a card reviewed for what it shows and not for how it reads"
description: "Readouts clipped to a third of the card read a vertical rate as an altitude, the stale edge narrowed a stale card until a caption wrapped, and every card laid its views out twice; each review counted the cells and none read them."
type: lesson
---

# Lesson 0007: a card reviewed for what it shows and not for how it reads

**Item:** [`0092`](../../.issue/0092-whole-readouts-and-uniform-cards.yml) · **Claims:** B-006, B-008, B-029 ·
**Found by:** the demo-readiness review of 2026-10-15, on the Mac Catalyst head against the 30-minute aircraft recording
**Kind:** spec gap

## Symptom

Vertical rates read "+1,919 ft" and "−705 ft/m": the cell was cut at its
column's edge, and a cut "ft/min" reads as an altitude. A stale card stood
taller than the fresh cards beside it, so the next row of the fleet started
lower under it. At startup MAUI logged "is already a child of" for every card's
readouts, labels, badge and edge.

## Root cause

Three, in one view.

- **The readouts were three columns at a card width that holds two.** A value
  is one `NoWrap` label in the monospace face at 18; at the card's 320-point
  minimum a third of the body is about 90 points, and a signed four-digit rate
  in feet per minute is thirteen characters, about 140.
- **The stale edge took width from the body.** It sat in its own `Auto` column
  beside the body, so a stale card's body was six points narrower than a fresh
  card's, which was the difference between "VERTICAL RATE" on one line and on
  two. A status changed the card's size.
- **The frame was rebuilt around views that outlive it.** `Lay` ran from the
  constructor and again when `Card` was bound, and each run built a new header,
  body and grid and added the same title, badge, edge and readouts to them while
  they were still children of the first.

No claim asks that a cell read whole or that a status leave the card's size
alone, and the reviews of B-029, B-030 and B-008 on `0070` counted what each
card showed — the roles, the distances, the mark — without reading a value
across a row of cards. § 4 row 13 makes rendering a review, and nothing named
what that review reads.

## Spec delta

§ 3 is unchanged in `0092`: a claim added there reopens §§ 1-5, and that is the
person's to approve. § 11 row 10 asks the person for one — every readout reads
whole at the card's minimum width, and a card's status changes no card's size —
and § 10 records the incident. The fix itself is view-only: two readout
columns, the edge overlaid on the body's padding, and the frame built once with
only the readouts re-laid on a swap.
