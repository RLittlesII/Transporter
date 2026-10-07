---
title: "ADR-0012: A user-triggered refresh is told to an actor"
description: "A button that fetches new data is a discrete effect, so it is a message to an actor rather than a fifth method on the tracker; the actor owns the throttle that keeps demand from outspending the cadence."
type: adr
---

# ADR-0012: A user-triggered refresh is told to an actor

**Status:** accepted

## Context

Nothing in this repository lets a user cause an update. `AircraftTrackerSource`
polls on the first subscription and the configured interval does the rest
(`aircraft-source` B-040, B-050), so "click a button and the fleet updates" was
true of no surface the demo has. The person asked for it on 2026-10-07, in spike
[`0040`](../../.issue/0040-actor-wiring-and-tracker-lifetime-spike.yml).

Two things already written decide most of the shape. `mvvm` § "Two kinds of
input, two routes" splits a view model's outputs: a **continuous** value — a
keystroke, a chosen comparer, a grouping — is published to the pipeline, while a
**discrete effect** — swap, start, stop, refresh — is a message to an actor,
because "these change what the system is doing" and an actor owns that including
failure and retry. [ADR-0009](0009-the-pipeline-publishes-changesets-a-consumer-binds.md)
then gave the tracker four input methods for the continuous half, which is what
`fleet-dashboard` B-009 – B-012 call.

The constraint that makes this more than a routing question is money. One poll
is one OpenSky credit against 4,000 a day (README.md § "Limits"), and
`fleet-pipeline` [decision 0002](../../src/Transponder/Tracking/.spec/decisions/0002-the-poll-interval-is-not-a-live-input.md)
already refused a **live poll interval** for exactly that reason: "a control
that makes the poll faster is a control that can empty the day's budget during
the talk." A refresh button is the same hazard at a smaller grain — a credit per
press, with nothing bounding the presses. That record left "specified in
`aircraft-source` as a spec delta" available later, so this is the delta rather
than a reversal.

## Decision drivers

- A discrete effect is an actor's, and the rule saying so is already written.
- The credit budget is the one resource a wrong answer spends irreversibly, and
  the run that matters is in front of people.
- A swap already works this way (`fleet-dashboard` B-016), so the demo gains no
  second mechanism to explain.

## Considered options

| Option                                              | Summary                                                                                                                                             | Why not                                                                                                                                                                                                                                                                  |
| --------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **A message to an actor, throttled there** (chosen) | The view model resolves the actor from `IActorRegistry` and `Tell`s it; the actor refuses a poll that falls inside the configured interval.         | —                                                                                                                                                                                                                                                                        |
| A fifth method on `IFleetTracker`                   | `Refresh()` beside `Filter`, `SortBy`, `GroupBy` and `StaleAfter`, symmetric with what the dashboard already calls.                                 | The four methods hand a **value** to a stage and the pipeline re-evaluates it; nothing is fetched. A fifth that reaches through the seam to make a provider call gives the pipeline a member about calls `fleet-pipeline` B-023 and B-024 forbid it from knowing happen. |
| A command the view model performs itself            | The view model calls the client and awaits it.                                                                                                      | A view model holding a client, and a blocking or awaited network call in one, is what `fleet-dashboard` B-017 and `mvvm` § "Never add" forbid outright.                                                                                                                  |
| No throttle — one poll per press                    | Simplest, and honest about what the button does.                                                                                                    | Puts the credit budget under an unbounded control, which is what decision 0002 refused. A press-and-hold during a talk is the failure, and it fails silently as a spent budget rather than as an error.                                                                  |
| A press budget per session                          | N refreshes, then the button disables, with the count on screen — and it teaches the economics decision 0002 said a polled source genuinely should. | Rejected by the person on 2026-10-07 as more state to specify than the gesture is worth, and a button that disables mid-talk needs explaining from the stage. Still available: the throttle does not foreclose it.                                                       |

## Decision

A user-triggered refresh is a **message told to an actor**, never a method on
the tracker and never work a view model performs. The view model resolves the
actor from `IActorRegistry` and `Tell`s it; it does not `Ask`, so no view model
waits on a network round trip (`fleet-dashboard` B-017).

**The throttle lives in the actor, at the configured polling interval.** A
demanded poll inside that window is refused, silently to the caller and without
faulting the stream or disturbing the cadence. The window is B-050's existing
interval rather than a new option, so demand can never make the source spend
credits faster than its own cadence already does, and there is no second value
to keep in step.

Claims: `aircraft-source` B-053 for the throttled on-demand poll,
`fleet-dashboard` B-028 for the button and its `Tell`. The budget reasoning is
`aircraft-source` [decision 0003](../../src/Transponder/Integrations/OpenSky/.spec/decisions/0003-a-refresh-is-throttled-at-the-poll-interval.md).

## Consequences

- A user gesture now causes an update, which is what the actor model is in this
  demo to show. Before this, the only actor message was a swap, performed once
  in the closing act.
- **B-009 – B-012 do not move.** A chosen comparer or grouping stays a
  continuous value and stays a method call: the split `mvvm` draws is unchanged,
  and this record is an instance of it rather than an exception to it.
- The throttle is derived from B-050, so changing the polling interval changes
  the refresh window with it. That is intended and worth knowing: setting a long
  interval makes the button feel broken, and the honest fix is the indicator
  B-028 requires rather than a second knob.
- A refused press is indistinguishable from an accepted one that returned
  identical data, because `EditDiff` emits no change for an unchanged snapshot
  (`aircraft-source` B-011). So the button's indicator cannot be driven from the
  fleet changing; B-028 states that, and it is the clause an implementation is
  most likely to get wrong.
- Credits are bounded but not free: the cadence already spends them, and the
  throttle only prevents demand from spending them faster. A rehearsal against
  the live provider still costs what it costs, which is why the runbook plays a
  recording back.
