---
name: akka-actor
description: Write and register Akka.NET actors — actors own scheduling, retry and reconnect, while the reactive cache owns collection state. Use when adding an actor, a message, or actor wiring.
---

# Akka actors

For Akka's own API — `ReceiveActor`, `Props`, supervision, `TestKit` — see
[akka.net](https://getakka.net). This file covers **what an actor is for here,
and the shape every actor in this repository follows**.

## Who owns what

The division matters more than the syntax:

- **Actors own time and failure.** A poll interval, a token refresh, a retry
  after a throttle response, a socket reconnect, a backoff. Anything that needs
  to happen _again later_ is an actor's job.
- **The reactive cache owns collection state.** Which items exist, what changed,
  what expired ([`dynamic-data-pipeline`](../dynamic-data-pipeline/SKILL.md)).
- An actor therefore emits snapshots and **never holds the collection the UI
  binds to**. Two places remembering the same set is one place too many.

## The shape

- Derive from `ReceiveActor`; wire `Receive<TMessage>` in the constructor.
- **An actor with no collaborators exposes
  `public static Props Props { get; }`** built with a static lambda:
  `Props.Create(static () => new ThingActor())`. Callers use the property, not a
  `Props.Create` at the call site.
- **An actor that takes a collaborator the container owns has no `Props` of its
  own.** A static property cannot reach the service provider, and a `Props`
  overload listing the dependencies is the same second place a registration
  already is. Akka's `IDependencyResolver` builds it instead — the registration
  takes `(system, registry, resolver)` and calls `resolver.Props<ThingActor>()`,
  and the constructor is resolved from the container like any other.
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

- State is actor-local and private. It is not an invitation to share mutable
  state between actors.
- Actors live beside the feature they serve
  ([`transporter-conventions`](../transporter-conventions/SKILL.md)).

## Registration and consumption

- Register through the container's Akka builder block, not ad-hoc at a call
  site:

    ```csharp
    .AddAkkaHost("<name>", static (system, registry) =>
        registry.Register<ThingActor>(system.ActorOf(ThingActor.Props)))
    ```

    With a container-owned collaborator, take the resolver and let it build the
    `Props`. Where the actor and its collaborator are internal to a library, the
    library exposes the registration and the host names only that:

    ```csharp
    .AddAkkaHost("<name>", static (system, registry, resolver) =>
        registry.AddThingActors(system, resolver))
    ```

- View models take `IActorRegistry` and resolve from it. **Every `Ask<T>`
  carries an explicit timeout**; an `Ask` with no timeout is a hang waiting for
  a bad network.
- Prefer `Tell` and a pushed result over `Ask` for anything streaming. `Ask` is
  request/response; a stream of snapshots is not. Which route a given piece of
  user input takes is settled in [`mvvm`](../mvvm/SKILL.md) "Two kinds of input,
  two routes".

## A source is an actor

- **A poller** schedules itself, refreshes its token on expiry or on a `401`,
  and honours the provider's retry-after header on a throttle response. It owns
  _when_ and _how often_; the HTTP mechanics belong to a plain client class it
  calls ([`flurl-http-client`](../flurl-http-client/SKILL.md)) — which is also
  what keeps that client testable, since `HttpTest` does not reach inside an
  actor.
- **A socket reader** owns the socket lifetime: connect, subscribe, read, and
  reconnect when the socket drops. **A dropped socket is normal operation, not
  an error path**, and a push provider needs no request/response contract
  invented for it ([`api-contract`](../api-contract/SKILL.md)).
- **Supervision restarts the reader without tearing down the cache.** The cache
  and everything after it are untouched by what happens upstream; a supervision
  strategy that kills the cache on a socket blip takes the whole collection with
  it.
- A source actor that is swapped away is **stopped**, not left running. A leaked
  poller keeps spending a provider's budget behind a source nothing is reading
  ([`hot-swap-source`](../hot-swap-source/SKILL.md)).

## Testing

Keep actor logic thin enough that the behavior under test lives in a plain class
the actor calls. Use `Akka.TestKit` for what genuinely needs the actor itself —
a message that must be sent, a supervision decision — and
[`test-from-scenarios`](../test-from-scenarios/SKILL.md) for the rest.

## Never add

- An `Ask` without a timeout.
- Shared mutable state between actors, or a collection of tracked items held in
  an actor.
- Blocking inside `Receive` — no `.Result`, no `.Wait()`.
- A supervision strategy that restarts the cache or the pipeline when an
  upstream source fails.
- A source actor left running after its source has been swapped out.
