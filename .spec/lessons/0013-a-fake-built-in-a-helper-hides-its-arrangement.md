---
title: "Lesson 0013: A fake built in a helper hides its arrangement"
description: "Private helpers that returned configured substitutes moved the arrangement out of the test and set up shared state xUnit's per-test construction exists to prevent; cases belong in TheoryData."
type: lesson
---

# Lesson 0013: A fake built in a helper hides its arrangement

**Date:** 2026-10-06
**Found by:** the person, reviewing pull request 29 (`0032`)

## Symptom

`FleetSortTests` and `FleetTrackerTests` each carried private helpers —
`Tracker(cache)`, `Over(cache)`, `Swapping(live)` — that built an
`ITrackerSource` substitute, configured its `Connect()` and handed it back. Every
test read `WithSource(Over(cache))` and said nothing about what the seam had been
told to do. The tests passed, and the review stopped them anyway.

## Root cause

Two problems, one shape. The arrangement left the test: a reader met
`Over(cache)` and had to go elsewhere to learn that the seam returns the cache's
changesets, which is the only interesting thing about it. And the helper is a
single definition every test depends on, so the first test that needs the double
configured differently either changes it for everybody or adds a second helper
beside it. Nothing was shared yet — the pitfall is the next caller, and by then
the pattern is the file's convention.

The matching defect was in the data: the sort test fixed two aircraft inline and
asserted one column's order, where the description offers three sortable columns
and no case covered the other two.

## Spec delta

None. No claim changed; B-009's text had already been amended in the same pull
request for an unrelated reason, and this review changed how its test is written
rather than what it proves.

## The rule

`transporter-conventions` references/testing.md now carries it: a fake is
configured in the test that uses it, what varies comes from `[ClassData]` or
`[MemberData]` with a `TheoryData<…>` subclass in a `*Cases.cs` file, and a
private helper may build data but never a double.

`SortedColumnCases` is what it looks like applied: one case per sortable column
the description offers, with the two aircraft built so no two columns agree — so
a comparer wired to the wrong column fails a case instead of passing by
coincidence. That is coverage the single hand-written case did not have, which is
the second reason the rule is worth the lines it costs.
