---
title: "Decision 0002: The notice interval is live on stage; the poll interval is not"
description: "A consumer's notice cadence is changeable while the demo runs, but the OpenSky poll interval stays the provider's startup option — out of scope here, and its credit cost is not put under a live control."
type: decision
---

# Decision 0002: The notice interval is live on stage; the poll interval is not

**Status:** decided

**Date:** 2026-10-05
**Made by:** the person

## The call

The poll interval is not exposed as a live input, here or in the dashboard. It
stays `aircraft-source`'s startup option. A new § 5 row records the exclusion and
names the owner. B-026's notice interval remains live, because pacing a notice
spends nothing.

## Why

The two intervals look alike and cost differently. A notice cadence is a local
rate cap over a stream that already exists, so changing it on stage costs one
observable value. The poll interval buys data: at this bounding box, one credit
per call against 4,000 a day (README.md § "Limits"). A control that makes the
poll faster is a control that can empty the day's budget during the talk, and the
one run that matters is the one in front of people.

Keeping it a startup option also keeps the boundary this Feature is built on:
the pipeline cannot name a provider, a client or a cache (B-023), and an input
that changes how often a provider is called is the provider's, not the
pipeline's.

## Rejected

**A claim here — the pipeline exposes the poll interval with its credit cost
derived alongside.** Would show the economics on screen, which is genuinely part
of what a polled source teaches. Rejected: it widens this Feature into
`aircraft-source`'s option surface and gives the pipeline a member about calls it
is not allowed to know happen.

**Agreed as wanted, specified in `aircraft-source` as a spec delta.** Still
available later and nothing here forecloses it; rejected now because no need in
§§ 1-2 asks for it and the demo has a safer way to make the same point — stating
the budget while the poll runs at its fixed interval.

## Affects

- § 5 row 8 — new, excluding a live poll interval and naming `aircraft-source`
  as its owner.
- B-026 — unchanged; the notice interval stays an observable input.
- § 4 row 8 — the "changed live, on stage" constraint is about the notice
  cadence only, which this record makes explicit.
- § 11 row 5 — answered.

## Reversal

Not applicable — this record has not been reversed.
