---
title: "Lesson 0024: Query syntax hides the operator"
description: "PR #62 composed Options with LINQ query syntax, following two earlier files; the language-ext skill asked for Bind and Map by name and never said the query form was out, so precedent won."
type: lesson
---

# Lesson 0024: Query syntax hides the operator

**Date:** 2026-10-08
**Kind:** process

## Symptom

PR #62 (`0064`) composed three `Option`s in `FleetReadout.Change` and two in the
readout delta with `from … in … select`. The review asked one word: "LINQ
syntax?" The same form was already in `FleetMovement` (`0062`) and
`AircraftSnapshotMapper`, so it had been copied twice before anyone looked.

## Root cause

`language-ext-usage` § "Keep it readable" says to prefer `Match`, `IfNone`, `Map`
and `Bind`, and to pick the plainer form where a reader would need to look up an
operator. It never says query syntax is the form to avoid, so a reader of the
code took the two existing uses as the convention. A rule stated only as a
preference loses to a precedent in the tree.

Query syntax over an `Option` is the case the skill was reaching for: to the
line-of-business audience this repository teaches, `from x in` reads as a
collection query, and which operator runs — `SelectMany`, here meaning `Bind` —
is hidden behind compiler translation.

## Spec delta

None. No claim is about how an `Option` is composed. Behaviour is unchanged:
the three sites were rewritten with `Bind`, `Map` and `Filter`, and fleet-pipeline
B-032, B-042 and aircraft-source B-036 keep their tests, which pass unchanged.

## Claim

Not applicable — a style rule, not a behaviour. The gate is review, and now the
skill's `Never add` list.

## Skill

`language-ext-usage`: a "Method form, not query syntax" bullet under "Keep it
readable", and query syntax over `Option` or `Either` added to "Never add".
