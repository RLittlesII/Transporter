---
name: api-contract
description: Define the typed OpenSky API contract, the client that caches what it fetches, the per-type strategy that projects snapshots to domain vehicles, and the decorator the fleet tracker wraps — plus what OpenSky actually does (positional JSON, OAuth2 tokens, credits, rate limits). Use when touching a contract, a client, a strategy, or credentials.
---

# The Transponder API contract

This file covers **the components between a provider and the fleet, and what
OpenSky actually does behind them**. The provider's own documentation is
[the OpenSky REST API](https://openskynetwork.github.io/opensky-api/rest.html);
the facts below are the consequences of it for this demo.

**Two skills share this name.** There is a general `api-contract` skill
describing the versioned-contract pattern — marker, versioned interface, one
`internal sealed` concretion with explicit interface implementation. It governs
*only* the contract layer below. Everything above the contract — the client,
the cache, the strategies, the decorator, the tracker — is this repository's own
design, because that pattern is silent on caching, observables and streaming and
defines no layer above the contract. Do not claim conformance for them.

## One responsibility each

See [ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md) for why
each of these is separate.

```csharp
// 1. API types — what OpenSky sends. The envelope mirrors the provider: its
//    reported time plus positional rows. The row type does not escape here.

// 2. The API contract — one method per endpoint. Task<T>, CancellationToken last.
//    No IObservable, cache, changeset, bounding box, interval or credential on it.
//    No version suffix: OpenSky publishes no version to be agnostic about.
public interface IOpenSkyApiContract
{
    Task<StatesResponse> GetAllStates(
        BoundingBox box, bool extended, CancellationToken cancellationToken);
}
// Exactly one internal sealed class implements it, explicitly, with nothing public.
// Lives in src/Transponder/Integrations/OpenSky/, never under Features/.

// 3. The snapshot — the server's record with names on it. Value equality over
//    every member; keyed on icao24; converts, derives and interprets nothing.
public sealed record AircraftSnapshot( /* … */ );

// 4. The client — contract and cache by constructor; constructs neither.
//    Reads rows into snapshots, writes the whole set as one differential update.
//    Owns the bounding box, the interval, the token, the credit header and 429.

// 5. The cache — a plain keyed store of snapshots. One per client, typed to that
//    client's snapshot. No diff policy of its own, no projection, no clock.

// 6. The strategy seam — where sources genuinely look alike.
public interface ITrackerSource
{
    IObservable<IChangeSet<TransportVehicle, string>> Connect();
}
// IAirplaneTrackerSource and IVesselTrackerSource adhere to it, and each owns its
// own Mapperly projection. THIS is the first and only place a domain object is built.

// 7. The decorator — registered as ITrackerSource, selects the live strategy at
//    runtime. No resolver type; nobody asks which strategy to use.

// 8. IFleetTracker — wraps ITrackerSource, owns the pipeline and the injected clock.
//    What view models depend on. See dynamic-data-pipeline.
```

**The writer owns the write.** A polling client applies a differential update
over a whole fetched set; a push client adds and removes what it was told about.
The cache cannot tell the difference and has no opinion either way — which is
what lets each transport write the way it can, instead of forcing a push feed to
assemble a full set so a shared differ can run.

**The seam is at the domain boundary, not the wire one.** Both feeds produce
records; what *differs* is how a record becomes a `TransportVehicle`. Putting
the seam where sources differ is what makes the plane-to-ship swap a new
strategy rather than a new pipeline — see
[`hot-swap-source`](../hot-swap-source/SKILL.md).

**Not every strategy has every layer.** A contract is one `Task<T>` per
endpoint, so it exists only where the provider is request/response shaped. The
vessel feed is a socket — subscribe, then receive — and there is no request to
return a response, so **it has no contract layer and none is invented for it**
([`ais-stream`](../ais-stream/SKILL.md)). Its named JSON also means no
hand-written rows step, so it is two components shorter than the polled
strategy. That asymmetry costs nothing: what the strategies share is
`ITrackerSource`, which is the only place they genuinely look alike. A fake
contract wrapping a socket buys symmetry on a diagram and a lie in the code.

- Strategies to date: live OpenSky (polled), AISStream vessels (push, the
  stretch goal), replay-from-recording, and a simulated source — see
  [`api-mock`](../api-mock/SKILL.md).
- Source-specific controls (bounding box, interval, credentials) are constructor
  or options input to one client, never on a contract or a seam.
- A source that needs to tell the UI *about itself* — display name, which
  columns make sense — does so through a separate small description, not by
  widening a per-type interface.

Two mappings, two owners, two mechanisms: **rows → snapshot** is hand-written in
the client, because positional arrays are not something Mapperly can map;
**snapshot → domain** is Mapperly in the strategy. See
[`mapping`](../mapping/SKILL.md).

## OpenSky: facts, not preferences

All of this is the provider's behavior and is not ours to simplify. The
details live in [`README.md`](../../README.md) § "Data source: OpenSky
Network"; the consequences are here.

- **No OpenAPI spec exists**, so there is no generator to look for. The
  contract's implementation is written with Flurl over `System.Text.Json` —
  [`http-client`](../http-client/SKILL.md) has the mechanics,
  [ADR-0001](../../.spec/adr/0001-flurl-for-http.md) the reasoning for Flurl,
  and [ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md) the
  reasoning for the components above.
- **OpenSky publishes no API version either** — there is no `/v1/` in the path.
  The versioned-contract pattern's rule is that a suffix matches the
  *provider's* major version, so an invented `V1` would be the internal version
  number that rule forbids. **So the contract carries no suffix and has no
  marker above it.** The marker-and-versioned split arrives the day OpenSky
  declares a version — there is nothing to be version-agnostic about until then,
  and a split with one member on each side buys nothing. A provider that *does*
  version gets the full pattern from its first line of code.
- **`states` is an array of arrays.** Fields are positional, not named, so the
  rows are read by index in the client, and the index→field table is the
  contract. Read positions by index against that table; never by guessing from
  a sample payload.
- **The envelope carries the provider's own reported time.** That is the
  observed instant for everything downstream, so nothing reads an ambient clock
  to answer "as of when".
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
gets its own contract, client and strategy if and when that clears; it is not
wired in by default.

## Never add

- A second strategy seam. Two of them means the swap has to bridge them, and
  the teaching point of the demo dies.
- OpenSky concepts (credits, bounding boxes, tokens) on a contract or a seam.
- An `IObservable`, a cache or a changeset on the API contract. The pattern's
  rule is `Task<T>` per endpoint, and a contract that streams is not a contract.
- More than one production class implementing the contract, a `public` endpoint
  method on it, or its type being resolvable from outside the integration code.
- An edit to a contract interface a class already implements. A new provider
  version is a new interface.
- A cache with a diff policy, a projection, or a clock. The writer owns the
  write; the projection is the strategy's; the clock is `IFleetTracker`'s.
- A domain type held, constructed or returned by a cache, or a domain object
  built anywhere but a strategy's projection.
- A strategy resolver that callers ask which strategy to use. The decorator is
  registered as the seam and nobody asks it anything.
- A generated client, or a positional row read outside the client.
- A retry that ignores `X-Rate-Limit-Retry-After-Seconds`, or a poll loop with
  no interval ceiling. Burning the daily credit budget before the talk is a
  self-inflicted outage.
- A credential in source control or in a log line.
