---
title: "A retained cache needs its own reported time"
description: "Source reactivation must restore its provider instant before retained vehicles are published."
type: lesson
---

# A retained cache needs its own reported time

## Symptom

Returning from vessel replay to an older aircraft recording briefly marked all
retained aircraft stale until the next paced aircraft payload arrived.

## Root cause

The selected strategy immediately republished its retained cache while the shared
observed clock still held the outgoing source's reported instant. Clock updates
were specified for payload arrival and recording rewind, but not reactivation.

## Spec delta

Replay-source B-009 now requires an already-timed source to restore its own last
provider instant before publishing retained vehicles. The existing provider-time
writer owns that restoration; no new payload is released early and no ambient
clock supplies a replacement instant. B-010's source-specific silence treatment
is then evaluated against the correct source immediately.
