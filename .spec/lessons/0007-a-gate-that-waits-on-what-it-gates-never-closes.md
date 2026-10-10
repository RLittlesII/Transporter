---
title: "Lesson 0007: A gate that waits on what it gates never closes"
description: "Three items were implemented against a specification nobody had signed, because approval required § 9 to have no Missing row, § 9's rows named tests that only the implementation could write, and nothing in the working loop ever reads § 12."
type: lesson
---

# Lesson 0007: A gate that waits on what it gates never closes

**Date:** 2026-10-04
**Kind:** process

## Symptom

The person, on finding four stacked pull requests under an unmerged
specification:

> I wonder how we have full implementation before I EVER approved the SPEC?!

At that point `src/Transporter.Analyzers/.spec/README.md` carried
`spec_status: draft`, all three § 12 rows read 🟡 Draft, and § 11 held three
open questions — while its `0020`, `0021` and `0022` were implemented, tested
and pushed as pull requests #5, #6 and #7, with `0019` – `0022` marked
`in-progress`.

It is not new to that Feature.
[`aircraft-source`](../../src/Transporter/Integrations/OpenSky/.spec/README.md) and
[`replay-source`](../../features/replay-source/.spec/README.md) are both
`spec_status: draft` as well, and `aircraft-source`'s `0002` and `0003` were
built, reviewed and merged against the draft in pull requests #1 and #2. The
pattern was already in `main`; three items in a row made it loud enough to ask
about.

## Root cause

Two causes, and only the second is about anyone's judgment.

**The gate could not be satisfied in the order it asked for.** § 12 said
`spec-reviewer` flips `spec_status` to `approved` "when every row above is 🟢
and § 9 has no `Missing` row". § 9 is the coverage matrix: a row leaves
`Missing` when the test it names passes. So approval waited on coverage,
coverage waited on the implementation, and the implementation was supposed to
wait on approval. For most Features that circle is merely uncomfortable — the
work goes ahead and the document is signed afterwards, which is what `0002` and
`0003` did. For a Feature whose § 9 names _its own_ tests, an analyzer that
enforces claims, the circle is closed: that specification could never have been
approved before being built, no matter who was being careful.

**Nothing in the working loop reads § 12.** The person chose "spec first" at
the outset and then said "start 0020 and 0021". The instruction was theirs; the
omission was the assistant's — it had just written the § 12 rows and did not
say, in the one line it would have taken, that they were 🟡 and that starting
meant implementing against an unsigned agreement. An item's `status` moved to
`in-progress` with nothing anywhere asking what its specification's sign-off
said.

## Spec delta

§ 12 of [`.spec/templates/feature.md`](../templates/feature.md) drops the § 9
clause from the approval gate. `approved` now means every § 12 row is 🟢 — the
sections are written and agreed. A `Missing` row in § 9 no longer holds
approval back, because § 9 is the **ship** gate and a separate one: it blocks
an item reaching `done`, not the agreement reaching `approved`. Both sentences
survive; they were one sentence, and that was the defect.

`aircraft-source` and `replay-source` § 12 stop restating the rule and point at
the template instead, so the change has one store and the copies cannot drift
from it — [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md).

No § 3 claim follows. Nothing about what the software does has changed.

## Claim

- None. A process lesson with no product delta owes no claim, following
  [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md).

## Skill

[`transporter-conventions`](../../.skills/transporter-conventions/SKILL.md)
gained it in § `deliver-change` → "The tracker", beside the line that makes
`in-progress` the mark an item is taken — the two belong together, because that
is the moment the question goes unasked:

> **An item does not move to `in-progress` while its specification's § 12 rows
> are 🟡.** Unsigned design sections mean there is nothing agreed to implement
> against, and the next reader cannot tell which parts were decided and which
> were invented on the way. The person may waive it for a **named item** — not
> for a Feature, and not standing — and the waiver is a row in that item's
> `decisions`.

The lesson holds the incident; the skill holds the rule. A reader reaches for
the skill, and the gate's own definition stays in the template that owns it.
