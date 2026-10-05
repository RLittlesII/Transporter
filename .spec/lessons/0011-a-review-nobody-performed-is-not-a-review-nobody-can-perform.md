---
title: "Lesson 0011: A review nobody performed is not a review nobody can perform"
description: "Two § 9 rows held 0002 blocked for a day as rows whose status could never move, and neither review had been attempted; one of them took ten minutes of reading."
type: lesson
---

# Lesson 0011: A review nobody performed is not a review nobody can perform

**Date:** 2026-10-05
**Kind:** process

## Symptom

`0002` landed in pull request #1 and still read `status: blocked` after #16,
with every claim it carried `Verified` except two. Asked what held it, the
answer given twice was that B-009 and B-049 were review obligations whose
preconditions could not be arranged — OpenSky publishes no version, and no push
provider exists — and the question put to the person was which of two bad
options to take: add a third § 9 status for a row that can never move, or accept
`blocked` as the item's terminal state. The person's reply:

> this is still marked as blocked

Both options accepted a premise nobody had tested. **Neither review had been
attempted.** B-009's took one `git log` and one diff: a single contract
interface, for the one version the provider publishes, with no change to its
declared surface since the class implemented it. That row was `Verified` on the
evidence already in the repository.

## Root cause

§ 8 of `aircraft-source` described the two rows as "the only two rows whose
Status will not move when the tests and the analyzer arrive", and that sentence
was read as a property of the claims rather than a statement about two
mechanisms that had not arrived. **A review is the third mechanism, and it is
performed by a reader** — `boundary-analyzer` § 9 performs four and records in
each row what was looked at, on which item, and what change re-does it. Nothing
about a review waits on a test or on an analyzer.

The failure is a familiar one in a new place: a gate described as unopenable was
never pushed. [Lesson 0007](0007-a-gate-that-waits-on-what-it-gates-never-closes.md)
is a gate waiting on what it gates; this is a gate waiting on nobody, because
the mechanism that opens it is a person reading code and no step said so. The
tell was the proposed remedy — a repository-wide status value, read by every
table parser in the test project, bought for one item's closure. A fix that
large against a problem that small is a sign the problem has been diagnosed
wrong.

The second half is the same finding as B-003 on #16. Where a review has a clause
naming something the repository does not contain, the row belongs to the item
that builds that thing. B-049's third clause names `ITrackerSource`, which is
`0005`'s, so the row is `0005`'s — not a row `0002` fails to close.

## Spec delta

`aircraft-source` § 8 and § 9 now say a review is performed and re-done rather
than describing two rows as permanently still; B-009 reads `Verified` with its
review recorded; B-049 moves to `0005`. § 11 row 5 records that the question's
two options were both refused. No claim text changes.

## Claim

- None. A process lesson, per [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md):
  the remedy is a skill rule.

## Skill

[`transponder-conventions`](../../.skills/transponder-conventions/SKILL.md)
references/delivery.md § "The tracker". Before reporting that an item cannot
close, **perform the reviews its rows name.** A `Missing` row naming a review is
a review nobody has done; the row records what was looked at, on which item, and
what change re-does it. A clause naming something the repository does not
contain moves the row to the item that builds it.
