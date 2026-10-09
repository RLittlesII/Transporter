---
title: "Lesson 0023: A sign-off that outlives its text approves text nobody read"
description: "Three Features gained or amended claims while their § 12 rows read 🟢, and nobody reopened them, because the role that may write § 12 is the one role not present when signed text changes."
type: lesson
---

# Lesson 0023: A sign-off that outlives its text approves text nobody read

**Date:** 2026-10-08
**Kind:** process

## Symptom

`fleet-dashboard` decision 0002 put cards and a trail map in place of the grid,
and on 2026-10-08 it landed as claims in three Features at once:
`fleet-dashboard` B-029 – B-038, `fleet-pipeline` B-032 – B-042 and
`aircraft-source` B-054 and B-055, with amendments to claims already there.
Every one of the three specifications read `spec_status: approved` with all
three § 12 rows 🟢 while it happened, and went on reading that way after the
pull request was open.

Asked what came next, the answer given was that § 12 was not yet signed. It
was signed — over text that had changed underneath it. The rows had to be
reopened by hand once that was noticed. Until then, every item cut from the
new claims read as startable against a sign-off nobody had given.

The review that followed showed a second gap. It was of a specification with no
code, and the reviewer's contract asked of a diff only: its question, its
reading list and every row it told the reviewer to look for assumed one. The
findings that blocked `0062` — a § 8 plan whose distance cases held latitude
fixed, a leg with no defined value when a position goes absent — had no row to
be found under, and the verdict the person's waiver needed, that § 7 covered
`0062`'s claims and no others, had no place in the report.

## Root cause

**The only role that may write § 12 is the only one not present when signed
text changes.** The specification author amends §§ 1-5, the implementer § 7,
the test writer § 8, and section ownership forbids each of them to touch § 12.
The reviewer writes § 12 only when asked to review. Nothing asks when a change
lands on a section that was already approved, so the row stays where it was. It
is the shape of [lesson 0010](0010-a-status-nobody-is-present-to-change-does-not-change.md):
a state change scheduled for a moment with no actor in it.

Ownership was written to stop a role raising a row it had no standing to raise,
and it was right to. It said nothing about lowering one, and the absence read as
a prohibition.

The second half is that the reviewer's contract was written for the review that
existed when it was — a pull request delivering an item against claims already
agreed. A Feature's specification is now reviewed on its own, before any item
is cut from it, and the contract never caught up.

## Spec delta

None. No claim changes; the section-ownership rule and the reviewer's contract
do.

## Claim

- None. A process lesson, per [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md):
  the remedy is a rule, and restating it as a claim would create a second place
  to drift from.

## Skill

[`transponder-conventions`](../../.skills/transponder-conventions/SKILL.md)
references/specs.md § "Section ownership": **a change to a signed section
reopens its row**, in the same commit, to 🟡 with `spec_status` back to
`in-review`. It is the one write to § 12 another role makes, and it only lowers
a row. [`feature.md`](../templates/feature.md) § 12 points there.

[`.agents/spec-reviewer.md`](../../.agents/spec-reviewer.md) checks for a
stale sign-off, carries rows for a specification reviewed before its code — a
claim no test can hold, records that disagree, a § 8 plan that cannot fail, a
claim that obligates another Feature — reports which items a 🟡 section is
complete for, and refuses to sign off a section its reviewer wrote. The item is
[`0074`](../../.issue/0074-spec-reviewer-contract.yml).
