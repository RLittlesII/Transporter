---
title: "Lesson 0005: A ruling is not a rule"
description: "A design choice was put to the person, answered, recorded, built and tested — and then reversed, because the standing convention the answer should have come from was written nowhere and nobody asked for it."
type: lesson
---

# Lesson 0005: A ruling is not a rule

**Date:** 2026-10-04
**Kind:** process

## Symptom

The OpenSky contract's return shape was settled by putting three options to the
person. The answer — `Task<Either<OpenSkyThrottled, OpenSkyStatesResponse>>` —
went into § 7 with its reasoning, into `0002`'s acceptance criteria, into two
Mermaid diagrams, and then into `IOpenSkyApi`, `OpenSkyHttpApi`, the
hand-written fake and three tests. All of it passed.

Reading the committed signature afterwards, the person said it looked wrong,
and then named why: **an API contract is the I/O boundary and is allowed to
throw; the Failure/Result track starts once a value is back inside the
application.** No document in the repository said that. A sweep for it found
nothing in any skill, specification or record.

The contract, the transport, the fake and three tests were rewritten, and
[ADR-0008](../adr/0008-the-contract-is-the-boundary-and-may-throw.md) now
carries the rule.

## Root cause

The question asked which shape to use, and it got a shape. Nobody asked which
rule the shape had to satisfy.

That is not a small distinction, because the rule and the ruling fail
differently. A ruling is checkable only by the person who gave it: the next
reader finds a recorded choice with three rejected alternatives and no way to
tell whether it still holds. A rule is checkable by anyone, and it decides the
cases nobody has asked about yet — here, the replay transport's return type and
every provider contract after this one.

Two things made it worse rather than caused it:

- **The one written line that touched the subject pointed the other way.**
  [`language-ext-usage`](../../.skills/language-ext-usage/SKILL.md) listed "a
  throttle response" among the cases for `Either`, and § 7 cited exactly that
  sentence as its justification. The design was not unsupported; it was
  supported by the wrong document, which is harder to notice than no support at
  all. That line is now corrected, and it is the line the rule should have been
  sitting beside all along.
- **The hazard was recorded and then walked past.** § Scoring had already
  written, when `0002`'s risk went from 3 to 4, that the contract "returns
  `Either`, which is a shape the pattern's reviewers will not have seen on a
  governed interface." That is the objection, in the document, before a line of
  the contract was written. It was filed as a score and never turned into a
  question. A hazard nobody acts on is a note.

The cost was bounded only by luck: the contract was two days old and nothing
consumed it yet. The same miss against a seam with callers would have been a
migration.

## Spec delta

[ADR-0008](../adr/0008-the-contract-is-the-boundary-and-may-throw.md) records
the rule repository-wide, with the three rejected shapes.
[`api-contract`](../../.skills/api-contract/SKILL.md) gained § "The boundary
throws" and one `Never add` line;
[`language-ext-usage`](../../.skills/language-ext-usage/SKILL.md) lost the
throttle from its `Either` examples and gained the boundary carve-out.

§ 7's paragraph now cites ADR-0008 instead of arguing the shape, and names the
superseded answer so the next reader finds out what was tried. B-028's own
wording — it bans an exception without saying where, while its scenario bans
one only at the subscriber — went to § 11 question 2 rather than being quietly
read the convenient way.

## Claim

- None. A process lesson with no product delta owes no claim, following
  [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md).

## Skill

[`clarify-requirements`](../../.skills/clarify-requirements/SKILL.md)
§ "Decide or ask" gained it, directly above the rule against resolving an
ambiguity by assumption — because this is the same failure with the person's
answer standing in for the assumption:

> **Ask what rule the answer follows, not only which option wins.** A ruling
> recorded with no rule behind it is one the next reader cannot check, and a
> standing convention the person holds but has never written down will
> contradict it sooner or later — usually after the code is built. Where the
> answer comes from a rule, the rule is the thing to write down; the ruling
> then follows from it instead of standing on its own.

It belongs in that skill rather than in `deliver-change`, because the failure
is in how the question was framed, not in how the answer was delivered. The
skill already says "an answer that lives only in the conversation was not
recorded"; this is the case where the answer *was* recorded and the thing that
produced it was not.

No rule is added about acting on a recorded hazard.
[`spec-and-traceability`](../../.skills/spec-and-traceability/SKILL.md) already
says a risk rationale must "name the specific wrong answer and the claim it
breaks" so that it "becomes an instruction instead of a restatement of the
score" — which that row did, correctly, and the instruction was still not
followed. A second rule saying to read the first one would be the thing this
repository's lessons keep warning against.
