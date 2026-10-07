---
title: "Lesson 0019: An advance that stops short hangs instead of failing"
description: "A test awaiting a value released by a scheduled wait never completes when the advance misses the due instant by a millisecond, and the run looks stuck rather than failing."
type: lesson
---

# Lesson 0019: An advance that stops short hangs instead of failing

**Date:** 2026-10-06
**Kind:** process

## Symptom

`dotnet test` on `0010`'s four pacer tests produced no result at all. The build
succeeded, the runner printed `A total of 1 test files matched the specified
pattern`, and then nothing — for five minutes, then another five after a second
attempt, until the run was killed. No test name, no failure, no timeout. Running
the one synchronous test of the five by name passed in 62 ms, which is what
showed the hang belonged to the four asynchronous ones rather than to the
runner, the build or the machine.

## Root cause

The fixture's recorded instants carried realistic millisecond jitter —
`14:32:11.113`, `14:32:26.118`, `14:32:41.104` — so the gap between the first
two payloads was 15.005 seconds. The test advanced a `TestScheduler` by fifteen
and awaited the payload, which the pacer releases five milliseconds later. The
scheduled work was never due, the `await` never completed, and nothing in the
stack has a deadline: xUnit waits on the returned task, the scheduler waits to
be advanced, and the advance had already happened.

The arithmetic was the author's mistake. What the lesson is about is that the
mistake had no failure mode: an assertion that is wrong fails and names itself,
while an advance that is short produces a run indistinguishable from a stuck
build. The cost was two five-minute runs and a bisection to find which test,
for a fixture typo.

## Spec delta

No product delta — no claim was wrong and no behaviour changed. The replay
source specification's § 8 gained the trap as its third, because the Feature
paces everything it builds from recorded instants and will meet this again on
`0011` and `0013`.

## Claim

- B-007 — `RecordingPacerTests.GivenThreePayloadsFifteenSecondsApart_WhenTheRecordingIsReplayed_ThenEachIsReleasedAtTheRecordedSpacing`,
  whose fixture now carries the same milliseconds on every line, so the recorded
  gap and the advance cannot differ.

## Skill

[`test-from-scenarios`](../../.skills/test-from-scenarios/SKILL.md) § "Time is
injected, always" gained: **advance to the due instant, not to the figure the
fixture reads like** — a test awaiting a value released by a scheduled wait
hangs for ever when the advance stops short, so derive the advance from the same
arithmetic the subject does, or build the fixture so the two cannot differ.
