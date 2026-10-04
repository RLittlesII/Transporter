---
name: api-contract
description: Define the single source-agnostic snapshot seam every Transponder data source implements, and the hand-written OpenSky client behind it (positional JSON, OAuth2 tokens, credits, rate limits). Use when touching a client, a contract, or credentials.
---

# The Transponder API contract

This file covers **the one seam every data source implements, and what OpenSky
actually does behind it**. The provider's own documentation is
[the OpenSky REST API](https://openskynetwork.github.io/opensky-api/rest.html);
the facts below are the consequences of it for this demo.

## The seam is the whole design

One interface, producing snapshots of tracked items:

```csharp
public interface ITrackingSource
{
    IObservable<IReadOnlyCollection<TransportVehicle>> Snapshots { get; }
}
```

Everything else in the demo sits on one side of that line or the other.

- **It is deliberately not OpenSky-shaped.** No bounding box, no credits, no
  token, no polling interval in the signature. A push source satisfies it by
  emitting the current known set; a replay source satisfies it by reading
  files. That is what makes the live plane-to-ship swap possible — see
  [`hot-swap-source`](../hot-swap-source/SKILL.md).
- Implementations to date: live OpenSky (polled), AISStream vessels (push,
  the stretch goal), replay-from-recording, and a simulated source — see
  [`api-mock`](../api-mock/SKILL.md).
- Source-specific controls (bounding box, interval, credentials) are
  constructor or options input to one implementation, never on the interface.
- A source that needs to tell the UI *about itself* — display name, which
  columns make sense — does so through a separate small description, not by
  widening this interface.

The item type is the abstract `TransportVehicle` from
[`transponder-domain-model`](../transponder-domain-model/SKILL.md) — a source
emits its own subclass (`Aircraft`, `Vessel`) and the seam only ever names the
base. The `JsonConverter` is the last place the wire shape exists.

## OpenSky: facts, not preferences

All of this is the provider's behavior and is not ours to simplify. The
details live in [`README.md`](../../README.md) § "Data source: OpenSky
Network"; the consequences are here.

- **No OpenAPI spec exists.** The client is hand-written with `HttpClient` and
  `System.Text.Json`. Do not go looking for a generator.
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
- A generated client, or a positional array read by anything but the converter.
- A retry that ignores `X-Rate-Limit-Retry-After-Seconds`, or a poll loop with
  no interval ceiling. Burning the daily credit budget before the talk is a
  self-inflicted outage.
- A credential in source control or in a log line.
