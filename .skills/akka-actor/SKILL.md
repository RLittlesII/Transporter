---
name: akka-actor
description: Write and register Akka.NET actors in Transponder — the poller and the WebSocket reader own scheduling, retry and reconnect, while the DynamicData cache owns collection state. Use when adding an actor, a message, or actor wiring.
---

# Akka actors in Transponder

Akka.NET is a decided technology ([`README.md`](../../README.md) §
"Technology Decisions"). **The actor *shape* below is not decided** — it is
what the sample
[`ClickActor`](../../src/Transponder/Features/Demo/Actors/ClickActor.cs)
demonstrates, taken from Akka's own documentation. Follow it so the codebase
reads as one thing, and change it in one move if something better shows up.

## Who owns what

The division matters more than the syntax:

- **Actors own time and failure.** The poll interval, the token refresh, the
  retry after a `429`, the WebSocket reconnect, the backoff. Anything that
  needs to happen *again later* is an actor's job.
- **The DynamicData cache owns collection state.** Which items exist, what
  changed, who expired — see
  [`dynamic-data-pipeline`](../dynamic-data-pipeline/SKILL.md).
- An actor therefore emits snapshots and never holds the collection the UI
  binds to. Two places remembering the fleet is one place too many.

## The shape

From `ClickActor`:

- Derive from `ReceiveActor`; wire `Receive<TMessage>` in the constructor.
- Expose **`public static Props Props { get; }`** built with a static lambda:
  `Props.Create(static () => new ClickActor())`. Callers use the property, not
  a `Props.Create` at the call site.
- Messages are `internal`, immutable, and declared beside the actor. A
  parameterless message gets a private constructor and a
  `static readonly Instance`, so there is one of it:

  ```csharp
  internal class Click
  {
      private Click() { }

      public static readonly Click Instance = new();
  }
  ```

- Actors live under `src/Transponder/Features/<Area>/Actors` (`AGENTS.md`).
- State is actor-local. The `AtomicCounter` in `ClickActor` is private to the
  actor and stays that way — it is not an invitation to share mutable state
  between actors.

## Registration and consumption

- Register through `AddAkkaHost` in
  [`MauiProgram`](../../src/Gui/MauiProgram.cs), which wraps
  [`AkkaHostBuilder`](../../src/Gui/Container/AkkaHostBuilder.cs):

  ```csharp
  .AddAkkaHost("Transponder", static (system, registry) =>
      registry.Register<ClickActor>(system.ActorOf(ClickActor.Props)))
  ```

- View models take `IActorRegistry` and resolve from it, as
  [`DemoViewModel`](../../src/Transponder/Features/Demo/ViewModels/DemoViewModel.cs)
  does. **Every `Ask<T>` carries an explicit timeout** — the sample uses five
  seconds. An `Ask` with no timeout is a hang waiting for a bad network.
- Prefer `Tell` and a pushed result over `Ask` for anything streaming. `Ask`
  is request/response; snapshots are a stream. Which route a given piece of
  user input takes is settled in [`mvvm`](../mvvm/SKILL.md) "Two kinds of
  input, two routes".

## The sources are actors

- **The poller** schedules itself at the credit-budgeted interval, refreshes
  the OpenSky token on expiry or `401`, and honors
  `X-Rate-Limit-Retry-After-Seconds` on a `429`. It owns *when* and *how often*;
  the HTTP mechanics belong to a plain client class it calls
  ([`http-client`](../http-client/SKILL.md)), which is also what keeps that
  client testable — `HttpTest` does not reach inside an actor.
- **The WebSocket reader** (the ships stretch goal) owns the socket lifetime:
  connect, subscribe with the bounding box, read, and reconnect when the
  socket drops — see [`ais-stream`](../ais-stream/SKILL.md). A dropped socket
  is normal operation, not an error path.
- Supervision: **restart the reader without tearing down the cache.** The
  whole point of the swap demo is that the cache and everything after it are
  untouched by what happens upstream. A supervision strategy that kills the
  cache on a socket blip kills the demo with it.
- A source actor that is swapped away is **stopped**, not left running. A
  leaked poller keeps spending credits behind a demo that has moved on.

## Testing

`Akka.TestKit` is **not yet in**
[`Directory.Packages.props`](../../Directory.Packages.props). Adding it is
part of the first issue that needs to test an actor, not a prerequisite for
reading this file. Until then, keep actor logic thin enough that the behavior
under test lives in a plain class the actor calls.

## Never add

- An `Ask` without a timeout.
- Shared mutable state between actors, or a collection of tracked items held
  in an actor.
- Blocking inside `Receive` — no `.Result`, no `.Wait()`.
- A supervision strategy that restarts the cache or the pipeline when an
  upstream source fails.
- A source actor left running after its source has been swapped out.
