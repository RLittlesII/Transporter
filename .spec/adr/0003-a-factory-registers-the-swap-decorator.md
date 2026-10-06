---
title: "ADR-0003: A factory registers the swap decorator"
description: "Register the tracker-source swap decorator with a factory over the per-type seams rather than with Scrutor's Decorate<>, which wraps one registration at a time and so cannot select between strategies."
type: adr
---

# ADR-0003: A factory registers the swap decorator

**Status:** proposed

## Context

[ADR-0002](0002-contract-client-strategy-tracker.md) puts the source swap in a
**decorator registered as `ITrackerSource`**: every strategy adheres to that
interface, the decorator does too, and a consumer resolving the seam gets the
decorator without asking anything. There is deliberately no strategy resolver.

That shape has one piece of plumbing to settle. A decorator registered as
`ITrackerSource` that needs the strategies it selects between cannot simply
inject `IEnumerable<ITrackerSource>` — it is itself registered as
`ITrackerSource`, so it would be handed itself. The container has to be told
which registration is the selector and which are the strategies.

`Microsoft.Extensions.DependencyInjection` has no first-class decoration. The
ways out are keyed services, naming each strategy by its own interface, a
library that rewrites the registration, or registering the selector as a
factory over the strategies.

**An earlier version of this record chose Scrutor, on an assumption that is
false.** It read that `services.Decorate<ITrackerSource, SwappingTrackerSource>()`
"rewrites the prior registrations so the decorator receives them" — plural. It
does not. `Decorate<>` is a one-to-one wrapper: it replaces **each** matching
registration with a decorator over **that** registration. Two strategies
registered as `ITrackerSource` produce two decorators, and
`GetRequiredService<ITrackerSource>()` returns the one wrapping whichever was
registered last. A decorator whose constructor asks for the set fails to
activate at all.

The assumption went unexamined through this record, through
`aircraft-source` § 7, through `replay-source` § 4 row 12, and into two items'
risk rows, until `0006` ran it against Scrutor 7.0.0 on 2026-10-06:

| Decorator constructor            | Two `ITrackerSource` registrations                                                         |
| -------------------------------- | ------------------------------------------------------------------------------------------ |
| `(ITrackerSource inner)`         | Two decorators — `Decorator(First)`, `Decorator(Second)`; the seam resolves the second one |
| `(IEnumerable<ITrackerSource> )` | `InvalidOperationException: A suitable constructor … could not be located`                 |

The probe was deleted with the item that wrote it; what it established is this
record's, and [lesson 0016](../lessons/0016-an-untested-assumption-is-not-a-decision.md)
is how it came to be written down as settled in the first place.

## Decision drivers

- Adding a strategy should touch the registration and nothing else — not a
  constructor, not a resolver, not a `switch`.
- The decorator chain should read as a chain. `dynamic-data-pipeline` and
  `hot-swap-source` both describe the swap as a wrapper; the registration should
  look like what the documents say.
- One dependency is cheap; a second place the strategy set is written down is
  not.
- This is a demo whose subject is DynamicData. Container cleverness that needs
  explaining on stage is a cost, and so is boilerplate that needs apologising
  for.
- **A failure mode that is invisible at runtime is the most expensive kind
  here**, because the stage is where it would first be seen.

## Considered options

