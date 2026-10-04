---
title: "Decision 0002: A busy indicator covers the swap, then the new fleet arrives"
description: "On a source swap the grid shows a busy indicator until the incoming strategy's first changeset lands, then fills — rather than draining the outgoing fleet or sitting empty."
type: decision
---

# Decision 0002: A busy indicator covers the swap, then the new fleet arrives

**Status:** decided

**Date:** 2026-10-04
**Made by:** project owner

## The call

When the live source is swapped, the view shows a busy indicator until the
incoming strategy's first changeset lands, and then the new fleet appears. A
clean cut, with the gap acknowledged rather than hidden.

## Why

Per-strategy caches mean a swap is not a clear-and-refill: the outgoing cache
simply stops being read, and there is a gap of one poll — up to 15 seconds —
before the incoming strategy has anything to emit. Something has to occupy that
gap, and an empty grid reads as a crash to anyone not following closely.

A busy indicator says "this is a transition, not a failure", which is exactly
what the audience needs to be told at the one moment the demo is making its
point. The claim being demonstrated is that *nothing downstream changed*, so the
gap should look deliberate and the refill should look instant.

## Rejected

**Letting the outgoing fleet drain by expiry.** Planes and ships would coexist
for a few seconds, which is dramatic and would showcase expiry. Rejected twice
over: B-051 decides a stale vehicle is marked and kept rather than removed, so
there is no expiry to drain with; and a mixed fleet reads as a bug to anyone who
blinked.

**An empty grid with no indicator.** Fewer moving parts, and honest. Rejected
because a few seconds of nothing, on a projector, in front of people, is
indistinguishable from a broken demo.

**Pre-connecting the incoming source so there is no gap.** Would remove the
problem rather than dress it. Rejected for now because it means two sources
running and two sets of credits or sockets live at once, which is a bigger
change than the gap is a problem — and `hot-swap-source` already lists
"swapping before the ships feed has authenticated" as a failure mode to
rehearse.

## Affects

- § 5 row 7 — the indicator's *rendering* stays out of scope here; this records
  the behavior it must express, not the control.
- `hot-swap-source` § "What the audience sees on swap" — the table now records
  this as the chosen option.
- B-051 — the rejected drain option depended on expiry, which that claim
  forecloses.

## Reversal

Not applicable — this record has not been reversed.
