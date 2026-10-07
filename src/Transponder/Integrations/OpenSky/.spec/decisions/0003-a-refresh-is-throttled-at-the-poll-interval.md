---
title: "Decision 0003: The demo gets a refresh button, throttled at the poll interval"
description: "A user may demand a poll, and demand cannot make the source spend credits faster than its own cadence: a press inside the configured interval is refused."
type: decision
---

# Decision 0003: The demo gets a refresh button, throttled at the poll interval

**Status:** decided

**Date:** 2026-10-07
**Made by:** the person

## The call

The dashboard gets a refresh gesture: the user presses a button, an actor is
told, and a poll happens. A press that falls within the configured polling
interval is refused rather than queued, so the fastest the source can be made to
spend credits is the cadence it already runs at. B-053 claims the throttled
poll, `fleet-dashboard` B-028 the button.

## Why

The gesture was asked for because the actor model had nothing to show. One actor
exists, it performs the swap, and the swap happens once in the closing act —
so for the length of the talk the audience sees a pipeline reacting to a clock
and to nothing a person did. A button that causes an update is the smallest
surface that makes the point, and `mvvm` § "Two kinds of input, two routes"
already says a discrete effect is an actor's. [ADR-0012](../../../../../.spec/adr/0012-a-user-triggered-refresh-is-told-to-an-actor.md)
is why it is a message rather than a fifth method on the tracker.

The throttle is about money. One poll is one credit against 4,000 a day
(README.md § "Limits"), and `fleet-pipeline`
[decision 0002](../../../../Tracking/.spec/decisions/0002-the-poll-interval-is-not-a-live-input.md)
refused a live poll interval on exactly that ground — "a control that makes the
poll faster is a control that can empty the day's budget during the talk." A
refresh button is the same hazard at a smaller grain, so it does not reach the
stage without a bound. That record left this delta available rather than
foreclosing it, which is why this is a decision here and not a reversal there.

Binding the window to B-050's interval rather than a new option is what keeps
this cheap: no second value to configure, no second value to drift, and the
guarantee states itself — demand cannot outspend the cadence. On stage the cost
is nil either way, because the runbook plays a recording back rather than calling
a live provider; the budget this protects is the rehearsal's.

## Rejected

**No throttle, one poll per press.** Simplest and honest about what the button
does. Rejected because it puts the credit budget under an unbounded control,
which is the thing decision 0002 refused; and the failure is silent — a spent
budget, not an error — discovered the next time someone rehearses.

**A press budget per session**, N refreshes and then a disabled button with the
count on screen. Genuinely the option that teaches the economics, which decision
0002 itself said a polled source should. Rejected as more state than the gesture
is worth, and a button that disables mid-talk is something to explain from the
stage. The throttle does not foreclose it.

**Making the poll interval live after all**, so the demo can show the economics
by moving it. Rejected: it is decision 0002's own rejected option, and nothing
here is new evidence against that call.

## Affects

- B-053 — new, the throttled on-demand poll.
- `fleet-dashboard` B-028 — new, the button and its `Tell`.
- B-050 — unchanged, and now also the refresh window.
- `fleet-pipeline` decision 0002 — unchanged and not reversed; this is the
  spec delta it left available.

## Reversal

Not applicable — this record has not been reversed.
