---
title: "ADR-0001: Flurl for the polled HTTP client"
description: "Use Flurl.Http for the OpenSky client instead of a hand-rolled HttpClient, for its request assertions, token hooks and response access."
type: adr
---

# ADR-0001: Flurl for the polled HTTP client

**Status:** accepted

## Context

The demo's data source is a REST endpoint polled on a timer
([`README.md`](../../README.md) § "Data source"), and its awkwardness is all in
the places a hand-rolled client is easy to get wrong:

- **A bounding box of four query parameters** plus `extended=1`, assembled on
  every call. `extended=1` is the only thing that supplies aircraft category, so
  losing it degrades the grouped view silently rather than loudly.
- **OAuth2 client credentials only**, with 30-minute tokens that must refresh on
  expiry _and_ on a `401`, because a token can die early.
- **A credit budget that the response reports.** `X-Rate-Limit-Remaining` is how
  a rehearsal learns its burn rate, and a `429` carries
  `X-Rate-Limit-Retry-After-Seconds` that should be honoured as given.
- **A positional `states` array** — fields by index, not by name — which needs a
  custom `JsonConverter` whatever client wraps it.
- **Tests that must never touch the network** and must still prove the request
  was shaped correctly.

There is no OpenAPI document, so a generated client was never an option.

The original plan of record said to write the client by hand with `HttpClient`
and `System.Text.Json`. That reverses here.

## Decision drivers

- Tests that assert the _request_, not only the parsed response.
- Token and rate-limit plumbing that lives in one place, not at every call site.
- No second JSON serializer: the positional-array converter must keep working.
- The talk's subject is the DynamicData pipeline — the HTTP layer should be
  small enough that nobody reads it on stage.

## Considered options

| Option                            | Summary                                                                                                                           | Why not                                                                                                                                                                                                                               |
| --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Hand-rolled `HttpClient`          | No dependency; full control.                                                                                                      | Every item above is written and tested by us: query assembly, a `BeforeCall` equivalent for tokens, retry-after handling, and a fake `HttpMessageHandler` per test. That is the most code for the least interesting part of the demo. |
| Refit or another generated client | Typed interface from attributes.                                                                                                  | There is no OpenAPI spec to generate from, and a positional array fits an attribute-driven interface badly. Adds a generator for no gain.                                                                                             |
| **Flurl.Http**                    | **Chosen.** Fluent URL building, `BeforeCall`/`OnError` hooks, `IFlurlResponse` header access, `AllowHttpStatus`, and `HttpTest`. | —                                                                                                                                                                                                                                     |

## Decision

Use `Flurl.Http` for the polled source. Verified before adopting it:

- version **4.0.2**, which serializes with `System.Text.Json` and pulls **no
  Newtonsoft** on modern targets, so the positional-array converter plugs into
  `JsonSerializerOptions` via `DefaultJsonSerializer`;
- `GetAsync()` yields an `IFlurlResponse` whose headers are readable before the
  body is deserialized;
- `AllowHttpStatus(429)` makes a throttle inspectable instead of an exception;
- `IFlurlClientCache` as a singleton is the supported way to avoid socket
  exhaustion;
- `HttpTest` fakes responses and asserts URL, verb, query parameters, headers
  and call count.

Usage rules are in [`flurl-http-client`](../../.skills/flurl-http-client/SKILL.md).

## Consequences

- A dependency where the framework would have done. Accepted for the testing
  story and the token plumbing, not for elegance.
- **`HttpTest` intercepts through the logical async call context**, so it does
  not follow a message into an actor on its own dispatcher. The client must stay
  a plain class that is tested directly, with the poller actor tested against a
  stub. This constrains the design, and the constraint is one the repository
  already wanted.
- `Flurl` and `Flurl.Http` are pinned in `Directory.Packages.props`;
  transitive pinning is on, so both are listed explicitly.
- **The ships feed is unaffected.** AISStream is a WebSocket, so it keeps
  `ClientWebSocket` or the community package. Two sources, two transports, one
  seam.
- `README.md` § "Data source" and § "Technology Decisions" are updated, so the
  reversed plan does not survive anywhere as the current one.
