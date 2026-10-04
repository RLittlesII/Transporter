---
name: api-contract
description: The two declarations between a provider and the domain — the API contract and the strategy seam — the traps around them, and where each component's responsibility is recorded. Use when touching a contract, a client, a cache or a strategy.
---

# The tracking contract and the components above it

This file carries the **declarations and the traps**. What each component
between a provider and the domain is for, why it is separate, the rejected
alternatives and the cost are all
[ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md)'s, and a
provider's own behavior is the specification's — `README.md` and the Feature's
`.spec/README.md`. Neither is restated here.

**Two skills share this name.** The general `api-contract` skill describes the
versioned-contract pattern — marker, versioned interface, one `internal sealed`
concretion with explicit interface implementation. It governs **only** the
contract layer. Everything above the contract — the client, the cache, the
strategies, the decorator, the tracker — is this repository's own design,
because that pattern is silent on caching, observables and streaming and defines
no layer above the contract. Do not claim conformance for them.

## The two declarations

Everything else about the arrangement — what each component is for, why it is
separate, and what it cost — is
[ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md) § Decision,
item by item. What that record does not give you is the code, so here it is:

```csharp
// The API contract — one method per endpoint. ADR-0002 item 2.
public interface IProviderApiContract
{
    Task<TResponse> GetSomething(TRequest request, CancellationToken cancellationToken);
}

// The strategy seam — where sources genuinely look alike. ADR-0002 item 6.
public interface ITrackerSource
{
    IObservable<IChangeSet<TransportVehicle, string>> Connect();
}
```

| Component | ADR-0002 item |
| --- | --- |
| API types and the envelope | 1 |
| The API contract | 2 |
| The snapshot | 3 |
| The snapshot client | 4 |
| The cache | 5 |
| The tracker source strategy | 6 |
| The swap decorator | 7 |
| The fleet tracker | 8 |

Two mappings, two owners — ADR-0002 states it after item 8: rows to snapshot is
hand-written in the client, snapshot to domain is the mapper in the strategy
([`mapping`](../mapping/SKILL.md)).

## The boundary throws

**`TResponse` is the payload.** Never a wrapper around a failure, and never a
union with one in it
([ADR-0008](../../.spec/adr/0008-the-contract-is-the-boundary-and-may-throw.md)).

- An **actionable failure gets its own exception type**, declared with the
  interface and holding whatever the handler needs. Nothing upstream unpacks a
  transport library's exception to find it.
- Anything else propagates untyped.
- **The Failure/Result track begins above the contract**, at the first
  component inside the application. One place turns a thrown call into a value
  ([`language-ext-usage`](../language-ext-usage/SKILL.md)).

## The traps

**Not every strategy has every layer.** A contract is one `Task<T>` per
endpoint, so it exists only where the provider is request/response shaped. A
socket provider — subscribe, then receive — has no request to return a response,
so **it has no contract layer and none is invented for it**. Where its JSON is
also named, it needs no hand-written payload-reading step either, making it two
components shorter. That asymmetry costs nothing: what strategies share is the
seam. **A fake contract wrapping a socket buys symmetry on a diagram and a lie
in the code.**

**Substitution happens at the deepest layer that exists**, which is why a
recorded source for a request/response provider stands in at the contract and
one for a push provider stands in at the strategy. Different depths, same seam.
What makes something a swap target is the seam it lands on, not its depth.

**A version suffix tracks the provider's version, never an internal one.** A
provider publishing no version has nothing to be agnostic about, so an invented
`V1` is the internal version number the pattern forbids.

**Source-specific controls are constructor or options input to one client**,
never on a contract or a seam. A source that needs to tell the UI *about itself*
— display name, which columns make sense — does so through a separate small
description, not by widening a per-type interface.

## Credentials

Credentials come from user secrets or environment variables. **Never committed,
never logged, never in a test fixture, never in a screenshot of the running
app.** A token value is not debug output: log that a refresh happened, not what
it returned.

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
- A failure wrapper as a contract's return type. The boundary throws.
- A generated client, or a wire-shaped payload read outside the client.
- A retry that ignores the provider's retry-after header, or a poll loop with no
  interval ceiling.
- A credential in source control or in a log line.
