---
name: api-contract
description: The components between a provider and the domain — API types, the typed contract, the snapshot, the client, the cache, the strategy seam, the swap decorator and the tracker — and which responsibility each one owns. Use when touching a contract, a client, a cache or a strategy.
---

# The tracking contract and the components above it

This file covers **what each component between a provider and the domain is
for**. The reasoning, the rejected alternatives and the cost are recorded in
[ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md); a
provider's own behavior is the specification's to carry, in `README.md` and the
Feature's `.spec/README.md`, never restated here.

**Two skills share this name.** The general `api-contract` skill describes the
versioned-contract pattern — marker, versioned interface, one `internal sealed`
concretion with explicit interface implementation. It governs **only** the
contract layer. Everything above the contract — the client, the cache, the
strategies, the decorator, the tracker — is this repository's own design,
because that pattern is silent on caching, observables and streaming and defines
no layer above the contract. Do not claim conformance for them.

## One responsibility each

```csharp
// 1. API types — what the provider sends. The envelope mirrors the provider,
//    including its own reported time. Wire-shaped types do not escape the
//    contract's implementation.

// 2. The API contract — one method per endpoint. Task<T>, CancellationToken last.
//    No IObservable, cache, changeset, source-specific control or credential on it.
public interface IProviderApiContract
{
    Task<TResponse> GetSomething(TRequest request, CancellationToken cancellationToken);
}
// Exactly one internal sealed class implements it PER TRANSPORT, explicitly,
// with nothing public: one over HTTP, one reading a recording. A recorded
// source substitutes here, so the client, cache and projection above it are
// unchanged. It lives in the provider's integration folder, never under a
// feature.

// 3. The snapshot — the server's record with names on it. Value equality over
//    every member; keyed; converts, derives and interprets nothing.

// 4. The client — contract and cache by constructor; constructs neither.
//    Reads wire payloads into snapshots, writes the whole set as one
//    differential update. Owns the source-specific controls: the request
//    parameters, the interval, the token, the budget headers and the throttle
//    response.

// 5. The cache — a plain keyed store of snapshots. One per client, typed to that
//    client's snapshot. No diff policy of its own, no projection, no clock.

// 6. The strategy seam — where sources genuinely look alike.
public interface ITrackerSource
{
    IObservable<IChangeSet<TransportVehicle, string>> Connect();
}
// A per-type strategy adheres to it and owns its own projection. THIS is the
// first and only place a domain object is built.

// 7. The decorator — registered as the seam, selects the live strategy at
//    runtime. No resolver type; nobody asks which strategy to use.

// 8. The tracker — wraps the seam, owns the pipeline and the injected clock.
//    What view models depend on. See dynamic-data-pipeline.
```

## The rules that arrangement exists for

**The writer owns the write.** A polling client applies a differential update
over a whole fetched set; a push client adds and removes what it was told about.
The cache cannot tell the difference and has no opinion either way — which is
what lets each transport write the way it can, instead of forcing a push feed to
assemble a full set so a shared differ can run.

**The seam is at the domain boundary, not the wire one.** Both kinds of feed
produce provider records; what *differs* is how a record becomes a domain item.
Putting the seam where sources differ is what makes a new source a new strategy
rather than a new pipeline ([`hot-swap-source`](../hot-swap-source/SKILL.md)).

**Not every strategy has every layer.** A contract is one `Task<T>` per
endpoint, so it exists only where the provider is request/response shaped. A
socket provider — subscribe, then receive — has no request to return a response,
so **it has no contract layer and none is invented for it**. Where its JSON is
also named, it needs no hand-written payload-reading step either, making it two
components shorter. That asymmetry costs nothing: what strategies share is the
seam, which is the only place they genuinely look alike. A fake contract
wrapping a socket buys symmetry on a diagram and a lie in the code.

**A version suffix tracks the provider's version, never an internal one.** A
provider publishing no version has nothing to be agnostic about, so its contract
carries no suffix and no marker above it; the marker-and-versioned split arrives
the day the provider declares a version. A provider that *does* version gets the
full pattern from its first line of code.

**Substitution happens at the deepest layer that exists.** For a
request/response provider a recorded source substitutes at the contract, so that
provider is replayed with no strategy of its own; a push provider has no
contract to stand in for, so its recorded source substitutes at the strategy.
Different depths, same seam — what makes something a swap target is the seam it
lands on, not its depth.

**Source-specific controls are constructor or options input to one client**,
never on a contract or a seam. A source that needs to tell the UI *about itself*
— display name, which columns make sense — does so through a separate small
description, not by widening a per-type interface.

## Two mappings, two owners

**Wire payload → snapshot** is hand-written in the client, because a positional
payload is not something a name-based mapper can map. **Snapshot → domain** is
the mapper, in the strategy. See [`mapping`](../mapping/SKILL.md).

## Credentials

Credentials come from user secrets or environment variables. **Never committed,
never logged, never in a test fixture, never in a screenshot of the running
app.** A token value is not debug output: log that a refresh happened, not what
it returned. A missing credential fails loudly at startup with a message naming
which one is absent.

## Never add

- A second strategy seam. Two of them means a swap has to bridge them.
- A provider's own concepts — budgets, request parameters, tokens — on a
  contract or a seam.
- An `IObservable`, a cache or a changeset on the API contract. A contract that
  streams is not a contract.
- More than one production class implementing the contract **for the same
  transport**, a `public` endpoint method on it, or any implementing type being
  resolvable from outside the integration code.
- An edit to a contract interface a class already implements. A new provider
  version is a new interface.
- A cache with a diff policy, a projection, or a clock.
- A domain type held, constructed or returned by a cache, or a domain object
  built anywhere but a strategy's projection.
- A strategy resolver that callers ask which strategy to use.
- A generated client, or a wire-shaped payload read outside the client.
- A retry that ignores the provider's retry-after header, or a poll loop with no
  interval ceiling.
- A credential in source control or in a log line.
