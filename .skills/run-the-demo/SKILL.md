---
name: run-the-demo
description: Stage-day runbook for the Transponder talk — credit budget, laptop-not-cloud, replay fallback, the OpenSky citation, and rehearsing the live plane-to-ship swap. Use when preparing to present or recording rehearsal data.
---

# Running the demo

The code is only half the deliverable; the other half is it working in a room
with bad wifi. Everything here comes from
[`README.md`](../../README.md) — § "Gotchas", § "Demo resilience", § "Closing
act", and the open items list.

## Before the talk

- **Register an OpenSky account and create an API client.** OAuth2 client
  credentials, no basic auth. Anonymous gets 400 credits a day at 10-second
  resolution; registered gets 4,000 at 5-second. Register.
- **The box and the interval are decided**: Houston — both airports, the ship
  channel and Galveston Bay — polled every 15 seconds, both configurable (see
  the aircraft-source specification's `decisions/0001`). That box is about
  2.9 sq°, so 1 credit a call and 240 an hour: a talk costs roughly 180 of the
  daily 4,000, and the app could run all day. **Credits bound the box, not the
  interval** — widening past 25 sq° is what doubles the bill.
- **Verify both credentials**: OpenSky client id and secret, and the AISStream
  API key for the closing act. An expired key discovered mid-sentence is the
  worst possible time.
- **Record replay data in rehearsal** — aircraft *and* vessels. See
  [`api-mock`](../api-mock/SKILL.md). Record long enough that items go stale on
  playback, or the staleness part of the talk has nothing to show.
- **Add the OpenSky citation slide.** The terms ask for it in public
  presentations: Schäfer, Strohmeier, Lenders, Martinovic, Wilhelm, "Bringing
  Up OpenSky: A Large-scale ADS-B Sensor Network for Research."
- The vessel subscription uses the **same** Houston box — that is why the box
  contains a port. Check there is real traffic in it at the talk's hour; an
  empty sea is a bad slide.

## On the day

- **Run from a laptop, not a cloud VM.** OpenSky blocks AWS and other
  hyperscaler IPs. This is not a performance preference; the demo simply will
  not get data.
- Check `X-Rate-Limit-Remaining` before starting. If the budget is nearly
  spent, open on replay and say so — it costs one sentence and removes the
  risk entirely.
- Have replay one control away, not one rebuild away: it is the same source
  selector as the live swap
  ([`hot-swap-source`](../hot-swap-source/SKILL.md)).
- Keep the credential values off screen. Secrets come from user secrets or
  environment variables ([`api-contract`](../api-contract/SKILL.md)), and a
  shared screen is a screenshot.

## The closing act

Swap the polled aircraft feed for the AISStream vessel feed **in the running
app** and let the audience watch the same grid, filters, sorts and groups
refill with ships.

What the audience should be watching is *the code that did not change*. Have
the pipeline on screen, or ready to show immediately after: same cache, same
`Filter`, same `Sort`, same `Group`, same `Bind`. That is the whole argument.

Rehearse:

- the swap, more than once in a row, watching memory and credit burn stay flat;
- the swap with the recorded vessel replay as the target, so a dead network
  does not take the ending with it;
- swapping while a poll is in flight — the late response must not land;
- swapping twice quickly, because someone will ask.

The failure modes and the cache-on-swap decision live in
[`hot-swap-source`](../hot-swap-source/SKILL.md). Pick clear-and-refill or
expiry-drain in rehearsal and stick to it; deciding on stage looks like a bug.

## If something breaks

| Symptom | First move |
|---|---|
| No data at all, live source | Check the IP — cloud VM or VPN exiting through one. Switch to replay. |
| `429` responses | The credit budget is spent or the retry is ignoring `X-Rate-Limit-Retry-After-Seconds`. Switch to replay. |
| Grid empty after the swap | Ships feed never authenticated or never emitted. Swap to recorded vessel replay. |
| Grid filling with ghosts | Expiry is not running — the staleness clock is wrong. Mention it, move on; it is one slide, not the talk. |

In every row the answer is the same shape: **swap the source, keep talking.**
That is the resilience plan the architecture exists to support, and saying so
out loud makes the failure part of the lesson.

## Never add

- A cloud-hosted run of the live demo.
- A credential on screen, in a log, or in a screenshot.
- A closing act with no recorded fallback.
- A rehearsal that skips the swap, or skips letting items go stale.
