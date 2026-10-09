---
title: "Lesson 0025: A test plan in the spec is the test file twice"
description: "fleet-pipeline § 8 grew a list of planned tests per item — names, values, fixtures — that repeated § 9 and the test files; PR #63's review asked whether it belonged in the spec, and it did not."
type: lesson
---

# Lesson 0025: A test plan in the spec is the test file twice

**Date:** 2026-10-08
**Kind:** process

## Symptom

PR #63's review pointed at the 80 lines of `0063`'s planned tests in
fleet-pipeline § 8 and asked: "Does this belong in the spec?" The same shape
was already there for `0062` and `0064`, headed "planned and delivered": every
test name, every expected value, every fixture builder, beside § 9 naming the
same tests and the test files holding the same values.

## Root cause

The template's § 8 asks for a testability assessment and the classes of
scenario. The short "What `00xx` proved" paragraphs written after delivery
(`0031` – `0059`) fit that. On 2026-10-08 the spec-author began writing the
plan _before_ delivery, so `spec-reviewer` could check that each claim had a
case a wrong implementation would fail — and it worked: the review of `0063`
found B-035's "in force when the point is added" untestable by reading that
list. Nobody decided the list belonged in § 8; it was copied from `0062` to
`0064` to `0063` because the first one had been useful.

What was useful was the falsifying case, not the list. `spec-and-traceability`
gave the spec "testing strategy" and barred "test plans" from the item, and
said nothing about where a test plan does go — so it went to the only record
left, and became a third copy of what § 9 and the test file hold.

## Spec delta

fleet-pipeline § 8's three blocks are cut to the "proved" form the earlier
items use: the arrangement, why, and what none of the tests proves. Their
falsifying cases are nine new scenarios — B-032 (a lost position, the
distance off the equator), B-034 (a lost fix spanned), B-035 (the threshold in
force), B-037 (a second description, none yet), B-042 (cell precision, no
delta, display units) — and B-036's existing scenario now says a role is the
column itself. The reviews `0063` owes, B-034's "no store beside it" and
B-037's "no altitude on the base", moved to their § 9 rows. Sixty-five
scenarios.

## Claim

Not applicable — a process rule about where content lives. The gate is
review, and now `spec-and-traceability`'s `Never add`.

## Skill

`spec-and-traceability` § "Claims and traceability": "The testing strategy is a
strategy, not a list of tests", with the home of each part, and a `Never add`
entry for a list of planned tests.
