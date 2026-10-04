---
name: flurl-http-client
description: Satisfy an HTTP API contract with Flurl — the client cache, URL building, keeping the response, 429 as data, bearer-token refresh, the serializer, and HttpTest. Use when writing, changing, or testing an HTTP client implementation.
---

# The HTTP client

This file covers **how an API contract is satisfied over HTTP with Flurl**. The
contract itself, and the client, cache and projection above it, live in
[`api-contract`](../api-contract/SKILL.md). The library's own API lives at
[flurl.dev](https://flurl.dev); the reasoning for choosing it, and the rejected
alternative, are in
[ADR-0001](../../.spec/adr/0001-flurl-for-http.md).

Flurl serializes with `System.Text.Json`. Keep it that way: a second JSON
serializer in a repository is two places to configure a converter.

## One named client per provider

Clients come from a **singleton `IFlurlClientCache`**, not from
`new FlurlClient()` and not from a bare URL extension. A client per request
exhausts sockets; the cache is the documented answer.

```csharp
services.AddSingleton<IFlurlClientCache>(_ => new FlurlClientCache()
    .Add("<provider>", "<base url>", builder => builder
        .WithSettings(settings => settings.JsonSerializer = ProviderSerializer)));
```

Register it through a container builder block
([`transponder-conventions`](../transponder-conventions/SKILL.md)), and let a
client class take `IFlurlClientCache` by constructor and ask for its own named
client.

## Build the URL; never concatenate one

```csharp
_client.Request("states", "all")
       .SetQueryParams(new { lamin, lomin, lamax, lomax, extended = 1 })
```

Query parameters that must not be mis-encoded — coordinates, flags that change
the response shape — are exactly what the builder exists for.

## Read the response, not just the body

**Use `GetAsync()` and keep the `IFlurlResponse`.** The convenience
`GetJsonAsync<T>()` on a URL throws the response away, and the response is where
a metered provider says what budget is left:

```csharp
var response = await request.AllowHttpStatus(429).GetAsync();

var remaining = response.Headers.FirstOrDefault("<rate-limit-remaining header>");
// log it at debug — it is how a burn rate becomes visible before it bites

if (response.StatusCode == 429)
{
    var retryAfter = response.Headers.FirstOrDefault("<retry-after header>");
    // honour that value; never invent a backoff
}

var payload = await response.GetJsonAsync<TResponse>();
```

- **A 429 is data, not an exception.** `AllowHttpStatus(429)` is what makes it
  inspectable; without it Flurl throws and the retry-after header is buried in a
  `FlurlHttpException`.
- Everything else non-2xx stays an exception. An unexpected 500 should be loud.
- The provider's own documentation names the headers; the feature specification
  records which ones this repository reads.

## Tokens

- Attach the current token in a **`BeforeCall`** hook —
  `WithOAuthBearerToken(token)` — so no call site has to remember.
- Refresh on expiry **and** on a `401`, through **`OnError`**, retried once. A
  token can die early; assuming the clock is enough is how a client fails at the
  worst moment.
- The token cache is one place, owned by the client or its actor.
- **Log that a refresh happened, never what it returned.** A credential is not
  debug output, and a shared screen is a screenshot.

## JSON

One `JsonSerializerOptions`, carrying any converter the wire format forces,
wrapped for Flurl:

```csharp
private static readonly ISerializer ProviderSerializer =
    new DefaultJsonSerializer(new JsonSerializerOptions
    {
        Converters = { new ProviderArrayConverter() },
    });
```

Set the serializer **on the named client**, so a second provider with a
different wire shape is unaffected. A converter's job stops at making the
payload readable as the contract's own types; naming its fields is the client's
first act, and any projection to the domain runs later still
([`mapping`](../mapping/SKILL.md)).

## Testing with `HttpTest`

`HttpTest` fakes responses and asserts the request — no server, no hand-written
`HttpMessageHandler`:

```csharp
using var http = new HttpTest();
http.RespondWithJson(RecordedPayload);           // chainable; queued in order

var result = await sut.Fetch(parameters);

http.ShouldHaveCalled("*/states/all")
    .WithQueryParam("extended", 1)
    .WithVerb(HttpMethod.Get)
    .Times(1);
```

- Assert the **request shape**, not just the parsed result: the parameters, the
  flags that change the response, and that a bearer token was attached. Those
  are what a silent regression breaks.
- `SimulateTimeout()` for the dead-network path, and a queued `429` carrying a
  retry-after header for the throttle path.
- Recorded fixtures replay through `RespondWithJson`, so one body of synthetic
  data serves both the client tests and a replay source.

### The actor boundary — the trap worth knowing

`HttpTest` intercepts through the **logical asynchronous call context**. It
fakes calls the system under test makes within the `using` block, including
across `await` — but it does **not** follow a message sent to an actor
processing on its own dispatcher.

So:

- **Test the contract's implementation directly under `HttpTest`.** It is a
  plain class; give it the Flurl cache and call it.
- **Test everything above it against a hand-written fake of the contract**,
  never by hoping `HttpTest` reaches inside an actor.

That constraint pushed the seam to a good place: the client, the cache, the
strategies and the tracker are all testable with no HTTP at all. It is the same
split [`akka-actor`](../akka-actor/SKILL.md) and [`mvvm`](../mvvm/SKILL.md) ask
for — keep the logic in a plain class the actor calls — now with a concrete
reason to hold the line.

## Not for a push feed

A WebSocket provider has nothing to take from Flurl: it connects and receives,
so there is no request to build or response to read. Its reader is an actor
owning the socket lifetime ([`akka-actor`](../akka-actor/SKILL.md)). Both kinds
of source still land on the same seam
([`api-contract`](../api-contract/SKILL.md)); only the polled one goes through
this file.

## Never add

- A concatenated or interpolated URL.
- `GetJsonAsync<T>()` on a URL where a response header matters.
- Newtonsoft, or a second JSON serializer.
- `new FlurlClient()` per request, or a bare-URL call that bypasses the cache.
- A hand-rolled `HttpMessageHandler` or `HttpClient` fake in a test.
- A retry that ignores the provider's retry-after header, or a poll loop with no
  interval ceiling.
- A token, client id or secret in a log line, a fixture, or source control.
- A test that reaches a live provider.
