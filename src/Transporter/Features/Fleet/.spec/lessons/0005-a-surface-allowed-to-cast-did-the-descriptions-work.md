---
title: "Lesson 0005: a surface allowed to cast did the description's work"
description: "PR #69's review: the detail pane's view model switched on Aircraft, labelled its fields, converted units DisplayUnit already owned, and was driven by side effects in two setters; the exception that allowed the cast was the cause, and the setter rule was missing."
type: lesson
---

# Lesson 0005: a surface allowed to cast did the description's work

**Item:** [`0038`](../../.issue/0038-detail-pane-and-summary.yml) · **Claims:** B-013, B-019, B-022, `fleet-pipeline` B-043 ·
**Found by:** the person, reviewing pull request #69 on 2026-10-09
**Kind:** spec gap

## Symptom

Three review comments on `FleetDetailViewModel` and `FleetViewModel`:

- The pane's projection — a `switch` on `Aircraft`, fourteen labels, and
  `Feet`, `Knots` and `FeetPerMinute` converters — "is either domain logic
  hiding in the ViewModel … or it's presentation logic hidden in the ViewModel.
  Either way, this doesn't belong here!"
- `FleetViewModel.Selected`'s setter also assigned `Detail.Vehicle` — "setters
  with multiple sets is a code smell."
- `FleetDetailViewModel.Vehicle`'s setter raised three other properties —
  "Setters with side effects are smells. There has to be an Rx approved way to
  do this."

## Root cause

The specification invited the first. B-022 and ADR-0005 item 6 made the detail
pane "the one surface allowed to care which kind of item it is showing", and
`mvvm` listed "a canonical unit into a display unit" among what belongs in a
view model. So the pane was built as the one place that knew an aircraft, and
everything that knowledge implies — which fields exist, what they are called,
which unit each reads in — followed it into the view model. `DisplayUnit`
already did those conversions for the card, in the description, where a swap
replaces them; the pane did them a second time, with a second rounding.

The setters had no rule against them. `mvvm` said a value the view model only
derives is read through `AsValue`, and `Rows`, `Title` and `IsEmpty` were
computed on read rather than assigned, so they looked derived — but their change
notifications were raised from a setter, and the setter was pushed by another
setter. The derivation was real; its trigger was a side effect.

## Spec delta

`fleet-pipeline` B-043 is added: the description names the detail pane's lines.
`fleet-dashboard` B-013 reads the pane from them, and B-022 loses its detail-pane
exception. ADR-0005 item 6 moves the one legitimate cast from the pane to a
source's own description, which `fleet-pipeline` B-022 already allowed.

## Skill

[`mvvm`](../../../../../../.skills/mvvm/SKILL.md) now says a setter sets its own
field and nothing else, that a dependent value observes the property through
`WhenChanged` and is read through `AsValue`, and that a unit conversion or a
`switch` on a subclass is the description's, not a view model's.
[`maui-ui`](../../../../../../.skills/maui-ui/SKILL.md) and
[`domain-model`](../../../../../../.skills/domain-model/SKILL.md) lose the
detail-pane exception.

Three setters predate the rule and break it: `FleetViewModel.SearchText` and
`SelectedFilter` call `Filter()`, and `SelectedGrouping` calls `GroupBy`. They
are named here so the rule is not read as already met; changing them is a
behaviour-preserving refactor of B-009, B-011 and B-012 that belongs to its own
item.
