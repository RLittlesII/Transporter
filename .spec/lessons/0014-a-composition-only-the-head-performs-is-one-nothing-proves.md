---
title: "Lesson 0014: A composition only the head performs is one nothing proves"
description: "0036 shipped green with two recorded reviews, and the application threw building its first window: the one place that registers anything was the one place no test can reach."
type: lesson
---

# Lesson 0014: A composition only the head performs is one nothing proves

**Date:** 2026-10-06
**Kind:** process

## Symptom

`0036` merged as #31 with a green build, two recorded reviews and four
`Verified` rows. The person ran the application the same day:

```
System.InvalidOperationException: Unable to resolve service for type
'Transponder.Tracking.IFleetTracker' while attempting to activate
'Transponder.Features.Fleet.ViewModels.FleetViewModel'.
```

Not a wrong value on a screen — no screen. `AppShell`'s only content is the
page, so the failure is the whole application, and the template's page it
replaced had opened.

## Root cause

Everything the window needed was already written, public, and called by nobody.
`AddFleetTracking` and `AddOpenSky` had been composed for items each, and
`MauiProgram` registered `FleetPage` and `FleetViewModel` over a container that
held neither the tracker nor a source.

Three things were supposed to catch that, and each had been let go for a reason
that was true on its own.

**B-004 is the registration claim**, and its § 9 row reads `Missing`, routed to
[`0044`](../../features/fleet-dashboard/.issue/0044-page-level-claim-proof.yml)
because `test/UnitTests` is `net10.0` and `src/Gui` is a pair of Apple-only MAUI
heads. The reason the claim could not be proved is the same reason the defect
could not be seen: **the registration list lived where nothing executes.**

**Spike `0040` was dropped from `0036`** on the reading that no claim B-001 –
B-007 is about registration or who polls first. The second half was right and
the first was not.

**`transponder-conventions` § "Verify and publish" step 3 says to launch against
a recorded or simulated source** — and this repository has nothing to launch
against: `replay-source` (`0008` – `0013`) is unbuilt, and OpenSky wants
credentials. A step with nothing behind it is a step that does not run, which is
why a person found this and the build did not.

So the deferral was sound, the spike reading was half sound, and the launch step
was unperformable — and the application could not start. The error was treating
"no mechanism can prove this here" as a statement about proof. It was a
statement about **where the code was**, and that was the thing to change.

## Spec delta

`fleet-dashboard` § 9's B-002 review is re-performed on `0047` and names the
registrations the head now carries; § 11 row 4 records that the spike reading
cost the application its startup, and that the composition moved. B-003, B-004
and B-006 stay `0044`'s, and no claim text changes: nothing here is a new
behavior, it is the same behavior put where it can be run.

## Claim

- None. A process lesson, per [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md):
  the remedy is a skill rule.

## Skill

[`maui-ui`](../../.skills/maui-ui/SKILL.md) § "Stack and wiring". **The head
registers only what is MAUI.** Everything else — the integration, the tracker,
the view models, the schedulers — is composed in one method a test can build a
host from, and the test resolves the view model each page takes. What is left in
the head is the settings it reads, its pages and the thread that owns its
window; a registration missing from any of the rest is a red test rather than a
window that never opens.
