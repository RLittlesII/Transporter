---
title: "Lesson 0004: two written rules, each read as not applying"
description: "PR #66's review: the card page sized its grid with a += on SizeChanged and the view model assigned Observed from a subscription; both rules were already in maui-ui and mvvm, and each was read past because the case looked different from its example."
type: lesson
---

# Lesson 0004: two written rules, each read as not applying

**Item:** [`0070`](../../.issue/0070-cards-replace-the-grid.yml) · **Claims:** B-029, B-031 ·
**Found by:** the person, reviewing pull request #66 on 2026-10-08
**Kind:** process

## Symptom

Two review comments, each naming a rule the repository already had:

- `FleetPage` set the card grid's span with `fleet.SizeChanged += …`, never
  removed — "Memory Leak. Either subscribe to the source generated observable,
  or properly dispose of it."
- `FleetViewModel.Observed` was a settable property assigned from a
  `Subscribe` — "`AsValue`".

## Root cause

Neither rule was missing; each was read as being about something else.

- `maui-ui` forbids `+=` in a view, and its example is a view-model
  `PropertyChanged` ([lesson 0001](0001-a-view-that-subscribes-with-plus-equals.md)).
  A layout event on the page's own child read as outside it, because the
  child cannot outlive the page. The rule is about the form, not the lifetime
  argument, and the argument is the thing a reader should not have to make.
- `mvvm` says a value the view model only derives is read through `AsValue`,
  and `IsRefreshing` beside it already was
  ([lesson 0002](0002-a-hand-rolled-command-and-a-projected-default.md)).
  `Observed` was added next to the description's subscribe-and-assign block,
  and copied its neighbour's shape rather than the rule's.

## Spec delta

None. B-029 and B-031 are about what a card shows and where its age is
measured from, and neither changed. `Observed` still reads the minimum until
the first instant and the tracker's instant after it, which
`FleetViewModelTests.GivenADescriptionAndAnObservedInstant_WhenTheyArrive_ThenTheCardRolesAndTheInstantAreTheTrackers`
still proves.

## Skill

[`maui-ui`](../../../../../../.skills/maui-ui/SKILL.md) § "The UI reads; it never
drives" now says the `+=` rule covers any event, a child control's layout
event included, and names the page's disposal: a `CompositeDisposable`
disposed in `OnHandlerChanged` when `Handler` is null. `mvvm` already said
enough; what this lesson adds there is the reading: before adding a property
the view model only derives, look for the `AsValue` beside it, not the
`Subscribe` beside it.
