---
title: "Lesson 0006: a picker that lists its choices and binds none"
description: "The filter and grouping pickers bound ItemsSource and no selection, and nothing called ChooseColumn after the cards replaced the grid, so three demo gestures were inert while their claims read Verified from view-model tests."
type: lesson
---

# Lesson 0006: a picker that lists its choices and binds none

**Item:** [`0090`](../../.issue/0090-wire-the-dashboard-choosers.yml) · **Claims:** B-001, B-011, B-012 ·
**Found by:** reading `FleetPage.Controls` against the view model on 2026-10-15
**Kind:** spec gap

## Symptom

On the running head the Filter and Group by pickers listed the description's
choices, and choosing one changed nothing. No control sorted the cards at all.

## Root cause

`0037` delivered the inputs as view-model members and proved them with tests
that set the members directly, which is all a test can reach: the page is in
the head, and no test references it (ADR-0016). B-001's review, performed on
`0036`, counted the controls on the page and chose from none of them. So the
half of each gesture only a reader can check — that the control writes the
member — had no row asking for it. `SelectedFilter` and `SelectedGrouping` were
`Option`s, which a picker cannot bind, so the gap was also one the types made
easy. When `0070` replaced the grid with cards, the header tap that called
`ChooseColumn` went with it, and nothing said a sort surface was now missing.

## Spec delta

B-001's scenario adds that choosing a filter, a grouping or a sort on the
running page changes the fleet, and its § 9 row re-opens as a review owed on
`0090`. B-011 and B-012's scenarios name the control. § 7 makes the two
choices plain references and adds `SortColumn`, `ReverseCommand` and
`ClearFilterCommand`.

## Skill

[`maui-ui`](../../../../../../.skills/maui-ui/SKILL.md) § "Testing" now says a
view-model input is delivered only when the page binds it, and the review that
proves the page performs the gesture rather than counting the control.
