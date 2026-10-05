---
title: "Lesson 0002: An inherited rule is not a decision"
description: "A pattern skill's ban on a mocking framework became a claim, an analyzer rule and a test suite over a test double, and the record of why said only that two documents disagreed; the first person to ask why could not be answered."
type: lesson
---

# Lesson 0002: An inherited rule is not a decision

**Date:** 2026-10-05
**Kind:** process

## Symptom

`0004` reached the first test above the contract and put § 11 row 4 to the
person, as that row said to. The person did not answer the question it asked.
They asked why the hand-written fake had been decided in the first place, and
said they had not weighed in.

Nothing in the repository could answer them. § 4 row 18 said two documents
disagreed and that the conflict was "resolved narrowly". B-010 said what the
double **SHALL** be. § Scoring and `TRN0018`'s message both said what the hazard
was. None of the four said who chose, when, or against what. The only record of
the choice is one paragraph in commit 31dda56, which states the conflict and the
resolution in a single sentence and argues neither.

By then the rule had grown: a claim in § 3, a § 9 row, a constraint row, a
§ 6 concern-separation row, a testability finding in § 8, a claim in a second
Feature (`boundary-analyzer` B-012), an analyzer rule, three analyzer tests with
their test data, a hand-written fake, a generated fixture for it, and two xUnit
tests whose subject was the fake. Thirteen places, from one imported sentence.

## Root cause

A rule that arrives from outside the repository was recorded as a **conflict**
rather than as a **decision**. The two are stored differently here and that is
not cosmetic: a decision gets a record under `decisions/` or an ADR, with the
options it was chosen over and the reasoning; a conflict row in § 4 gets a
source and an impact. Writing the import into § 4 made it look settled while
leaving no answer to "why", so every later document cited the claim and none
cited a reason.

The hazard the repository now gave as the reason — a double returning `default`
on an unset call makes every test above it pass for the wrong reason — is real,
and it was written **after** the rule was in place, in § Scoring and in the
analyzer's message. That is the shape worth recognising: a rule with no recorded
reason accumulates one, because each document that cites it has to say something
about it, and what gets written is a justification rather than a history. It
then reads exactly like a decision anyone would make again.

Two signals were available before the person asked. The claim's own source cell
pointed at a skill rather than at a record in this repository. And B-010 was the
only claim here whose § 9 row named a test of a test double — the cost of a rule
showing up as a test with no product behaviour under it.

## Spec delta

B-010 is **Withdrawn**, both halves: the ban on a mocking framework and the
throw-on-unset requirement. NSubstitute stands in at the contract seam as it
does everywhere else, and `OpenSkyApiFake`, its fixture and its two tests are
deleted. § 4 row 18 now records that the conflict resolves toward this
repository's conventions, and says the pattern's testing rule governs nothing
here. § 4 row 19, § 6's row, § 8's DI-seam finding, § 9's counts and
§ Scoring all follow.

It crosses into `boundary-analyzer`: B-012 is Withdrawn there, `TRN0018` is
retired from the analyzer and from its § 7 mapping and code-fix tables, and
[ADR-0006](../../../../.spec/adr/0006-an-analyzer-enforces-the-layer-boundaries.md)
is amended from eighteen assigned rules to seventeen. The id stays in
`AnalyzerReleases.Unshipped.md` so nothing reuses it.

## Process delta

[`spec-and-traceability`](../../../../.skills/spec-and-traceability/SKILL.md)
carries the rule this lesson produced: a constraint imported from outside the
repository is recorded with what it costs here and who accepted that cost, or it
is a decision and gets a decision record. A § 4 row whose Source names only an
external document is the signal, and the question to ask of it is not "is this
rule good" but "did anyone here choose it".

## Claim

- B-010 — Withdrawn; no § 9 row, by the template's rule for a withdrawn claim.
