---
name: http-client
description: How an ITrackingSource implementation talks to a provider over HTTP with Flurl — URL building, bearer-token refresh, rate-limit headers, 429 handling, the positional-array serializer, and HttpTest. Use when writing or changing a client, or testing one.
---

# The HTTP client

This file covers **how the source seam is satisfied over HTTP**. The seam
itself and the provider's own behavior live in
[`api-contract`](../api-contract/SKILL.md); the library's API lives at
[flurl.dev](https://flurl.dev).

`Flurl.Http` is referenced by `src/Transponder` and pinned in
[`Directory.Packages.props`](../../Directory.Packages.props). The reasoning and
the rejected alternative are recorded in
[`.spec/adr/0001-flurl-for-http.md`](../../.spec/adr/0001-flurl-for-http.md).

**On version 4**: Flurl serializes with `System.Text.Json` — there is no
Newtonsoft anywhere in this repository, and the positional-array converter
OpenSky forces on us plugs straight into `JsonSerializerOptions`. Keep it that
way.

## One named client per provider

Clients come from a **singleton `IFlurlClientCache`**, not from `new
FlurlClient()` and not from a bare URL extension. A client per request
exhausts sockets; the cache is the documented answer.

```csharp
// Provisional shape: follows the existing extension(MauiAppBuilder) blocks.
services.AddSingleton<IFlurlClientCache>(_ => new FlurlClientCache()
    .Add("OpenSky", "https://opensky-network.org/api", builder => builder
        .WithSettings(settings => settings.JsonSerializer = StatesSerializer)));
```

Register it through a builder block in `src/Gui/Container/`, beside
[`AkkaHostBuilder`](../../src/Gui/Container/AkkaHostBuilder.cs) and
[`UserInterfaceBuilder`](../../src/Gui/Container/UserInterfaceBuilder.cs),
rather than piling registrations into
[`MauiProgram`](../../src/Gui/MauiProgram.cs). A client class takes
`IFlurlClientCache` by constructor and asks for its own named client.

## Build the URL; never concatenate one

```csharp
_client.Request("states", "all")
       .SetQueryParams(new { lamin, lomin, lamax, lomax, extended = 1 })
```

The bounding box is four numbers that must not be mis-encoded, and
`extended=1` is the only thing that supplies aircraft category at all — drop
it and grouping by category silently has nothing to group.

## Read the response, not just the body

**Use `GetAsync()` and keep the `IFlurlResponse`.** The convenience
`GetJsonAsync<T>()` on a URL throws the response away, and for OpenSky the
response carries the thing a rehearsal most needs to know:

```csharp
var response = await request.AllowHttpStatus(429).GetAsync();

var remaining = response.Headers.FirstOrDefault("X-Rate-Limit-Remaining");
// log `remaining` at debug — it is how you learn the burn rate before a talk

if (response.StatusCode == 429)
{
    var retryAfter = response.Headers.FirstOrDefault("X-Rate-Limit-Retry-After-Seconds");
    // honour that value; do not invent a backoff
}

var states = await response.GetJsonAsync<StatesResponse>();
```

- **A 429 is data, not an exception.** `AllowHttpStatus(429)` is what makes it
  inspectable; without it Flurl throws and the retry-after header is buried in
  a `FlurlHttpException`.
- Everything else non-2xx stays an exception. An unexpected 500 should be loud.

## Tokens

OpenSky is OAuth2 client-credentials only, with 30-minute tokens
([`api-contract`](../api-contract/SKILL.md)).

- Attach the current token in a **`BeforeCall`** hook —
  `WithOAuthBearerToken(token)` — so no call site has to remember.
- Refresh on expiry **and** on a `401` through **`OnError`**, retried once. A
  token can die early; assuming the clock is enough is how a demo fails
  mid-sentence.
- The token cache is one place, owned by the client or its actor.
- **Log that a refresh happened, never what it returned.** A credential is not
  debug output, and a projector is a screenshot.

## JSON

One `JsonSerializerOptions`, carrying the positional-array converter, wrapped
for Flurl:

```csharp
private static readonly ISerializer StatesSerializer =
    new DefaultJsonSerializer(new JsonSerializerOptions
    {
        Converters = { new StateVectorConverter() },
    });
```

The converter turns OpenSky's array-of-arrays into a named wire DTO; the
mapper takes it from there
([`mapping`](../mapping/SKILL.md)). Set the serializer **on the named client**,
so a second provider with named fields is unaffected.

## Testing with `HttpTest`

`HttpTest` fakes responses and asserts the request — no server, no
hand-written `HttpMessageHandler`:

```csharp
using var http = new HttpTest();
http.RespondWithJson(RecordedSnapshot);          // chainable; queued in order

var snapshot = await sut.Fetch(BoundingBox);

http.ShouldHaveCalled("*/states/all")
    .WithQueryParam("extended", 1)
    .WithVerb(HttpMethod.Get)
    .Times(1);
```

- Assert the **request shape**, not just the parsed result: the bounding box,
  `extended=1`, and that a bearer token was attached. Those are the things a
  silent regression breaks.
- `SimulateTimeout()` for the dead-network path, and a queued `429` with a
  retry-after header for the throttle path. Both are states the demo will meet.
- Recorded fixtures from [`api-mock`](../api-mock/SKILL.md) replay through
  `RespondWithJson`, so one body of synthetic data serves the client tests and
  the replay source.

### The actor boundary — the trap worth knowing

`HttpTest` intercepts through the **logical asynchronous call context**. It
therefore fakes calls the system under test makes within the `using` block,
including across `await` — but it does **not** follow a message sent to an
actor that is processing on its own dispatcher.

So:

- **Test the client class directly under `HttpTest`.** It is a plain class;
  give it the cache and call it.
- **Test the poller actor against a stubbed `ITrackingSource`**, never by
  hoping `HttpTest` reaches inside it.

This is the same split [`akka-actor`](../akka-actor/SKILL.md) and
[`mvvm`](../mvvm/SKILL.md) already ask for — keep the logic in a plain class
the actor calls — now with a concrete reason to hold the line.

## Not for the ships feed

AISStream is a **WebSocket**, so Flurl has nothing to offer it. That source
uses `ClientWebSocket` or the community package
([`ais-stream`](../ais-stream/SKILL.md)). Both sources still satisfy the same
seam; only the polled one goes through this file.

## Never add

- A concatenated or interpolated URL.
- `GetJsonAsync<T>()` on a URL where a response header matters — for OpenSky,
  that is every call.
- Newtonsoft, or a second JSON serializer.
- `new FlurlClient()` per request, or a bare-URL call that bypasses the cache.
- A hand-rolled `HttpMessageHandler` or `HttpClient` fake in a test.
- A retry that ignores `X-Rate-Limit-Retry-After-Seconds`, or a poll loop with
  no interval ceiling.
- A token, client id or secret in a log line, a fixture, or source control.
- A test that reaches `opensky-network.org`.
