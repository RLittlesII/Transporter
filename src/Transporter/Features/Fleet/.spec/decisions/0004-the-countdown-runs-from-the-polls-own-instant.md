---
title: "Decision 0004: The countdown runs from the poll's own instant"
description: "Measure the next-poll countdown from the instant the poll was made, carried on the poll status, rather than from the provider's observed instant, which lags the poll."
type: decision
---

# Decision 0004: The countdown runs from the poll's own instant

**Status:** decided

**Date:** 2026-10-15
**Made by:** the person

## The call

The next-poll countdown (B-036) runs for the gap between the due instant and
the instant the poll that set it was made, both carried on the poll status.
It is no longer measured from the observed instant.

## Why

The rendered review on `0072`, in the iOS Simulator against live OpenSky on a
fifteen-second interval, saw the countdown start at sixteen or seventeen
seconds and still read about one second when the next poll landed. The cause
is two clocks subtracted: the due instant is the poll's time on the poller's
scheduler plus the wait (`aircraft-source` B-054), while the observed instant
is OpenSky's reported `time` (`aircraft-source` B-003, `fleet-pipeline`
B-031), which lags the poll. The countdown therefore overstated by the
provider's lag on every poll, in front of an audience.

The poller already reads the instant it made the poll, on the scheduler it
runs on, to compute the due instant. Carrying that instant beside the due one
puts both ends of the countdown on one clock. No consumer reads a clock to do
it, so `aircraft-source` B-003 holds; the subtraction stays in the view's call
to `FleetCardText`, so B-031's ban on arithmetic over instants in a view model
holds too.

What is left is one request's latency: the status arrives one request after
the poll was made, and the next arrives one request after the next poll, so
the two cancel to within jitter. § 5 row 10 keeps it that way.

## Rejected

**Keep the measure and document the skew.** No code, but the stage shows
seventeen seconds on a fifteen-second interval and about one second when the
poll lands, and the specification would bless subtracting two clocks.

**Clamp to the interval.** The view does not know the interval: it is the
source's own setting, which a swap must not need a view edit to follow, and a
`429` refusal is longer than it. A clamp therefore needs the wait carried on
the status — the same seam change as this call — or a fixed fifteen seconds,
wrong for a refusal and for any other source. It hides the two-clock mix
rather than removing it.

## Affects

- `fleet-dashboard` B-036, its `@B-036` scenario, § 5 row 10, and §§ 7 and 9
  through [`0095`](../../.issue/0095-countdown-from-the-poll-instant.yml).
- `fleet-pipeline` B-040 and its `@B-040` scenario: the status carries the
  instant the poll was made.
- `aircraft-source` B-054 and its `@B-054` scenarios: the poller reports that
  instant.
- Both through `aircraft-source`
  [`0094`](../../../../Integrations/OpenSky/.issue/0094-report-the-poll-instant.yml),
  which adds the member to the status and writes it, as `fleet-dashboard`
  `0037` carried `fleet-pipeline` B-029.
