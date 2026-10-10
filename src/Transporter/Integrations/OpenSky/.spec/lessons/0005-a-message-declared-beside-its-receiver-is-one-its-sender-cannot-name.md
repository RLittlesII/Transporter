---
title: "Lesson 0005: A message declared beside its receiver is one its sender cannot name"
description: "0080 declared the swap actor's four messages beside the actor, as the actor skill said to, in a namespace the boundary analyzer forbids a view model to name — and the view model is what sends them."
type: lesson
---

# Lesson 0005: A message declared beside its receiver is one its sender cannot name

**Date:** 2026-10-09
**Kind:** process

## Symptom

Nothing failed. `0080` built `GetSwapTargets`, `SwapTargets`, `SwapTarget` and
`SwapSource` in `Transporter.Tracking.Sources`, its three tests passed, § 12
was signed and the item closed. `fleet-dashboard` § 7 then designed
`FleetViewModel` to ask the first, read the next two and tell the last.

The `spec-reviewer`'s re-read of `fleet-dashboard` on 2026-10-09 read that
design against `BoundaryAnalyzer`: `TRN0006` reports a view model that names a
concrete class under `Transporter.Tracking` which `IFleetTracker` does not
publish. `0039`, built exactly as designed, would have been reported by the
rule its own claim B-020 relies on. It was found one item before the build
would have found it.

## Root cause

**Two written rules gave two answers, and the design read one of them.**
`akka-actor` said "Messages are `internal`, immutable, and declared beside the
actor." `transporter-conventions` references/coding.md § "Project structure"
said a message a consumer tells an actor lives in `src/Transporter/Messages/`,
and names `TRN0006` as the reason. The second was written on 2026-10-07 for
`DemandPoll`; the first was never amended to give way to it.

Two things kept it from being seen. The actor's half and the view model's half
of one exchange are designed in two Features' § 7, so each was reviewed without
the other's constraint in front of it. And three of the four types are not
"told" at all — one is asked, one is the answer, one is carried in the answer —
so the convention's sentence, read literally, covers `SwapSource` alone.

## Spec delta

§ 7's type table gives the four files under `Messages/`, and the swap actor's
design says where its messages are declared and why. `fleet-dashboard` § 7 says
where the view model reads them. No claim changed: B-057 is what the actor
answers and B-041 is what a view model may depend on, and both read as they
did.

## Claim

- B-041 and `fleet-dashboard` B-020, preserved —
  `FeatureAssemblyTests.GivenAMessageAViewModelNames_WhenItsDeclarationIsRead_ThenItIsInMessages`,
  which fails if any of the four is declared outside `Transporter.Messages`.
- B-057, preserved — `SourceSwapActorTests`, unchanged but for a `using`.

## Skill

[`akka-actor`](../../../../../../.skills/akka-actor/SKILL.md) § "The shape" no
longer says every message is declared beside its actor. It says one that only
the actor's own layer sends is, and that one another layer tells, asks or reads
in an answer is declared where that layer may name it — the answer and what it
carries included. `transporter-conventions` references/coding.md already says
where that is here, and now says it for an answer as well as for a message
told.
