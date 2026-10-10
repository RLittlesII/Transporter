---
title: "Lesson 0010: A status nobody is present to change does not change"
description: "Four merged items sat at in-review or in-progress because the rule put `done` after the merge, which is the one moment the branch, the worktree and the agent are all gone."
type: lesson
---

# Lesson 0010: A status nobody is present to change does not change

**Date:** 2026-10-05
**Kind:** process

## Symptom

Asked what to do next after pull requests #13 and #14 merged, the honest answer
was that `0023` still read `status: in-review` with `closed: null` while its
work was on the trunk and both its claims read `Verified`. The person's reply:

> this is a flaw in your process. You should flip it to done before submitting
> a PR.

It was the fourth time. `0019` – `0022` all read `in-progress` after merging,
and #13 had to carry a housekeeping commit to correct three of them — which is
itself the evidence: the repair happened because somebody noticed later, not
because the process produced it.

## Root cause

`transporter-conventions` references/delivery.md said `done` "when it lands",
and **nothing is present when a change lands.** The squash merge deletes the
branch, the worktree comes down once the pull request opens, and the session
that did the work has reported and stopped. A step scheduled for that moment has
no owner, so it does not happen; `in-review` becomes a terminal state by
default, and the tracker disagrees with the trunk in the direction nothing
corrects.

Worth naming precisely: this is not forgetfulness. It is the same defect shape
as [lesson 0007](0007-a-gate-that-waits-on-what-it-gates-never-closes.md) — a
rule whose precondition is arranged after the only actor has left. A gate that
waits on what it gates never closes; a status change scheduled after the merge
never runs.

The second half is scope. A pull request was treated as closing the one item it
was cut for, so `0021` stayed `blocked` on B-016 and `0019` stayed
`in-progress` even in a change that finished what they were waiting on. An item
is `done` when its claims are proven, whichever pull request proved them.

## Spec delta

None. No claim changes; the delivery convention does.

## Claim

- None. A process lesson, per [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md):
  the remedy is a skill rule, and restating it as a claim would create a second
  place to drift from.

## Skill

[`transporter-conventions`](../../.skills/transporter-conventions/SKILL.md)
references/delivery.md § "The tracker". `done` and the `closed:` date are now
written **by the pull request that delivers the item, before it is opened**,
dated the day it opens — the last moment anyone is there to know. And a pull
request closes every item its work finishes, not only the one it was cut for:
a parent whose last child lands, and a sibling held `blocked` on a claim this
work proves.
