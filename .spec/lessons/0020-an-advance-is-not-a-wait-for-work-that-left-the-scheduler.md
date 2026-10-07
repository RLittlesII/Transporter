---
title: "Lesson 0020: an advance is not a wait, for work that left the scheduler"
description: "A chain that awaits tasks resumes on the thread pool, so reading state straight after AdvanceBy races it — and the late continuation schedules its next virtual wait from a later clock, so a poll loop drifts and never catches up."
type: lesson
---

# Lesson 0020: an advance is not a wait, for work that left the scheduler

**Date:** 2026-10-06
**Kind:** process

## Symptom

`0011`'s cadence test asserted that the second recorded payload is released
fifteen seconds after the first. It failed with the first payload's instant,
and the staleness test beside it failed with a vehicle that was never marked:

```
Expected clock.Current to represent the same point in time as <2026-10-04 14:32:25 +0h>
... but <2026-10-04 14:32:10 +0h> does not.
```

A probe advanced the same chain one second at a time for forty seconds and the
observed instant never moved off the first payload — so it read as a stalled
loop rather than as a race. Driving the pacer alone over the same recording
released the second payload at exactly fifteen seconds, which is what showed
the pacer was not the subject.

## Root cause

Two things, and the second is why it looked like a stall rather than a flake.

**An await of a `Task` leaves the virtual scheduler.** Awaiting a scheduler
operation resumes on the scheduler, inside `AdvanceBy`. Awaiting a `Task` —
which is what every layer above the pacer does, each with
`ConfigureAwait(false)` — resumes on the thread pool. So `AdvanceBy` returns
before the chain above the scheduler has finished reacting, and any state read
on the next line is read early.

**A late continuation schedules its next wait from a later clock.** The poll
loop's next wait is scheduled when the continuation actually runs, against
whatever the virtual clock then reads. A continuation that lands after an
advance of one second schedules fifteen virtual seconds from _there_, so the
next advance is short by exactly as much as the test was late — and every
iteration falls further behind. A loop driven this way never catches up, which
is indistinguishable from a loop that stopped.

Nothing in the production code is wrong, and nothing about it is specific to
replay: `AircraftSnapshotClient`'s existing poll tests are deterministic only
because their fake contract answers with an already-completed task, so no
continuation ever leaves the scheduler.

## Spec delta

No product delta — no claim was wrong and no behaviour changed.
`replay-source` § 8 gained the trap, because every item left in that Feature
drives a chain whose waits are scheduled from inside awaited tasks.

## Claim

- B-007's second test —
  `ReplayCadenceTests.GivenTheLiveClientOverTheReplayContract_WhenPayloadsArrive_ThenNoIntervalOfItsOwnIsAdded`,
  which now drives the awaitable fetch and awaits it after advancing, and reads
  the interval the client answers with rather than inferring it from timing.
- B-010 —
  `ReplayStalenessTests.GivenAnAircraftLastReportedSixMinutesBeforeTheRecordingEnds_WhenTheRecordingIsReplayed_ThenItIsMarkedStaleAtThatPointAndKept`,
  whose strategy is handed a client that polls nothing, so the only waits in the
  test are the recording's own.

## Skill

[`test-from-scenarios`](../../.skills/test-from-scenarios/SKILL.md) § "Time is
injected, always" gained: **advance, then await the work the advance was for.**
An advance releases scheduled work; it does not wait for work that resumes off
the scheduler, and a fire-and-forget loop cannot be awaited at all — drive the
subject through its awaitable call and stub the loop that would otherwise race
it.
