---
title: "Lesson 0021: An arrangement written by hand is a fixture nobody wrote"
description: "Four tests constructed Aircraft inline and shared a hardcoded instant between them; the convention already said what varies comes from cases, and said nothing about where a domain value comes from."
type: lesson
---

# Lesson 0021: An arrangement written by hand is a fixture nobody wrote

**Date:** 2026-10-07
**Kind:** process

## Symptom

Pull request #47's review read "Tests need work", with six inline comments on
three files: `ClassMemberData?` against two `private static readonly` members in
`FleetSearchTests`, `ClassMemberData?` against three private factory helpers in
`FleetViewModelTests`, and `AutoFixture?` against three hand-built `Aircraft` in
`FleetSourceDescriptionTests`. The tests passed, the build was green, and the
claims they proved were the right claims.

## Root cause

Two failures, and only one of them is the convention's.

**Mine.** `transponder-conventions` references/testing.md already said "**What
varies comes from `[ClassData]` or `[MemberData]`** — a `TheoryData<…>` subclass
in a `*Cases.cs` file beside the tests". The repository had eight `*Cases.cs`
files and ten `[ClassData]` call sites when I wrote `[InlineData]` seven times.
Nothing was unwritten; I wrote the tests before reading the reference for the
thing I was writing. The cost was a review round on a rule a reader could have
pointed at.

**The convention's.** The same bullet ends "A private helper may still build
**data** a case needs (a domain value, a cache the test writes to)", which reads
as permission to construct a domain type by hand — and that is what the three
`Aircraft` in `FleetSourceDescriptionTests` were. The rule said where a _double_
comes from and where a _varying value_ comes from, and left where a _domain
value_ comes from unsaid, so two readings were available and I took the cheap
one. `AircraftSnapshotFixture` and `SchedulerProviderFixture` existed; the type
I needed one for did not have one, and adding one was never framed as the
obligation it is.

The second failure is the one worth the lesson. Hand-constructing a domain type
in an arrangement spreads its shape across every test that names it: four files
repeated `new Aircraft(key, LastContact) { … }`, and two of them carried their
own hardcoded `LastContact` constant so the same instant was written twice with
different values. A required member added to `Aircraft` would have broken all of
them, each needing the same edit.

## Spec delta

None. No claim changed, no scenario moved, and the four claims the tests prove
— B-009 – B-012 — read exactly as they did. This is a lesson about how the
proof is written, not about what is claimed.

## Claim

- B-010 — `FleetSearchTests.GivenSearchText_WhenItIsMatched_ThenMatchingIsCaseInsensitiveTrimmedAndEmptyMatchesEverything`,
  now a `[ClassData(typeof(SearchTextCases))]` theory whose seven cases carry
  their own reasons, over an `AircraftFixture` and a `FleetSourceDescriptionFixture`.
- B-011 — `FleetSearchTests.GivenASearchAndAFilter_WhenEitherIsCleared_ThenTheOtherStillApplies`,
  one `[Fact]` asserting seven things replaced by seven cases in
  `ComposedPredicateCases`.

## Skill

`transponder-conventions` references/testing.md, in the bullet that already
held the `[ClassData]` rule: a domain value a helper builds comes from that
type's fixture, a type with no fixture gets one rather than a constructor call
in a test, and a shared constant feeding those constructors goes into the
fixture's defaults. Three fixtures were added in the same change —
`AircraftFixture`, `TrackedVehicleFixture`, `FleetSourceDescriptionFixture` —
so the rule is written and reachable rather than written and unaffordable.
