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
design says where its messages are declared and why. It also says the actor is
registered under `SwapSource`: the move's own review found the registration
still keyed on `SourceSwapActor`, which left the view model one class short of
being able to reach the actor at all. `fleet-dashboard` § 7 says where the view
model reads the messages and how it resolves the actor. No claim changed: B-057
is what the actor answers and B-041 is what a view model may depend on, and
both read as they did.

## Claim

No claim changed, and no § 9 row names these tests; they guard the placement
this lesson is about.

- `FeatureAssemblyTests.GivenAMessageAViewModelNames_WhenItsDeclarationIsRead_ThenItIsInMessages`
  fails if one of the four named messages is declared outside
  `Transporter.Messages`. It holds four names, not the rule: a fifth message
  declared beside its actor passes it, and `TRN0006` is what reports that one,
  at the view model that names it.
- `SourceSwapActorTests.GivenTheTrackingActorsRegistered_WhenTheSwapActorIsResolvedByTheMessageItIsTold_ThenItAnswersTheTargets`
  fails if the actor is registered under anything but `SwapSource`.
- B-057's own three tests in `SourceSwapActorTests` are unchanged but for a
  `using`.

## Skill

[`akka-actor`](../../../../../../.skills/akka-actor/SKILL.md) § "The shape" no
longer says every message is declared beside its actor. It says one that only
the actor's own layer sends is, and that one another layer tells, asks or reads
in an answer is declared where that layer may name it — the answer and what it
carries included. Its § "Registration and consumption" says an actor another
layer resolves is registered under the message that layer tells it, not under
its own class. [`mvvm`](../../../../../../.skills/mvvm/SKILL.md) § "Talking to
an actor" resolved an actor by its class in its example; it now resolves by the
message. `transporter-conventions` references/coding.md already said where a
told message lives and that it is the key, and now says the first for a message
asked, its answer and what the answer carries.
