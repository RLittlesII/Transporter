---
title: "Lesson 0006: A row is not a reason to write a test"
description: "Three claims about structure got xUnit tests because the traceability matrix gives every claim a row with a test name, even though a decision record had already said the mechanism for structural claims is an analyzer."
type: lesson
---

# Lesson 0006: A row is not a reason to write a test

**Date:** 2026-10-04
**Kind:** process

## Symptom

Review of pull request #1 rejected three tests in one pass:

> We should not use reflection to test concerns. These are brittle low value
> tests

> Reflection makes it harder to reason about what is *actually* being tested.

The three were the only ones in the suite that asserted over a type object
rather than over a value the code computed — the contract's method list and
parameter order for B-005, the envelope's member names for B-002, and the
double's assembly for B-010's "not produced by a mocking framework" clause.
All three had been reported `Verified`.

## Root cause

The matrix is written before the tests exist, and it gives every claim a row
with a test name in it. Writing § 9 therefore asks fifty-one times "what is
this claim's test called?", and for a claim about structure the only answer
that fits the column is a test that reads the structure. The row produced the
test.

The decision that should have stopped it was already in the repository and
already made the argument.
[ADR-0006](../adr/0006-an-analyzer-enforces-the-layer-boundaries.md) says the
mechanism for a claim about what may *name* what is a compiler diagnostic, and
names this exact failure in its rejected-options table: *"A § 9 row claiming a
reference rule is proven when only its signatures are is the fake-gate failure
[lesson 0002] is about."* § 9 applied that to the eleven claims the ADR listed
and to no others — so B-002, B-005 and B-010's second clause, which are the
same kind of claim, got xUnit names instead. The ADR's examples were read as
its scope.

The `Verified` status is the part that cost something. A reflection test does
run and does pass, so the gate reported three claims proven when what had been
checked was that a declaration still had the shape someone typed. Nothing
downstream could tell the difference, which is the failure
[lesson 0002](0002-metadata-about-a-rule-drifts-too.md) exists for, arrived at
from a new direction.

## Spec delta

§ 9 moves B-002, B-005 and B-010's mocking-framework clause to the analyzer,
so fourteen rows now wait on it rather than eleven. `Verified` drops from five
rows to two — B-001 and B-008, the two proven by what the code does rather
than by how it is declared.

B-010 keeps its xUnit test for the half that *is* behaviour, the throw on an
unset response, and reads `Missing` because the other half names a mechanism
that does not exist. § 9 now states that rule where a reader meets it: a row is
`Verified` when **every** mechanism it names passes, which is also why B-037
has always read `Missing`.

§ 8 gains the reason such a test is neither of the two mechanisms it
describes, and why looking like the first is what let it stand in for the
second.

## Claim

- None. A process lesson with no product delta owes no claim, following
  [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md).

## Skill

[`test-from-scenarios`](../../.skills/test-from-scenarios/SKILL.md) gained it
in § "Write from the scenario, not the conversation", immediately above the
rule that a test asserting nothing proves nothing — the two are the same
failure at different volumes:

> **A test that reads a declaration is not a test.** Asserting over a type
> object, a member list, a parameter order or an assembly checks the shape of
> the code rather than anything it does. It breaks on a rename that changed no
> behaviour, it passes on a body that does the wrong thing, and when it fails
> it names a missing member instead of a broken rule — so a reader has to
> reconstruct what the rule was. Where a claim really is about structure, the
> mechanism is a compiler diagnostic; where the project has none yet, the
> coverage row says so rather than naming a test that asserts the shape.

The last clause is the one that answers this incident rather than merely
describing it. Without it the rule says what not to write and leaves the
column still demanding a name, which is the pressure that produced these three
in the first place.

No rule is added saying "apply a decision to every claim of its kind rather
than to the ones it listed". That is a reading failure, not a missing
convention, and a repository that writes a rule every time someone reads one
too narrowly ends up with rules nobody reads.
