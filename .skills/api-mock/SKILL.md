---
name: api-mock
description: Record real snapshots and replay them through the same tracking seam, so the demo survives a dead venue network and tests never touch a network or a wall clock. Use when building a replay or simulated source, or a test fixture.
---

# Recorded and simulated sources

The demo's resilience plan ([`README.md`](../../README.md) § "Demo
resilience") is one sentence: put the data source behind a single interface,
then have more than one implementation. Replay is not a testing afterthought —
it is what goes on stage when the venue wifi dies.

It also makes the teaching point for free: **the pipeline does not care where
the data comes from.**

## Implementations of the same seam

All of these implement `ITrackingSource` from
[`api-contract`](../api-contract/SKILL.md). Nothing downstream can tell them
apart.

| Source | Purpose |
|---|---|
| Replay — aircraft | Recorded OpenSky snapshots, fed back at their original cadence. The primary fallback. |
| Replay — vessels | Recorded AISStream traffic. **The closing act needs a fallback too** — a stretch goal that only works on a good network is not a demo, it is a gamble. |
| Simulated | Generated movement, no network at all. Useful offline and in tests of the pipeline itself. |

## Recording

- Record during rehearsal, not on the day. The README's open items track it.
- Write **raw** snapshots as received, one file or one line per snapshot, with
  the instant it arrived. Raw means replay can be re-parsed after a converter
  fix; parsed means a bug in the converter is baked into the recording.
- Keep the recording long enough to show the behavior being taught: aircraft
  appearing, updating, and going stale. A 30-second loop shows updates but
  never shows expiry.

## Replay

- Feed snapshots at the recorded cadence, then loop. A loop boundary that
  rewinds every item at once looks like a glitch; prefer looping on a
  recording long enough that the seam is not the thing people notice.
- **Replay is selected through the same mechanism as the live swap** — the
  source selector in [`hot-swap-source`](../hot-swap-source/SKILL.md). One
  switch, not two: a separate "offline mode" flag is a second code path that
  only ever runs under pressure.
- Replay drives time from the recording, not from `DateTime.UtcNow`. Staleness
  must behave the same on replay as it does live, which means the staleness
  clock is injected, not ambient.

## Tests

- **No test reaches a network.** Not `opensky-network.org`, not
  `stream.aisstream.io`, not the backup source.
- **No test depends on the wall clock.** Time-based operators (`ExpireAfter`,
  throttles, the poll interval) take an injected scheduler so a test advances
  time deliberately. See
  [`test-from-scenarios`](../test-from-scenarios/SKILL.md).
- Fixtures are **synthetic** (`AGENTS.md`): invented callsigns, invented
  MMSIs, invented positions. Committed as JSON next to the tests that use
  them.
- A fixture exercising the positional-array converter keeps the array shape
  exactly as OpenSky sends it, including the nulls — that converter's whole
  job is surviving a sparse row.

## Never add

- A test that reaches a live provider, or that passes only when a key is
  present.
- Real recorded traffic as a test fixture without scrubbing it first. A
  rehearsal recording is operational data, not a fixture.
- A second selection path for offline mode.
- `Thread.Sleep` or a real delay in a test. If a test takes a second, it is
  reading the wrong clock.
