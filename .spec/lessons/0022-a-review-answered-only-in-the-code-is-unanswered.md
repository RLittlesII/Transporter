---
title: "Lesson 0022: A review answered only in the code is unanswered"
description: "Six review comments were fixed, committed and reported to the person in chat; none was replied to on its thread and none was resolved, because the publish method ends at opening the pull request."
type: lesson
---

# Lesson 0022: A review answered only in the code is unanswered

**Date:** 2026-10-07
**Kind:** process

## Symptom

Pull request #47 came back with a review — "Tests need work" and six inline
comments. The comments were acted on: cases classes, three fixtures, a tightened
convention, a lesson, a green build, a commit. Then the work was reported in
chat and the turn ended. On GitHub the six threads were still open, still
unanswered, and the reviewer had no way to see which of the six had been taken,
which had been taken differently than asked, or which had been pushed back on —
short of diffing a commit and inferring it. The person had to say so.

A second, smaller symptom in the same turn: the push had been failing on
GitHub 500s, so for a while the commit existed nowhere but this machine. The
review looked answered in chat and was not even published.

## Root cause

`deliver-change` references/publish.md runs: commit, open the pull request, body,
screenshot, prove it runs, tear down, watch checks. It ends there. **Nothing in
the method says what happens when a review arrives**, so the work of answering
one had no owner and no step number, and the steps that do exist all end in a
commit — which trained the habit that a commit is where a thing is finished.

Two rules were already written and still held: `transponder-conventions`
references/delivery.md says a reviewer who sends work back changes the status
with the rest of the change, and that was done. So the item's state was right
while the conversation it belonged to was abandoned. A status is not a reply.

The deeper error is the one worth naming: **a review is a conversation, and a
commit is not a turn in it.** Acting on feedback is half of answering it; the
other half is telling the person what you did, where, and where you disagreed.
Silence after a correction reads as either not having read it or having read it
and declined — and both are worse than the thing being corrected.

## Spec delta

None. No claim, scenario or § 9 row is about how a review is answered; this is
delivery method, which is where the fix goes.

## Claim

Not applicable — no claim governs this, and inventing one would put a process
rule in a specification. The gate is a reader: a pull request with open threads
nobody replied to is the visible failure.

## Skill

`deliver-change` references/publish.md, new step 10 — "Answer the review on the
review": one reply per thread saying what changed and where; resolve only the
threads actually addressed, leaving open anything pushed back on or asked about;
say which comment was a rule that already existed and which was a gap, and land
the gap in the skill that should have held it in the same change.

The companion needs no edit: the status half was already written in
`transponder-conventions` references/delivery.md and was already followed.
