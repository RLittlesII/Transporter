---
title: "Decision 0001: This pipeline demonstrates no AutoRefresh"
description: "The aircraft strategy projects with Transform, so no instance is mutated in place and AutoRefresh has nothing to refresh on; README's operator table records where it would apply instead."
type: decision
---

# Decision 0001: This pipeline demonstrates no AutoRefresh

**Status:** decided

**Date:** 2026-10-05
**Made by:** the person

## The call

`AutoRefresh` is not a stage in this pipeline. Staleness re-evaluates on the
observed clock's own observable (B-018), and a vehicle's value changes arrive as
changeset updates, which `Filter` and `Sort` already see. README.md's operator
table is amended to say where `AutoRefresh` would apply and why this pipeline
does not reach for it — option A of § 7's decision block, closing § 11 row 1.

## Why

`AircraftTrackerSource` projects with `Transform`, so an updated aircraft
arrives as a **new** `Aircraft` rather than a mutated one (§ 4 row 4). There is
no in-place property change to refresh on, so an `AutoRefresh` stage here would
be an operator wired to a signal that never fires — demonstrated, and a lie.

The demo's own claim is that everything downstream of the cache is an ordinary
pipeline. Bending the projection to produce a beat from the operator table would
trade that claim for the beat. Telling the audience the operator exists, where it
applies, and why this pipeline does not need it is the same lesson with the claim
intact.

## Rejected

**B — mutate the projected vehicle in place and let `AutoRefresh` re-evaluate.**
Would demonstrate the operator the table promises. Rejected: the strategy becomes
stateful, `Transform` gains a lookup of what it previously emitted, and a
vehicle's identity stops arriving together with its values — three structural
costs for one demo beat, in the Feature whose point is that the structure is
ordinary.

**C — option A here, `AutoRefresh` in the closing act**, where a push source
genuinely mutates what it already holds. Honest and complete, and not foreclosed
by this record: README.md § "Open items" has not chosen the closing act's second
source, so an operator cannot be deferred to it. If that act lands with a
mutating push source, demonstrating `AutoRefresh` there is that Feature's call
and needs nothing reversed here.

## Affects

- B-018 — the clock's observable is the re-evaluation trigger, which is what
  removes the need this option set was weighing. No claim text changes.
- § 4 row 4 and § 7's decision block — the block is answered; its owner records
  the outcome in § 7.
- README.md § "DynamicData operators" — the `AutoRefresh` row now says where it
  applies and that this pipeline's projection gives it no subject.
- § 11 row 1 — answered.

## Reversal

Not applicable — this record has not been reversed.
