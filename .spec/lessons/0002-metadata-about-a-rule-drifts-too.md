---
title: "Lesson 0002: Metadata about a rule drifts the way a fact does"
description: "Lesson 0001 banned a skill restating a fact; it left untouched a document restating a rule, and a derived field stored a second time by hand. Both drifted, and the drift was invisible because each copy read as authoritative."
type: lesson
---

# Lesson 0002: Metadata about a rule drifts the way a fact does

**Date:** 2026-10-04
**Kind:** process

## Symptom

Four contradictions, none of which any check could have caught, because every
copy involved read as authoritative:

- Two different commands for regenerating the CI workflow. `AGENTS.md` said
  `nuke --generate-configuration …`; [`nuke-build`](../../.skills/nuke-build/SKILL.md)
  said `./build.sh --generate-configuration …`. An agent reading the entry point
  ran a command that is not this repository's entry point.
- Two cites to sections that do not exist. The aircraft specification's § 5
  routed two repository-wide undecided questions to
  [`spec-and-traceability`](../../.skills/spec-and-traceability/SKILL.md)
  § "Decisions this demo still owes a record". There is no such section and no
  such list; both questions were open in `README.md` § "Open items" all along.
- § 9 Traceability Matrix — the declared ship gate — reporting `Verified` for
  B-001 in both specifications, against 51 and 27 claims and no tests at all.
  It was the template's example row, left in place when the file was copied.
- Three stored `rank` values that disagreed with the formula deriving them, and
  a `priority: med` that is not a value in the vocabulary.

## Root cause

[Lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md) scoped its rule to
a **skill** restating a **fact**. Two things fall outside that scope and were
left standing:

**A document that copies instruction copies it whole.** Every per-section
authoring comment in `.spec/templates/feature.md` was copied verbatim into both
feature specifications — around fifty lines each — along with the
`{{placeholder}}` rows the comments tell the author to replace. The copies then
reworded themselves: the template's "so link each id to its own file" became
"so each id links to its own file" in one spec. Three copies, three wordings,
and nothing to say which governed. This is also how
`transponder-conventions`'s claim to be "the only place the mapping is written"
became false in seven other places while still reading as true.

**Documenting a mirror is not removing one.** The template said outright that
§ 12's Overall row is what flips `spec_status`, and that `priority` and `rank`
are derived. Writing the dependency down made it look managed. It was still two
places to write one answer, and the hand-written one went wrong — which is the
ordinary outcome, not bad luck. The formula that derives `rank` was itself
stated once, inside one Feature's specification, so the two items that could not
see it were the two that disagreed with it.

A fact restated in the wrong place is wrong when the fact changes. A **rule**
restated in the wrong place is wrong when the *rule* changes — and a rule about
where something is written changes every time the layout does, which is more
often.

## Spec delta

No product delta. The specifications lost no claim, constraint or decision;
§ 3's `Status` column and § 12's `Overall` row were removed as duplicate stores,
with § 9 and the frontmatter's `spec_status` keeping those answers.

Two things the specifications now state that they did not: that §§ 6-9 are
unwritten, in words and naming the role that owes each, rather than by leaving a
`{{placeholder}}` row that reads as content; and that § 9 standing empty **is**
the gate, which is why every item sits at `ready-for-architecture`.

## Claim

- None. A process lesson with no product delta owes no claim, following
  [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md).

## Skill

[`coding-conventions`](../../.skills/coding-conventions/SKILL.md) already holds
the rule this generalizes; what changed are the documents that were outside its
reach:

- [`.spec/templates/feature.md`](../../.spec/templates/feature.md) states that
  its per-section guidance belongs to the template and is reduced to a one-line
  pointer on copy, the way a `{{placeholder}}` is replaced. The pointer names no
  owner, because the section-to-owner mapping is
  [`transponder-conventions`](../../.skills/transponder-conventions/SKILL.md)'s.
- [`.spec/templates/item.yml`](../../.spec/templates/item.yml) carries the
  `rank` formula, its thresholds and its bucket vocabulary beside the fields
  they derive, and the positive shape of a criterion that cites rather than
  restates.
- `AGENTS.md` is a router: each class of rule names its owner, and only the
  traps that cost real damage when missed are written inline.
- `transponder-conventions` stopped citing `AGENTS.md` as its own source. That
  loop left authority in neither file, which is what let the two regeneration
  commands coexist.

One distinction this lesson needs, because it is the reason the previous
sentence is not self-contradictory: **a lesson or an ADR may quote a rule in
full.** It is dated history, and freezing what the rule said that day is its
job — which is why lesson 0001's six-line quotation of `coding-conventions`
stays exactly as written. A skill may not, because a skill is current
instruction, and a quotation inside current instruction is a second copy
pretending to be the first.
