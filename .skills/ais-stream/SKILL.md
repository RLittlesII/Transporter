---
name: ais-stream
description: Consume AISStream.io's vessel WebSocket feed behind the same tracking seam as OpenSky, keyed by MMSI, with silent vessels aged out by ExpireAfter. Use when building or changing the ships source.
---

# The AISStream vessel feed

The demo's closing act and its stretch goal
([`README.md`](../../README.md) § "Closing act"): a push feed of live vessel
positions, swapped in mid-demo to prove that everything downstream of the
cache is unchanged. The README records the intent (`[X] Intend to show
ships`), and it fits the "fleet" framing better than the alternatives
considered.

It implements `ITrackingSource` like every other source
([`api-contract`](../api-contract/SKILL.md)). Nothing downstream learns it is
here — that is the entire point.

## Facts

- **WebSocket, not polling.** Connect, then receive. There is no snapshot
  endpoint, so this source assembles its own current known set and emits it as
  a snapshot set like every other source; **the cache diffs that set** exactly
  as it diffs a poll's. See
  [`dynamic-data-pipeline`](../dynamic-data-pipeline/SKILL.md) and
  [ADR-0002](../../.spec/adr/0002-four-layers-wire-to-fleet.md). How it holds
  that set without becoming a second collection of tracked items is the open
  question this source has to answer — § 11 of the aircraft-source
  specification — and it is the first thing to settle when this source is
  specified.
- **Keyed by MMSI.** The cache key for a vessel, the way `icao24` is for an
  aircraft.
- **Subscribe with a bounding box.** Filtering happens server-side on the
  subscription message, not client-side after the fact.
- **Named JSON fields** — easier to consume than OpenSky's positional arrays.
  No custom converter needed; a plain DTO and a Mapperly mapper
  ([`mapping`](../mapping/SKILL.md)) are enough.
- **A free API key is required** (sign up with GitHub).
- **Vessels go silent rather than being removed.** The feed does not announce
  a departure; a ship simply stops reporting. So `ExpireAfter` and the
  staleness rule matter here exactly as much as they do for aircraft.
- Rate and fairness limits are the provider's, and lighter than OpenSky's
  credit budget — but the feed is free and unmetered-looking, which is not the
  same as unlimited. Subscribe to the box you need.

## Client choice is open

Two documented routes, neither referenced in
[`Directory.Packages.props`](../../Directory.Packages.props) yet:

| Route | Note |
|---|---|
| `ClientWebSocket` | What AISStream's own C# example uses. No dependency, full control of the read loop and reconnect. |
| `AISStream.NET` | Community package. Less code, one more dependency to vet for a demo. |

Pick in the first issue that builds this source and record the choice. Either
way the reader is an actor
([`akka-actor`](../akka-actor/SKILL.md)) owning connect, subscribe, read and
reconnect — **a dropped socket is normal operation, not an error path** — and
a restart of the reader never tears down the cache.

## Credentials

The API key comes from user secrets or environment variables, never from
source control, a log line, a fixture, or a screenshot — same rule as the
OpenSky client credentials ([`api-contract`](../api-contract/SKILL.md)). A
missing key fails loudly at startup, naming itself.

**Verify the key before going on stage.** An expired key discovered during
the closing act is the worst possible time to find out.

## Stage readiness

- **Record vessel traffic in rehearsal.** The closing act needs the same
  replay fallback as the main demo ([`api-mock`](../api-mock/SKILL.md)); a
  stretch goal that only works on a good network is a gamble, not a demo.
- Pick a box with enough traffic to look alive. An empty sea is a bad slide.
- The swap mechanics, and what the cache does during it, live in
  [`hot-swap-source`](../hot-swap-source/SKILL.md).

## Never add

- An API key in source control, a log, or a test fixture.
- A test that connects to `stream.aisstream.io`.
- Client-side filtering standing in for a bounding-box subscription.
- A reconnect that rebuilds the cache or the pipeline.
- `Vessel` leaking past the detail pane — the grid binds `TransportVehicle`
  ([`transponder-domain-model`](../transponder-domain-model/SKILL.md)).