| Option                                                      | Summary                                                                                                                                                                                                         | Why not                                                                                                                                                                                                                                                                                                                                                                                                                               |
| ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Keyed services (`AddKeyedSingleton`, `[FromKeyedServices]`) | Built into .NET 8 and later, so no dependency at all. Strategies register under keys; the decorator resolves by key.                                                                                            | The decorator has to know the keys, so the set of strategies is written down a second time — in the decorator, next to the registration that already lists them. Adding a vessel strategy then means editing both. That is the resolver this design rejected, wearing a different hat.                                                                                                                                                |
| One interface per strategy, injected explicitly             | `IAircraftTrackerSource` and `IVesselTrackerSource` already exist, so the decorator can take both by constructor and register as plain `ITrackerSource`. No dependency, and the compiler enforces completeness. | Adding a strategy edits the decorator's constructor, which is the one thing this design was shaped to avoid.                                                                                                                                                                                                                                                                                                                          |
| Scrutor's `Decorate<>`                                      | A library whose whole subject is decoration, and the obvious reach for this shape.                                                                                                                              | **It cannot do this.** `Decorate<>` wraps one registration at a time, so the N strategies this seam exists to select between become N decorators and the last one wins — silently, with nothing to read in the container and nothing to see until a swap does nothing on stage. The table in Context is the run. Taking it anyway would mean one `ITrackerSource` registration forever, which is the shape below without the package. |
| Hand-written decoration over `ServiceDescriptor`            | Capture the strategy descriptors, replace the `ITrackerSource` registration with a factory built over the captured ones. About thirty lines.                                                                    | Thirty lines of descriptor manipulation in a demo about reactive collections, and it is only needed when the strategies are registered under the decorated type — which the option below does not do.                                                                                                                                                                                                                                 |
| **A factory over the per-type seams**                       | **Chosen.** Strategies register under their own seams; one factory registration constructs the decorator over them and registers it as `ITrackerSource`.                                                        | —                                                                                                                                                                                                                                                                                                                                                                                                                                     |

## Decision

**Nothing but the decorator is registered as `ITrackerSource`.** Each strategy
registers under its own per-type seam — `IAircraftTrackerSource`,
`IVesselTrackerSource` — which already exist for the reason `aircraft-source`
B-037 gives, and one factory registration builds the selector over them:

```csharp
services.AddSingleton<IAircraftTrackerSource, AircraftTrackerSource>();

// The one registration of the seam, and the one place the strategy set is
// written down. A strategy registered as ITrackerSource here instead would be
// resolved in place of the selector.
services.AddSingleton<ITrackerSource>(static provider => new SwappingTrackerSource(
    provider.GetRequiredService<IAircraftTrackerSource>()));
```

The decorator takes the strategies as `params ITrackerSource[]`, so adding one
is a word on that registration line and no edit to the decorator, to a
resolver, or to any consumer. Consumers resolve `ITrackerSource` and get the
selector, exactly as ADR-0002 claimed.

The registration lives in a container builder block alongside the existing
`AkkaHostBuilder` and `UserInterfaceBuilder` extension blocks, per
`coding-conventions`, not piled into `MauiProgram`.

No package is added. `Scrutor` is not taken, and
[`Directory.Packages.props`](../../Directory.Packages.props) does not carry it.

## Consequences

- **The registration-order hazard is retired, and it was the live one.** The
  prior record's own Consequences called `Decorate<>`'s order sensitivity "a
  real trap, it fails silently"; `replay-source` § 4 row 12 ruled a whole class
  of registration out because of it, and two items carried it in their risk
  rows. A factory has no such ordering: the strategies it names are resolved
  when the seam is first resolved, whenever they were registered. Those rows
  are amended by the change that carries this record.
- **A new trap replaces it, and it is the narrower one**: registering a strategy
  as `ITrackerSource` rather than under its per-type seam makes the container
  hand that strategy to consumers instead of the selector. It is one line, in
  one place, beside a comment saying so — and unlike the ordering hazard, the
  B-052 composition test sees it, because that test asserts what the tracker's
  source actually is.
- **No dependency.** The thing the library would have done is a lambda, and the
  reader of the composition root does not have to know a package to read it.
- **The strategy set is written once**, on the factory line, which is the
  property ADR-0002 claimed and this record makes true.
- **The decorator stays substitutable.** Nothing about the pattern depends on
  this registration shape: a container with first-class decoration would
  express the same chain without touching the decorator, the strategies or
  anything downstream.
- **`README.md` § "Technology Decisions" gains no line**, where the prior record
  said it would.
