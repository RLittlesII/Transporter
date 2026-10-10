---
title: "Lesson 0017: A written convention with no gate is a preference"
description: "The comment rule was written down, in the skill every change loads, and 0006 broke it across nine files anyway — because nothing reads prose, and a reviewer is the only thing that does."
type: lesson
---

# Lesson 0017: A written convention with no gate is a preference

**Date:** 2026-10-06
**Kind:** convention

## Symptom

`0006`'s pull request was reviewed with one finding, on ten lines across seven
files: "every change in this file is a maintenance burden. It's either covered
by analysis, or it should be. We shouldn't need all the additional comments."

The comments it named were paragraph-length `<remarks>` blocks, `<para>` pairs
inside them, and inline prose above registration calls that restated the call.
Several carried claim and record identifiers in the middle of a sentence —
`(B-034, ADR-0002 item 6)`.

`transporter-conventions` § "The rules that bind every change" already said:

> **Terse, and XML over inline.** A `<summary>` is one line, `<remarks>` one
> sentence and only for what the code cannot state, an inline comment one line
> saying a _why_. Reasoning belongs in the specification, an ADR or a lesson —
> cite instead of explaining.

That file loads whole on activation, and it loaded. The rule was read and the
work was done anyway.

## Root cause

**Every other rule in that skill has a gate, and this one does not.** The naming
rules are `.editorconfig` severities. The layer rules are `TRN0001` – `TRN0017`,
which fail the build at the line. The format rules are `dotnet format`. The item
rules are a `commit-msg` hook. A change that breaks any of them stops.

A change that writes four paragraphs of `<remarks>` builds, formats, passes 134
tests and opens. Nothing between the author and the reviewer reads prose, so the
rule's only enforcement is a person who notices — and what a person notices is a
review's worth of attention spent on something a diagnostic would have spent
none on.

The second half is that writing it felt like following a different rule. Each
paragraph deleted in the fix had a defensible reason: it named the claim, it
recorded the option rejected, it said why the operator was that one. All of that
belongs in the specification, the ADR or the item's `decisions` — and all of it
_was_ in one of those, which is what makes the comment a second copy rather than
a record. The author had just written the record, so the sentence was fresh, and
putting it in both places felt like thoroughness.

## Spec delta

- `transporter-conventions` § `coding` carries the rule in its own words, with
  the two shapes the review named — prose restating the line, and an identifier
  inside comment prose — and the test that settles it: delete the comment and
  name what a reader loses.
- [`0051`](../../.issue/0051-verbose-comment-diagnostic.yml) is the gate, under
  `boundary-analyzer`: a diagnostic for a `<summary>` or `<remarks>` past its
  sentence count. The identifier half is `ready-for-architecture` rather than
  decided, because the reviewer raised it as a suspicion and a bare citation on
  a line that needs one is the form that should survive.
- [`0050`](../../.issue/0050-scrub-comment-maintenance-burden.yml) is the scrub.
  The repository was written under the same drift, and one item's files are the
  ones a review happened to look at.

## The rule

**A convention nothing executes is a preference, and it is enforced by whoever
is reading.** So when a rule is written down, the same change says what reads it
— an `.editorconfig` severity, an analyzer, a hook, a test — or says, in the
rule's own text, that it has no gate and a review is it.

The point is not that prose rules are worthless. It is that their cost is paid
by a reviewer, every time, forever, and that cost is invisible when the rule is
written and obvious when the review arrives with ten comments on one subject.
A rule worth a reviewer's attention twice is worth a diagnostic.

This is [lesson 0003](0003-a-dedupe-is-a-move-and-a-move-has-a-destination.md)'s
shape one level up: there, a rule in two places drifted; here, a rule in one
place with no enforcement never bound at all.
