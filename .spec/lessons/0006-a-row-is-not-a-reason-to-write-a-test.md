---
title: "Lesson 0006: A row is not a reason to write a test"
description: "Four claims about structure got xUnit tests because the traceability matrix gives every claim a row with a test name, even though a decision record had already said the mechanism for structural claims is an analyzer — and the first correction moved only the three that had been commented on."
type: lesson
---

# Lesson 0006: A row is not a reason to write a test

**Date:** 2026-10-04
**Kind:** process

## Symptom

Review of pull request #1 rejected three tests in one pass:

> We should not use reflection to test concerns. These are brittle low value
> tests

> Reflection makes it harder to reason about what is _actually_ being tested.

The three were the ones that asserted over a type object rather than over a
value the code computed — the contract's method list and parameter order for
B-005, the envelope's member names for B-002, and the double's assembly for
B-010's "not produced by a mocking framework" clause. All three had been
reported `Verified`.

A fourth followed in the next exchange. B-008's test resolved the contract out
of a built container and asserted that the implementation type did not resolve
and that one alias existed. It read as behaviour — a container _doing_
something — which is why the first correction left it alone. It is not: what
B-008 forbids is a registration shape, and asserting a shape by building the
thing and looking at it is the same test in a costume.

## Root cause

The matrix is written before the tests exist, and it gives every claim a row
with a test name in it. Writing § 9 therefore asks, once per claim, "what is
this claim's test called?", and for a claim about structure the only answer
that fits the column is a test that reads the structure. The row produced the
test.

**The correction repeated the mistake it was correcting.** The first pass
moved exactly the three claims that had comments on them and left B-008, which
is the same kind of claim, because no one had pointed at it. That is the root
cause below, applied a second time by the person who had just written it down.

Three more followed when `0003` was built: B-014, B-030 and B-031 had named
tests reading `WhenItsMembersAreInspected` and `WhenItIsInspected`, which is
the shape in the method name. Those were moved before being written, which is
the only time this costs nothing.

The decision that should have stopped it was already in the repository and
already made the argument.
[ADR-0006](../adr/0006-an-analyzer-enforces-the-layer-boundaries.md) says the
mechanism for a claim about what may _name_ what is a compiler diagnostic, and
names this exact failure in its rejected-options table: _"A § 9 row claiming a
reference rule is proven when only its signatures are is the fake-gate failure
[lesson 0002] is about."_ § 9 applied that to the eleven claims the ADR listed
and to no others — so B-002, B-005, B-008 and B-010's second clause, which are
the same kind of claim, got xUnit names instead. The ADR's examples were read
as its scope, and then the review's examples were read as theirs.

The `Verified` status is the part that cost something. Such a test does run
and does pass, so the gate reported four claims proven when what had been
checked was that a declaration, or a container built from one, still had the
shape someone typed. Nothing
downstream could tell the difference, which is the failure
[lesson 0002](0002-metadata-about-a-rule-drifts-too.md) exists for, arrived at
from a new direction.

## Spec delta

§ 9 moves B-002, B-005, B-008 and B-010's mocking-framework clause to the
analyzer, and B-014, B-030 and B-031 followed when `0003` reached them, so
eighteen rows now wait on it rather than eleven. `Verified` drops
from five rows to one — B-001, the only claim proven by what the code does
rather than by how it is declared or registered.

Deleting B-008's test left nothing exercising `AddOpenSky` at runtime, because
B-008 constrains the registration's shape and not its outcome. B-052 was
written for the outcome — the container the application constructs resolves
`IFleetTracker` with every decorator applied — and assigned to `0007`, where a
whole chain first exists to resolve. Finding the gap is the better half of
this lesson: moving a claim to the mechanism that fits it is what exposed that
no claim covered the other thing at all.

B-010 keeps its xUnit test for the half that _is_ behaviour, the throw on an
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
