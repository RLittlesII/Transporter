---
title: "ADR-0003: Scrutor for decorator registration"
description: "Register the tracker-source swap decorator with Scrutor's Decorate<> rather than hand-rolling the resolution, so the decorator chain is declarative and adding a strategy touches no constructor."
type: adr
---

# ADR-0003: Scrutor for decorator registration

**Status:** proposed

## Context

[ADR-0002](0002-contract-client-strategy-tracker.md) puts the source swap in a
**decorator registered as `ITrackerSource`**: every strategy adheres to that
interface, the decorator does too, and a consumer resolving the seam gets the
decorator without asking anything. There is deliberately no strategy resolver.

That shape has one piece of plumbing to settle. A decorator registered as
`ITrackerSource` that needs the strategies it wraps cannot simply inject
`IEnumerable<ITrackerSource>` — it is itself registered as `ITrackerSource`, so
it would be handed itself. The container has to be told that one registration
wraps the others.

`Microsoft.Extensions.DependencyInjection` has no first-class decoration. The
three ways out are keyed services, naming each strategy by its own interface, or
a library that rewrites the registration.

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

## Considered options

| Option | Summary | Why not |
| ------ | ------- | ------- |
| Keyed services (`AddKeyedSingleton`, `[FromKeyedServices]`) | Built into .NET 8 and later, so no dependency at all. Strategies register under keys; the decorator resolves by key. | The decorator has to know the keys, so the set of strategies is written down a second time — in the decorator, next to the registration that already lists them. Adding a vessel strategy then means editing both. That is the resolver this design rejected, wearing a different hat. |
| One interface per strategy, injected explicitly | `IAirplaneTrackerSource` and `IVesselTrackerSource` already exist, so the decorator can take both by constructor and register as plain `ITrackerSource`. No dependency, no self-injection possible, and the compiler enforces completeness. | Adding a strategy edits the decorator's constructor, which is the one thing this design was shaped to avoid. It also makes the per-type interfaces load-bearing for plumbing when their actual job is to let each strategy own its own projection. |
| Hand-written decoration | Register the strategies, capture the descriptors, replace the `ITrackerSource` registration with a factory that builds the decorator over the captured ones. Exactly what a library does, in about thirty lines. | Thirty lines of `ServiceDescriptor` manipulation in a demo about reactive collections. It is the kind of code that invites a question from the audience that the talk has no time to answer, and it has to be right the first time because a container bug looks like a DynamicData bug. |
| **Scrutor** | **Chosen.** `services.Decorate<ITrackerSource, SwappingTrackerSource>()` rewrites the prior registrations so the decorator receives them. | — |

## Decision

Take `Scrutor` and register the swap decorator with `Decorate<>`. Strategies
register as `ITrackerSource`; the decorator is applied over them; consumers
resolve `ITrackerSource` and get the decorator.

The registration lives in a container builder block alongside the existing
`AkkaHostBuilder` and `UserInterfaceBuilder` extension blocks, per
`coding-conventions`, not piled into `MauiProgram`.

Adding a strategy is then one line in a registration, and nothing else in the
application changes — which is the property ADR-0002 claimed and this record
makes true.

`Scrutor` is added to `Directory.Packages.props` as a `PackageVersion` with the
reference in the project that needs it, in the same change, per
`coding-conventions` § "Dependencies". It is not yet there; the first item that
builds the decorator adds it.

## Consequences

- **A dependency where the framework nearly suffices.** Keyed services would
  avoid it. Accepted because the alternative writes the strategy set down twice,
  and two places that must agree is the failure this design keeps removing.
- **Scrutor's decoration is registration-order sensitive.** `Decorate<>` wraps
  what is already registered, so a strategy registered *after* the decorate call
  is not wrapped and will be resolved raw. That is a real trap, it fails
  silently, and the registration block should carry a comment saying so.
- **It is a third-party library in the composition root**, which is where a
  reader looks to understand how the app is wired. Its one use is visible and
  named, so a reader who does not know Scrutor can still see what the line does.
- **The decorator stays substitutable.** Nothing about the pattern depends on
  Scrutor: if it is dropped later, the hand-written option above replaces it
  without touching the decorator, the strategies or anything downstream. This
  record is reversible in a way the ADR-0002 shape is not.
- **One more package for the audience to notice.** `README.md` § "Technology
  Decisions" gains a line, since that list is what the talk points at.
