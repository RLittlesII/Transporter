---
title: "Lesson 0008: An item that cannot be worked alone is a decomposition defect"
description: "Four items went in-progress at once because they were cut on what a rule examines — a boundary in the analyzer's code — rather than on what one person can take, finish and review by itself."
type: lesson
---

# Lesson 0008: An item that cannot be worked alone is a decomposition defect

**Date:** 2026-10-04
**Kind:** process

## Symptom

Review of pull request #6, on `src/Transporter.Analyzers/.issue/0019`:

> The items got moved to in-progress at once. This seems like this should be the
> exception not the rule. That we should define the items in a way that we can
> actually work them one at a time. Learn the lesson. No direct action to take.

`0019`, `0020`, `0021` and `0022` all read `status: in-progress` in the same
pull request stack, and `0020` – `0022` landed as three pull requests opened
within the hour.

## Root cause

**The items were cut on a boundary in the code, not on a unit of work.** The
Feature's `## Tasks` says it plainly: the children are "cut on what a rule has
to look at rather than on § 3's four groups" — a reference rule, a declaration
rule and a call-site rule are three registrations against one compilation. That
is a true statement about the analyzer and a poor one about delivery. Nothing
in `0020` is demonstrable on its own: `transponder-conventions` says a green
build does not prove an analyzer loaded, so `0020`'s own acceptance criterion
could not be met until `0021` wrote a rule that fires. `0021` in turn could not
prove B-004 or B-005 without that rule. Two items, one indivisible piece of
work, which is why both were open at once.

**And nothing said how many may be taken.** `in-progress` is documented as the
mark that an item is taken, with a rule against picking up an item that already
carries it — but no rule against carrying four at once yourself. The limit was
assumed rather than written, so it held only while someone remembered it.

The second cause is the one worth keeping: a dependency this tight is visible
at decomposition time. `0020`'s criteria named `0021`'s rule before either was
built.

## Spec delta

None. § 3's claims are unchanged, and the reviewer asked for no restructuring —
`0020` – `0022` stay as cut, now with the cost recorded here. The next Feature
is where the rule below applies, and `epic-decomposition`'s slicing is what it
feeds.

## Claim

- None. A process lesson with no product delta owes no claim, following
  [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md).

## Skill

[`transponder-conventions`](../../.skills/transponder-conventions/SKILL.md)
gained it in § `deliver-change` → "The tracker", beside the rule that an item
does not start against unsigned sign-off rows — both are about taking work that
is not ready to be taken:

> **One item `in-progress` at a time, per Feature.** Two at once is the
> exception and says so in the item's `decisions`. An item that cannot be
> finished, demonstrated and reviewed without a sibling is not a small item; it
> is a decomposition defect, and the remedy is to merge it with the sibling or
> re-cut both — not to carry both.

The lesson holds the incident; the skill holds the rule.
