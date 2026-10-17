---
title: "Lesson 0008: A countdown needs one clock"
description: "A provider timestamp and a poller due instant overstated the scheduled wait; the poll status now supplies both ends on the poller's clock."
type: lesson
---

# Lesson 0008: A countdown needs one clock

**Date:** 2026-10-17
**Kind:** product

## Symptom

The rendered review for item `0072` saw a fifteen-second cadence begin at
sixteen or seventeen seconds and still show about a second when the next
poll landed.

## Root cause

`NextDue` used the poller's scheduler, while the countdown subtracted
`Observed`, the provider's reported data time. Provider lag inflated every
wait. The status carried no poll-start instant, so the view could not measure
the scheduled gap on one clock.

## Spec delta

[Decision 0004](../decisions/0004-the-countdown-runs-from-the-polls-own-instant.md)
already amended B-036 to use the poll's own instant and excluded consumer
clock reads to compensate for request latency. Items `0094` and `0095` build
that agreement: aircraft-source B-054 reports `PolledAt` beside `NextDue`,
fleet-pipeline B-040 republishes both, and the countdown binds the complete
record. `Observed` remains the source of data ages and supplies no countdown
fallback.

## Claim

- B-036 — `FleetCardTextTests.GivenAPollInstantAndADueInstant_WhenTheDurationIsRead_ThenItIsTheirNonnegativeGap`
  proves ordinary/refused waits, absent starts and nonpositive gaps;
  `FleetPollViewModelTests.GivenSuccessRefusalAndNone_WhenStatusesArrive_ThenEachNotificationReadsTheCompleteReport`
  proves matching status values are readable during each binding notification.
- Aircraft-source B-054 — `AircraftPollInstantTests` distinguishes request
  start from provider time and delayed completion for cadence and demand,
  including refusals and failures.
