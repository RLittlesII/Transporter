---
title: "Lesson 0012: A type named for the exception describes the minority"
description: "StaleVehicle was the name of the element the fleet pipeline publishes, and almost no instance of it is stale — caught in review, after the name had passed a specification review, an ADR and an implementation."
type: lesson
---

# Lesson 0012: A type named for the exception describes the minority

**Date:** 2026-10-06
**Kind:** convention

## Symptom

`src/Transponder/Tracking` § 7 declared the element its fleet stream publishes as
`StaleVehicle` — a vehicle and a derived stale mark, wrapped so a consumer binds
one collection and reads the mark off the row it already has. The name went
through the § 11 row 4 architecture review, which considered the wrapper against
two alternatives and kept it;
[ADR-0009](../adr/0009-the-pipeline-publishes-changesets-a-consumer-binds.md),
which decided the published shape; `src/Transponder/Features/Fleet` § 7, which binds
it; and `0007`, which built it.

Reading `0007`'s pull request, the person asked one question:

> Rather than `Stale` shouldn't it be `Tracked`?

A fleet of thirty-seven aircraft reporting normally is thirty-seven
`StaleVehicle` instances, none of them stale. The type is the fleet's row. Being
stale is one `bool` on it, usually `false`.

Renamed to `TrackedVehicle` in the same pull request: the type, its file, the
three § 7 sections that name it, and `0034`'s summary.

## Root cause

The name came from the problem that justified the wrapper rather than from the
thing the wrapper is. The argument in the review was about **staleness** — where
the mark may live, given that `domain-model` § "Never add" keeps it off the
domain object and the clock moves under it — and the type was named after the
argument it settled. That is the drift: a name earned in a discussion about one
member describes that member, and a reader meets the name before the
discussion.

It survived four passes because nothing those passes check is about a name. A
claim is checked for falsifiability, a § 9 row for naming a test, an ADR for its
rejected options, an implementation for the claims it cites — and none of them
asks whether every instance of a type answers to its own name. The declaration
was reviewed as a shape, and it was the right shape.

## Spec delta

`src/Transponder/Tracking` § 7 carries the new name and one paragraph recording
the rename, because the old name stays in two places on purpose: § 11 row 4's
record of the review that chose the wrapper, and `0030`'s decision rows. Those
are records of what was decided on 2026-10-05, and rewriting a record to match a
later rename is how a history stops being one.

`src/Transponder/Features/Fleet` § 4 row 8 and § 7, and
`src/Transponder/Integrations/OpenSky/.spec/README.md` § 7, name the type as
current design and were updated. `fleet-dashboard` § 9's
`GivenAStaleVehicle_WhenItsRowIsProjected_...` keeps its wording: that is English
about a stale vehicle, not a reference to the type.

## Claim

- None. No claim named the type, by design — `fleet-pipeline` § 3 names no types
  at all, which is why a rename this late cost four markdown edits and no claim
  amendment. The design that made the review cheap is the one worth noticing
  here.

## Skill

[`transponder-conventions`](../../.skills/transponder-conventions/SKILL.md)
§ `coding` gained the rule, beside the naming traps:

> **A type is named for what every instance of it is**, not for the state some
> of them carry.

It belongs in the project companion rather than in
[`coding-conventions`](../../.skills/coding-conventions/SKILL.md) because the
example is this repository's; the method skill already says a naming rule at
warning severity is a rule, and this one no analyzer can check.

The same review asked whether `Tracking/` should be
`Features/Tracking/`. It should not, and the answer was a citation rather than a
change — but the rule it cited did not quite cover the case, so
§ `coding` § "Project structure" now says what `Features/` holds and that the
tracker seam, the pipeline over it, the clock and the published element are
`Tracking/`, consumed by two Features and the replay source alike.
