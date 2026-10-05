---
title: "Lesson 0003: A dedupe is a move, and a move has a destination"
description: "Removing a duplicate on the grounds that another document owns it deleted the only copy, because nobody opened the document named as the owner."
type: lesson
---

# Lesson 0003: A dedupe is a move, and a move has a destination

**Date:** 2026-10-04
**Kind:** process

## Symptom

Writing § 7 of the aircraft specification required a `| Field | Type | Notes |`
table for `Aircraft`, which meant naming what it inherits from
`TransportVehicle`. No document in the repository said what `TransportVehicle`
carries.

[`domain-model`](../../.skills/domain-model/SKILL.md) had held a member table
naming five — key, position, last contact, label, grouping key. It was removed
eight commits earlier by `0e47d2c`, whose message reads "leave the base's
members to the record that shaped it" and names
[ADR-0005](../adr/0005-an-abstract-base-carries-the-tracked-item.md) as that
record. ADR-0005 states seven rules about the base and names no member of it.

The table did not move to ADR-0005. It left the repository, and the commit that
removed it said where it had gone.

## Root cause

The dedupe confirmed that a duplicate existed. It did not confirm that the
keeper held the content.

The overlap was real and large. The removed table's six bullets restated
ADR-0005's Decision closely enough to quote it — "written once, tested once",
"no subclass can produce a keyless item", "the detail pane is the only place" —
so reading the two side by side, the whole block looked like one document
repeating another. It was a restatement of the rules **plus** five member names
the rules never gave. Deleting the block removed the overlap and the remainder
together, and the remainder was the only copy.

Nothing detected it. A missing fact has no failing test, no broken link and no
`Missing` row: ADR-0005 reads as complete, because rules that name no member
look exactly like rules that have no members to name. The next reader to need
one was a section nobody had written yet, which is why eight commits passed
first.

The near miss is the part worth keeping. Had § 7 been written without checking
`git log`, the natural move would have been to name the members in the Feature
specification — putting a repository-wide base class's surface in one Feature's
document, which is
[lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md)'s failure in the
opposite direction. A dedupe that loses its content does not merely lose it; it
invites the next writer to re-create it in a worse place, with every appearance
of having chosen that place deliberately.

This generalizes past skills. The rule the dedupe was applying —
`coding-conventions` § "Skills", a fact belongs to one file — is correct, and
applying it is what every commit in that pass was for. What the rule does not
say is that removing a copy is only half a move. The other half is arriving.

## Spec delta

[ADR-0005](../adr/0005-an-abstract-base-carries-the-tracked-item.md) gained
§ "The members": the five members, their types, and which are abstract.

Three of them the rules genuinely did not reach, so this is not purely a
restoration:

- `Position` is on the base rather than per-source, because both sources report
  one for some items and none for others — which is item 5's own test for
  shared rather than hoisted.
- `IsStale` takes the instant as a parameter rather than holding a clock. Item
  1 says the derivation runs "against an injected clock", and the clock is
  injected into the fleet tracker that owns it; a model type holding a service
  is the first step toward one that references the container.
- Item 2's grouping key does not survive contact with a real source. Aircraft
  have two grouping dimensions a view would plausibly use, and one abstract
  member cannot answer both, so it is recorded as a question rather than
  written as a member.

That the restoration could not be mechanical is itself evidence: the deleted
table was not a copy.

## Claim

- None. A process lesson with no product delta owes no claim, following
  [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md).

## Skill

[`coding-conventions`](../../.skills/coding-conventions/SKILL.md) § "Skills"
gained the rule, beside the one it is the other half of:

> **Removing a copy is half a move.** Before deleting content because another
> document owns it, open that document and find the content in it. A commit
> message naming the owner is not a transfer, and a cite is not a copy. What
> survives a dedupe is whatever the keeper actually says — and a document whose
> rules name no particulars reads exactly like one with no particulars to name,
> so nothing downstream reports the loss.

It belongs there rather than in a new section because § "Skills" already states
that a fact belongs to one file. That rule is what motivates every dedupe, and
this is the failure mode of obeying it — which makes the two neighbours, not
separate concerns.

No other skill changed. The rule is general to any document pair, and
`coding-conventions` is the method skill that applies to every change.
