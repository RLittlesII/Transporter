---
title: "Lesson 0009: A stack drains into its base, not into main"
description: "Two reviewed pull requests merged into their base branches and never reached main, because a five-deep stack only auto-retargets one level and nobody re-checked the bases after the lower merges."
type: lesson
---

# Lesson 0009: A stack drains into its base, not into main

**Date:** 2026-10-04
**Kind:** process

## Symptom

The person, after pulling `main`:

> ah, so there were more stacked work that hadn't been pull requested? You
> should have said that. I had no idea what you were doing ...

`main` held #3, #4, #5, #6 and #9. #7 and #8 were open, reviewed and merged —
into `0021/diagnostic-surface` and `0022/reference-rules`, their base branches.
Six reference rules, twelve `Layers` predicates, six tests and six
`aircraft-source` § 9 rows were merged, absent from `main`, and carried by no
open pull request. Every screen read as done.

## Root cause

**The stack was five deep and only one level retargets itself.** GitHub moves a
pull request's base to `main` when the branch below it is deleted — the one
directly above, and no further. #7 pointed at `0021` and #8 at `0022`; when #5
and #6 merged, nothing moved #7 or #8, and merging them then did exactly what
they said.

**The stack did not need to be that deep.** Of the four, only `0021` genuinely
depended on its predecessor: `0020` stood up the project and `0021` wrote the
first rule that proves the project loaded. `0022`'s rules and the
`aircraft-source` traceability edit each touched files nothing else was
touching, and both could have branched off `main`.

**And the assistant watched the wrong thing.** It checked that each pull request
was mergeable and told the person to delete each branch on merge, without once
re-reading the bases after the lower two landed. Then, finding the gap, it began
merging and pushing rather than saying so — which is how the person learned of
it.

## Spec delta

None. No claim, no section; the shape of the work changed, not what it does.

## Claim

- None. A process lesson with no product delta owes no claim, following
  [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md).

## Skill

[`transponder-conventions`](../../.skills/transponder-conventions/SKILL.md)
§ `deliver-change` → "Worktree and branch":

> **Branch off `main`. If the work can be done off `main`, it is.** Stack only
> where the later work cannot compile or be tested without the earlier — not
> because the items are related, read better in order, or were written in one
> sitting. One dependent level at most.
>
> **A dependent pull request is retargeted to `main` the moment its base
> merges.**

Two deep is recoverable by hand; five is a queue nobody is watching.
