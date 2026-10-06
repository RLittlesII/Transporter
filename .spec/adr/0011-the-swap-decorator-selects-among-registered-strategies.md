---
title: "ADR-0011: The swap decorator selects among registered strategies"
description: "Register every strategy under one strategy seam and the decorator as the consumer seam, so adding a source is one registration line and a swap at runtime is a Switch over the strategies the container already handed the decorator."
type: adr
---

# ADR-0011: The swap decorator selects among registered strategies

**Status:** proposed

Supersedes [ADR-0003](0003-scrutor-for-decorator-registration.md), whose
premise — that Scrutor's `Decorate<>` hands one decorator every prior
registration — is false.

## Context

[ADR-0002](0002-contract-client-strategy-tracker.md) puts the source swap in a
**decorator registered as `ITrackerSource`**: every strategy adheres to that
interface, the decorator does too, and a consumer resolving the seam gets the
decorator without asking anything. There is deliberately no strategy resolver.

The behaviour that has to come out of this, in front of people: the presenter
taps a control, the live source changes from aircraft to vessels **with no
restart**, and nothing downstream is rebuilt or re-bound.

That leaves one piece of plumbing. A decorator registered as `ITrackerSource`
cannot inject `IEnumerable<ITrackerSource>` to find the strategies — it is
itself registered as `ITrackerSource`, so the container would hand it itself.
`Microsoft.Extensions.DependencyInjection` has no first-class decoration, so
something has to say which registration is the selector and which are the
strategies.

ADR-0003 answered that with Scrutor and was wrong about what Scrutor does. Run
against 7.0.0, with two strategies registered as `ITrackerSource` and
`Decorate<ITrackerSource, T>()` applied:

| Decorator constructor            | Result                                                                     |
| -------------------------------- | -------------------------------------------------------------------------- |
| `(ITrackerSource inner)`         | **Two** decorators, one per registration; the seam resolves the last one   |
| `(IEnumerable<ITrackerSource> )` | `InvalidOperationException: A suitable constructor … could not be located` |

`Decorate<>` is a one-to-one wrapper, not an N-way selector. The probe was
deleted with the item that wrote it; what it established is this record's, and
[lesson 0016](../lessons/0016-an-untested-assumption-is-not-a-decision.md) is
how the assumption came to be written down as settled.

## Decision drivers

- **A swap is a runtime event, not a registration.** Whatever is chosen has to
  put every strategy in reach of one object while the application is running.
- Adding a strategy should touch the registration and nothing else — not a
  constructor, not a resolver, not a `switch`, and not a list anyone maintains
  by hand.
- The decorator chain should read as a chain. `dynamic-data-pipeline` and
  `hot-swap-source` both describe the swap as a wrapper; the registration should
  look like what the documents say.
- One dependency is cheap; a second place the strategy set is written down is
  not.
- This is a demo whose subject is DynamicData. Container cleverness that needs
  explaining on stage is a cost, and so is boilerplate that needs apologising
  for.
- **A failure mode invisible at runtime is the most expensive kind here**,
  because the stage is where it would first be seen.

## Considered options

| Option                                                      | Summary                                                                                                                                                                                     | Why not                                                                                                                                                                                                  |
| ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Keyed services (`AddKeyedSingleton`, `[FromKeyedServices]`) | Built into .NET 8 and later, so no dependency at all. Strategies register under keys; the decorator resolves by key.                                                                        | The decorator has to know the keys, so the strategy set is written down a second time — in the decorator, beside the registration that already lists them. Adding vessels means editing both.            |
| One interface per strategy, injected explicitly             | `IAircraftTrackerSource` and `IVesselTrackerSource` already exist, so the decorator takes both by constructor.                                                                              | Adding a strategy edits the decorator's constructor, which is the one thing this design was shaped to avoid.                                                                                             |
| A factory listing the strategies                            | `AddSingleton<ITrackerSource>(p => new SwappingTrackerSource(p.GetRequiredService<IAircraftTrackerSource>(), …))`. No package, no descriptor manipulation.                                  | The argument list **is** the second place the strategy set is written down, and it is maintained by hand. It is this record's first draft, and review rejected it on exactly that reading.               |
| Scrutor's `Decorate<>`                                      | A library whose whole subject is decoration, and the obvious reach for this shape.                                                                                                          | **It cannot do this**, per the table in Context: one decorator per registration, the last one resolved, silently. ADR-0003 took it on the assumption that it could.                                      |
| Hand-written decoration over `ServiceDescriptor`            | Capture the strategy descriptors and replace the `ITrackerSource` registration with a factory over the captured ones.                                                                       | Thirty lines of descriptor manipulation in a demo about reactive collections, and it is only needed because the strategies are registered under the decorated type — which the option below does not do. |
| **Two seams: a strategy seam and a consumer seam**          | **Chosen.** Strategies register as `ITrackerSourceStrategy`; the decorator registers as `ITrackerSource` and takes `IEnumerable<ITrackerSourceStrategy>`. Different service type, no cycle. | —                                                                                                                                                                                                        |

## Decision

**The thing a consumer resolves and the thing a strategy registers as are two
different interfaces.** `ITrackerSource` is what the tracker and every other
consumer depend on, and only the decorator registers as it.
`ITrackerSourceStrategy` is what a source registers as, and the container's
own `IEnumerable<T>` is what hands the whole set to the decorator — so there is
no cycle, no key table, no descriptor rewriting and no list anyone maintains.

