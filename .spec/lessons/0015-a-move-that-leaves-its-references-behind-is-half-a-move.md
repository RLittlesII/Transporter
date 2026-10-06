---
title: "Lesson 0015: A move that leaves its references behind is half a move"
description: "One specification was moved beside its code and sixty-nine references were left pointing at where it had been, because the move was made as a judgement call and never became a rule."
type: lesson
---

# Lesson 0015: A move that leaves its references behind is half a move

**Date:** 2026-10-06
**Kind:** process

## Symptom

[1563e5b](https://github.com/RLittlesII/Transponder/commit/1563e5b) moved
`aircraft-source`'s specification and items out of `features/` and into the
integration they describe, `src/Transponder/Integrations/OpenSky/`. A day later
the repository held sixty-nine references to where they had been.

- **Forty-four relative links inside the moved tree** resolved one directory
  short. `features/aircraft-source/.spec/` is three directories deep and
  `src/Transponder/Integrations/OpenSky/.spec/` is four, so every `../../../`
  in the specification and its four lessons — to the root ADRs, the templates,
  the skills, `Directory.Packages.props`, and nineteen source files — pointed
  one level above its target. The specification's own § 7 type table, where a
  reader goes to find the code, was the largest block of them.
- **Twenty-five references in fourteen files outside it** — `README.md`, five
  root ADRs, a root lesson, the `item.yml` schema comment, two skills and the
  runbook — still named `features/aircraft-source/...`, which had become a
  directory that does not exist.
- **`transponder-conventions` still taught `features/<slug>/`** as the home for
  a specification, its items and its records. The one place that would have
  made the move a convention was the one place the move did not touch, so the
  next specification written would have landed in the directory the last one
  was moved out of — and three more specifications were still sitting there.

Nothing failed. No build broke, no test went red, and a reader following a link
got a 404 on GitHub or a missing file locally, silently, one reader at a time.

## Root cause

**A directory move was treated as a file operation rather than as a change of
address.** `git mv` moves bytes; it does not move the links into the bytes, the
links out of them, or the rule that said where they went. The commit that did
the move is correct about everything it touched and silent about everything
that pointed at what it touched.

Two properties of this repository make that silence cost more than it would
elsewhere. Every record is cross-linked by relative path — that is what makes
the specification chain navigable at all — so the blast radius of a move is the
whole web of records, not the moved directory. And a relative link is depth
sensitive: a tree moved to a different depth breaks every link that escapes it
even when both ends still exist, which is the half nobody thinks to check,
because the files are all right there.

The deeper cause is that the move was a **judgement call that was never
promoted to a rule**. One Feature's specification was put beside its code
because that was plainly better; the convention describing where specifications
live was left describing the old layout. A judgement call that is right applies
to every Feature, and the only way it reaches the next one is the skill.

## Spec delta

No product delta — nothing the application does changed, and no § 3 claim
moves.

The delta is to the conventions, and it has two halves, because the lesson has
two halves:

1. **Where a specification lives.** A Feature's `.spec/` and `.issue/` sit in
   `<home>`, the directory holding the code that implements it.
   `features/<slug>/` is where a specification waits for code — the only home a
   Feature with no implementation has — and it is not a permanent address.
2. **What a move owes.** The change that moves a specification repoints every
   reference to it and re-depths every relative link inside it. A move that
   does one and not the other is half a move.

Item `0048` performed both for the three specifications still in `features/`
and repaired 1563e5b's sixty-nine references. `replay-source` stays in
`features/` and moves when it is built.

## Claim

- None. A process lesson with no product delta owes no claim.

The guarantee here is not a test but a check that can be run: resolve every
relative link and every path-shaped reference in the repository and assert the
target exists. Both halves of this lesson are a file that does not exist at the
end of a link, which is why the sixty-nine were countable at all.

## Skill

[`transponder-conventions`](../../.skills/transponder-conventions/SKILL.md)
carries both halves —
[`specs`](../../.skills/transponder-conventions/references/specs.md) § "Where
things live" replaces `features/<slug>/` with `<home>` throughout, names each
Feature's current one, and states what a move owes:

> **`features/<slug>/` is where a specification waits for its code**, and the
> only home a Feature with no implementation has. It is not a permanent
> address: the specification and its `.issue/` move to `<home>` in the change
> that builds the code, and the move repoints every reference to them,
> re-depthing the relative links inside the files that moved.

and [`delivery`](../../.skills/transponder-conventions/references/delivery.md)
§ "The tracker" points an item at `<home>/.issue/` rather than at
`features/<slug>/.issue/`.
