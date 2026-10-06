---
title: "Lesson 0001: a view that subscribes with += outlives what it subscribed to"
description: "The fleet page rebuilt its columns from a PropertyChanged handler it never removed; the generated Events() observable is the form that disposes."
type: lesson
---

# Lesson 0001: a view that subscribes with `+=` outlives what it subscribed to

**Date:** 2026-10-06
**Kind:** process

## Symptom

`0036`'s first `FleetPage` rebuilt its header and row template whenever the
description changed, and reached that change through

```csharp
viewModel.PropertyChanged += (_, arguments) => { … };
```

Found in review of pull request #31, not by a run: nothing fails, nothing is
reported, and the page is the application's only one, so the leak it names has
no symptom on a machine the presenter controls. It is the demo teaching a
pattern that costs an application with navigation a page per visit.

## Root cause

`maui-ui` said what a view may _know_ and said nothing about how a view
_subscribes_, so the handler read as ordinary MAUI. The repository had no
counter-example either — the template's page bound through XAML and subscribed
to nothing — which is how a convention that nobody wrote down gets established
by the first file to need it.

## Spec delta

No claim changed: B-001 and B-007 are about what the page shows and where its
columns come from, and both are still proved the same way. What moved is the
rule — `maui-ui` § "The UI reads; it never drives" now says a view subscribes
through the generated `Events()` observable and disposes it when its handler
goes, and `mvvm` § "Projecting state back" spells the disposal as
`.DisposeWith()` rather than leaving it to each author.

## Claim

- B-007 — `FleetViewModelTests.GivenADescription_WhenTheColumnsAreRead_ThenTheyAreTheDescriptionsInItsOrder`, unchanged: the view model's half is what executes, and the page's half is B-001's review.

## Skill

[`maui-ui`](../../../../../../.skills/maui-ui/SKILL.md) § "The UI reads; it never
drives" and its `Never add` list: no `+=` in a view, and a subscription ends
with the handler. [`mvvm`](../../../../../../.skills/mvvm/SKILL.md) § "Projecting
state back": `.DisposeWith(…)` is how a subscription is kept.