```csharp
// Tracking/ITrackerSourceStrategy.cs — a source, as opposed to the seam
// consumers depend on. Empty: it adds nothing to ITrackerSource and exists so
// the container can enumerate strategies without enumerating the decorator.
internal interface ITrackerSourceStrategy : ITrackerSource;
```

```csharp
// One line per strategy, and nothing else changes when a strategy is added.
services.AddSingleton<IAircraftTrackerSource, AircraftTrackerSource>();
services.AddSingleton<ITrackerSourceStrategy>(static p => p.GetRequiredService<IAircraftTrackerSource>());

// … and when vessels land, beside it:
// services.AddSingleton<IVesselTrackerSource, VesselTrackerSource>();
// services.AddSingleton<ITrackerSourceStrategy>(static p => p.GetRequiredService<IVesselTrackerSource>());

// The selector, and the only registration of the seam consumers resolve.
services.AddSingleton<ITrackerSource, SwappingTrackerSource>();
```

The per-type seams stay what `aircraft-source` B-037 made them — one per
strategy, empty, so a widening is visible — and the alias beside each is what
puts that strategy in the enumerable.

## How a swap happens at runtime

The registration above is what makes the swap possible; this is the swap. No
part of it is a restart, a re-registration, or a rebuild.

```csharp
internal sealed class SwappingTrackerSource : ITrackerSource
{
    public SwappingTrackerSource(IEnumerable<ITrackerSourceStrategy> strategies)
    {
        _strategies = strategies.ToArray();
        _selected = new BehaviorSubject<ITrackerSource>(_strategies[0]);
    }

    public IObservable<IChangeSet<TransportVehicle, string>> Connect() =>
        _selected.Select(static source => source.Connect()).Switch();

    // Told, never asked: no consumer calls this to find out which source is live.
    public void Select<TStrategy>() where TStrategy : ITrackerSource =>
        _selected.OnNext(_strategies.OfType<TStrategy>().Single());
}
```

1. The presenter taps the control. `fleet-dashboard` B-016 makes that a `Tell`
   to an actor, with a busy indicator covering it — never an `Ask`, and never a
   value published from the view model.
2. The actor calls `Select<IVesselTrackerSource>()`. Which strategy a tap means
   is named by **the per-type seam**, so the choice is a type the screen already
   distinguishes (`fleet-dashboard` B-013) and no member is added to the seam to
   carry a name or a kind — which B-037 forbids.
3. `_selected` ticks, and `Switch` unsubscribes from the outgoing strategy's
   inner sequence and subscribes to the incoming one. The tracker's pipeline,
   its filters, its comparers, its groups and its bindings are all downstream of
   `Connect()` and are not touched: that is `aircraft-source` B-039 and B-042.
4. The outgoing strategy's poller stops because the subscription that owned it
   is gone — `aircraft-source` `0006` holds the call that a strategy's
   `Connect()` owns its poll, which is what makes B-040 true by construction
   rather than by a disposal anyone has to remember.
5. Tapping back selects the first strategy again, and its poll starts again.
   Nothing was disposed that cannot be re-subscribed, which is what
   `hot-swap-source` means by a swap being flat across repetitions.

What `0006` builds is this class, its registration and the actor that calls
`Select`. The control and the indicator are `fleet-dashboard` `0039`'s.

## Consequences

- **The strategy set is written once**, as registrations, and the container
  assembles it. Adding a source is one line beside its own, and the decorator,
  the actor and every consumer are untouched — the property ADR-0002 claimed and
  ADR-0003 did not deliver.
- **A second interface exists to carry no members.** `ITrackerSourceStrategy`
  earns itself by what it keeps out of the enumerable: without it the decorator
  is in its own strategy list. It is the same trick, and the same justification,
  as the empty per-type seams B-037 already requires.
- **The registration-order hazard is retired, and it was the live one.** ADR-0003
  called `Decorate<>`'s order sensitivity "a real trap, it fails silently";
  `replay-source` § 4 row 12 ruled a class of registration out because of it and
  two items carried it in their risk rows. Nothing here is order-sensitive: the
  enumerable is resolved when the seam is first resolved.
- **A narrower trap replaces it**: a strategy registered as `ITrackerSource`
  rather than as `ITrackerSourceStrategy` is handed to consumers in place of the
  selector, and the swap then does nothing. It is one line, beside a comment
  saying so, and `aircraft-source` B-052's composition test asserts what the
  tracker's source actually is — so it fails a test rather than a demo.
- **No dependency.** `Scrutor` is not taken and
  [`Directory.Packages.props`](../../Directory.Packages.props) does not carry
  it, so `README.md` § "Technology Decisions" gains no line.
- **The decorator stays substitutable.** Nothing downstream depends on this
  registration shape; a container with first-class decoration would express the
  same chain without touching the decorator, the strategies or anything after
  them.
- **`Select<TStrategy>()` is a method on the decorator, not an interface
  consumers resolve.** Only the swap actor calls it, which keeps B-038's "no
  strategy-resolver type that callers ask" true: the decorator is told what is
  live, and nobody asks it.
