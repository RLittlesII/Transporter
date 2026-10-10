---
title: "Lesson 0003: A specification that carried live code"
description: "PR #52's second review round: § 7 held the indicator's stream line by line and a paragraph walking a reader through each operator, which is a second definition of shipped code and a lesson hiding in a specification; the rule is now in transponder-conventions."
type: lesson
---

# Lesson 0003: A specification that carried live code

**Item:** [`0058`](../../.issue/0058-refresh-control.yml) · **Claim:** B-028 ·
**Found by:** the person, reviewing pull request #52 on 2026-10-07

## What happened

§ 7 carried the refresh indicator's full stream as a `csharp` block, and under it
a paragraph that walked the reader through each operator in turn — "six things are
stated by that shape". Two review comments, on the block and on the paragraph:

> Code examples don't belong in the spec once the code is live. There should be a
> lesson for that somewhere.

> This looks like lessons hiding in the spec.

Both are right, and the first already had a rule:
`transponder-conventions` § "Declarations in § 7" said a declaration is written
out **only while its file does not exist**. I read that as being about
declarations — an `interface` sketch — and not about a constructor's stream, so
the stream went in and then survived two rewrites of the same section, each of
which had to edit the copy and the code together. The second comment names the
tell: a paragraph explaining what an operator is for, operator by operator, is a
code comment or a lesson. It is not what a specification is for, and it is the
shape that makes a section grow every time the code changes.

## Why the copy is worse than redundant

The code moved three times in one pull request — `GestureCommand` to
`RxCommand`, `StartWith` into `AsValue`, `Observable.Return(true).Concat(…)` into
`StartWith(true)` — and every one of those was a second edit in § 7, applied by
hand, with nothing checking that the two agreed. A reader who trusted § 7 between
any two of those commits read a stream the repository did not have.

## The rule now written down

`transponder-conventions` § "Declarations in § 7" opens with it: no code in a
specification once the code is live, and the rule covers operator pipelines and
constructor bodies, not only declarations. What § 7 writes instead is what the
code **owes** — the behaviour, the rule, the reason an operator is the one chosen
— as claims or sentences pointing at the file. B-028's indicator is five bullets
now: what raises it, what lowers it, the cap, whose constant the cap is, and that
every scheduler is injected. No operator is named, and the file is linked.

## What it cost

One code block and one paragraph deleted, five bullets written, and the two
cross-references that pointed at "§ 7's stream" reworded. No claim changed. The
rule it violated was already written, which is the part to keep: a rule read
narrowly is a rule that does not fire.
