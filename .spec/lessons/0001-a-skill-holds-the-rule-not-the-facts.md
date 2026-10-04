---
title: "Lesson 0001: A skill holds the rule, not the facts"
description: "Nineteen skills had absorbed the specification, the repository's current state and the product's reasons, because nothing said what a skill is for."
type: lesson
---

# Lesson 0001: A skill holds the rule, not the facts

**Date:** 2026-10-04
**Kind:** process

## Symptom

Nineteen skills, 2,651 lines, and a reader could not tell which file to believe.

- `run-the-demo` restated `README.md` §§ "Limits" and "Gotchas" — the credit
  tiers, the bounding box arithmetic, the citation, the hyperscaler block.
- `ais-stream` restated § "Closing act": the WebSocket, the key, the two client
  packages, the MMSI key.
- `api-contract` restated §§ "Response shape", "Authentication" and "Limits",
  having first said the provider's own documentation was the authority.
- `api-mock` restated `features/replay-source/.spec/README.md` and
  [ADR-0004](../adr/0004-ndjson-recording-format.md).
- `transponder-domain-model` carried field tables it introduced with the words
  "the index table in `README.md` is the contract and lives there, not
  duplicated here".

Four skills carried notes that were true the day they were written and false
afterwards: no test target, a package not yet referenced, a provisional shape,
nothing adopted yet.

Meanwhile the skills that were supposed to carry the method could not travel.
`deliver-change` held the item schema, the branch-name shape, `./build.sh` and
the claim-id prefix inline, so its discipline — which is not specific to this
repository at all — could not be read apart from this repository.

## Root cause

**Nothing said what a skill is for.** With no rule, each skill absorbed
whatever was true while it was being written: the rule, the facts the rule was
derived from, the state of the repository that day, and the reason the product
wanted it. Only the first of those four is durable, and the other three are
what grew the files and aged them.

The second-order cause is that a skill is cheap to append to. Adding a fact to
the skill in hand takes one line; finding the file that already owns the fact
and linking it takes a search. Each individual append was reasonable, which is
why the rule has to be written down rather than left to judgement.

## Spec delta

No product delta — no behavior changed, and no § 3 claim moves.

The delta is to the conventions: a skill holds the rule and links the authority
for the facts. Skills are now one of three kinds — method, technology, or the
project companion — and the facts that were in them are read from `README.md`,
a feature specification, a decision record, or a library's own documentation.

This lesson also establishes that **lessons split by blast radius exactly as
decision records do**: one that binds every feature lives here, in the root
`.spec/lessons/`, numbered repository-wide from `0001`; one scoped to a feature
stays in that feature's `.spec/lessons/`.

## Claim

- None. A process lesson with no product delta owes no claim, and inventing one
  to fill this row would be this same lesson's mistake in miniature: a fact
  written where it does not belong so that a shape looks complete.

## Skill

[`coding-conventions`](../../.skills/coding-conventions/SKILL.md) gained a
**Skills** section carrying the rule, and it is the test every skill in this
repository was then read against:

> A skill holds the rule, the trap, and the `Never add` list. Facts live where
> they are authoritative — the specification, a decision record, or the
> library's own documentation — and the skill links them. Four things never go
> in a skill: a fact restated from somewhere else in the repository, the
> current state of the repository, a project path or command in a skill that is
> not the project companion, and the reason the product wants the rule.
