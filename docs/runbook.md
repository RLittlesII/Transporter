---
title: "Stage-day runbook"
description: "What to do before the talk, on the day, and when something breaks — the operational half of the demo's resilience plan."
type: guide
---

# Stage-day runbook

The code is only half the deliverable; the other half is it working in a room
with bad wifi. The facts behind each step are in
[`README.md`](../README.md) §§ "Limits", "Gotchas", "Demo resilience" and
"Closing act", and in
[decision 0001](../src/Transporter/Integrations/OpenSky/.spec/decisions/0001-houston-bounding-box.md);
this file is the order to do them in.

## Before the talk

- **Register with the provider and create an API client.** Registered access is
  worth an order of magnitude in budget over anonymous access
  (`README.md` § "Limits"). Tracked in `README.md` § "Open items".
- **Verify every credential** — the polled provider's client id and secret, and
  the push provider's key for the closing act. An expired key discovered
  mid-sentence is the worst possible time.
- **Build the head on the machine that holds the secret store.** The polled
  provider's credentials are packaged into the bundle when it is built
  (`README.md` § "Authentication"), so a bundle built elsewhere, or before the
  store was set, has none — and one built here is never copied off the machine.
- **Record replay data in rehearsal**, aircraft _and_ vessels
  ([`features/replay-source`](../features/replay-source/.spec/README.md)).
  Record long enough that items go stale on playback, or the staleness part of
  the talk has nothing to show.
- **Add the citation slide** the provider's terms ask for
  (`README.md` § "Gotchas").
- **Check the box has traffic at the talk's hour.** The vessel subscription uses
  the same box as the aircraft, which is why the box contains a port. An empty
  sea is a bad slide.

## On the day

- **Run from a laptop, not a cloud VM.** The provider blocks hyperscaler IPs
  (`README.md` § "Gotchas"). This is not a performance preference; the demo
  simply will not get data.
- **Check the remaining budget before starting.** If it is nearly spent, open on
  replay and say so — it costs one sentence and removes the risk entirely.
- **Have replay one control away, not one rebuild away.** It is the same source
  selector as the live swap
  ([`hot-swap-source`](../.skills/hot-swap-source/SKILL.md)).
- **Keep credential values off screen.** Secrets come from the user-secrets
  store, and a shared screen is a screenshot.

## The closing act

Swap the polled feed for the push feed **in the running app** and let the
audience watch the same grid, filters, sorts and groups refill.

What they should be watching is _the code that did not change_. Have the
pipeline on screen, or ready to show immediately after: same cache, same
`Filter`, same `Sort`, same `Group`, same `Bind`. That is the whole argument.

Rehearse:

- the swap, more than once in a row, watching memory and budget burn stay flat;
- the swap with the recorded vessel replay as the target, so a dead network does
  not take the ending with it. **A stretch goal that only works on a good
  network is a gamble, not a demo**;
- swapping while a poll is in flight — the late response must not land;
- swapping twice quickly, because someone will ask.

The failure modes and the gap-on-swap decision live in
[`hot-swap-source`](../.skills/hot-swap-source/SKILL.md). Settle what the
audience sees during the gap in rehearsal and stick to it; deciding on stage
looks like a bug.

## If something breaks

| Symptom                                       | First move                                                                                                   |
| --------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| No data at all, live source                   | Check the IP — a cloud VM, or a VPN exiting through one. Switch to replay.                                   |
| Throttle responses                            | The budget is spent, or the retry is ignoring the provider's retry-after header. Switch to replay.           |
| Grid empty after the swap                     | The push feed never authenticated or never emitted. Swap to recorded vessel replay.                          |
| Grid filling with items that should have gone | Expiry or staleness is not running — the clock is wrong. Mention it, move on; it is one slide, not the talk. |

In every row the answer is the same shape: **swap the source, keep talking.**
That is the resilience plan the architecture exists to support, and saying so
out loud makes the failure part of the lesson.

## Never

- A cloud-hosted run of the live demo.
- A credential on screen, in a log, or in a screenshot.
- A closing act with no recorded fallback.
- A rehearsal that skips the swap, or skips letting items go stale.
