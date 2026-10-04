---
name: api-contract
description: Define the four layers between a provider and the fleet — API types, the internal snapshot, the source-agnostic seam, and the store-and-diff cache — and the hand-written OpenSky client behind them (positional JSON, OAuth2 tokens, credits, rate limits). Use when touching a client, a contract, or credentials.
---

# The Transponder API contract

This file covers **the layers between a provider and the cache, and what
OpenSky actually does behind them**. The provider's own documentation is
[the OpenSky REST API](https://openskynetwork.github.io/opensky-api/rest.html);
the facts below are the consequences of it for this demo.

## Four layers, and the seam is the third

One responsibility each, named so a reader can tell from a signature which
layer they are in — see
[ADR-0002](../../.spec/adr/0002-four-layers-wire-to-fleet.md):

```csharp
// 1. API types — what OpenSky sends. Positional; the converter is their only reader.
//    StatesResponse, and the row it carries.

// 2. The internal snapshot — the server's record with names on it.
//    Value equality over every member; keyed on icao24; derives nothing.
public sealed record AircraftSnapshot( /* … */ );

// 3. The seam — what every source satisfies.
public interface ITrackingSource
{
    IObservable<SnapshotSet<AircraftSnapshot>> Snapshots { get; }
}

// 4. The cache — stores and diffs snapshots. Nothing else: no domain type, no clock.
public interface IVehicleCache
{
    IObservable<IChangeSet<AircraftSnapshot, string>> Connect();
}
```

A `SnapshotSet` carries the complete set the source currently knows about
**and the instant it was observed**, so "as of when" is data rather than
something a consumer answers with an ambient clock.

Downstream of the cache, `IFleetTracker` projects those changesets into
`TransportVehicle` — that is the second mapping layer and the first place a
domain object exists. It belongs to
[`dynamic-data-pipeline`](../dynamic-data-pipeline/SKILL.md), not here.

The wire shape sits above layer 2 and the collection sits below layer 4;
neither can see the other.

- **The seam is deliberately not OpenSky-shaped.** No bounding box, no
  credits, no token, no polling interval in the signature. A replay source
  satisfies it by reading files; a push source satisfies it by emitting its
  current known set, and the cache diffs that set exactly as it diffs a poll's.
  That is what makes the live plane-to-ship swap possible — see
  [`hot-swap-source`](../hot-swap-source/SKILL.md). How a push feed assembles a
  full set without holding a second collection is an open question on the
  aircraft-source specification (§ 11), not settled here.
- Implementations to date: live OpenSky (polled), AISStream vessels (push,
  the stretch goal), replay-from-recording, and a simulated source — see
  [`api-mock`](../api-mock/SKILL.md).
- Source-specific controls (bounding box, interval, credentials) are
  constructor or options input to one implementation, never on the interface.
- A source that needs to tell the UI *about itself* — display name, which
  columns make sense — does so through a separate small description, not by
  widening this interface.

The item type at the seam and in the cache is the **snapshot**, not the domain
object. `TransportVehicle` and its subclasses from
[`transponder-domain-model`](../transponder-domain-model/SKILL.md) first appear
at `IFleetTracker`, and the seam names neither them nor a DynamicData type.
`AircraftSnapshotConverter` is the last place the positional wire shape exists;
`AircraftSnapshot` is the last place the wire's vocabulary exists.

## OpenSky: facts, not preferences

All of this is the provider's behavior and is not ours to simplify. The
details live in [`README.md`](../../README.md) § "Data source: OpenSky
Network"; the consequences are here.

- **No OpenAPI spec exists**, so there is no generator to look for. The client
  is written with Flurl over `System.Text.Json` —
  [`http-client`](../http-client/SKILL.md) has the mechanics,
  [ADR-0001](../../.spec/adr/0001-flurl-for-http.md) the reasoning for Flurl,
  and [ADR-0002](../../.spec/adr/0002-four-layers-wire-to-fleet.md) the
  reasoning for the four layers above.
- **`states` is an array of arrays.** Fields are positional, not named, so a
  custom `JsonConverter` is mandatory, and the index→field table is the
  contract. Read positions by index against that table; never by guessing from
  a sample payload.
- **`extended=1` is what supplies category.** Without it the field is absent,
  and grouping by category silently has nothing to group. If the UI offers
  category grouping, the request sets `extended=1`.
- **OAuth2 client credentials only.** There is no basic auth. Token URL is in
  the README. Tokens last 30 minutes: refresh on expiry *and* on a `401`,
  because a token can die early.
- **Credits are the real budget.** Cost per `/states/all` call scales with
  bounding-box area (≤25 sq° = 1, up to 4 for global). A metro box polled
  every 10 seconds burns roughly 360 credits per hour against a registered
  account's 4,000 per day. **The polling interval is therefore a budget
  decision, not a tuning knob** — changing it changes how long the demo can
  run.
- **Watch the headers.** `X-Rate-Limit-Remaining` says how much budget is
  left; log it at debug so a rehearsal reveals the burn rate. A `429` carries
  `X-Rate-Limit-Retry-After-Seconds` — honor that value rather than inventing
  a backoff.
- **OpenSky blocks AWS and other hyperscaler IPs.** This is why the demo runs
  from a laptop. See [`run-the-demo`](../run-the-demo/SKILL.md).
- Public presentation of this data owes the OpenSky citation. That is a slide,
  tracked in the README's open items.

## Credentials

- `client_id` / `client_secret` for OpenSky and the AISStream API key come
  from user secrets or environment variables.
- **Never committed, never logged, never in a test fixture, never in a
  screenshot of the running app.** A token value is not debug output; log that
  a refresh happened, not what it returned.
- A missing credential fails loudly at startup with a message naming which one
  is absent. Discovering it on stage mid-sentence is the failure this prevents.

## The backup source

`airplanes.live` (README § "Backup source") has named JSON fields and would be
easier to consume, but **its access terms are unresolved** — the README says
to email them first. So: it is a legal open question, not a technical one. It
implements the same seam if and when that clears; it is not wired in by
default.

## Never add

- A second seam. Two source interfaces means the swap has to bridge them, and
  the teaching point of the demo dies.
- OpenSky concepts (credits, bounding boxes, tokens) on the shared interface.
- An `IChangeSet` or any other DynamicData type on `ITrackingSource`. A source
  that has to build a cache to say "here are four aircraft" is not a seam.
- A cache owned by, or constructed inside, a source. One cache outlives every
  source, which is what makes the swap a clear rather than a rebuild.
- A domain type held, constructed or returned by the cache, or a clock read
  inside it. The cache stores and diffs snapshots; staleness is the tracker's.
- A generated client, or a positional array read by anything but the converter.
- A retry that ignores `X-Rate-Limit-Retry-After-Seconds`, or a poll loop with
  no interval ceiling. Burning the daily credit budget before the talk is a
  self-inflicted outage.
- A credential in source control or in a log line.
