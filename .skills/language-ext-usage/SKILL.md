---
name: language-ext-usage
description: Use LanguageExt Option and Either at wire boundaries where data is sparse, and stop at the binding, message and cache-key edges. Use when modelling an optional value or an operation that can fail.
---

# LanguageExt

This file covers **how far functional style goes here, and where it stops**. For
the library's own API, see
[louthy/language-ext](https://github.com/louthy/language-ext).

It earns its place at a boundary where the wire is sparse: making "not reported"
a value rather than a null to be remembered is most of the correctness work in
consuming a feed that reports partial records.

## Where it applies

- **Optional wire fields → `Option<T>`**, at the mapping boundary
  ([`mapping`](../mapping/SKILL.md)).
- **A missing value is not a default.** A null measurement is not zero; a null
  coordinate pair is not the origin. Mapping a null to a default is data loss
  that looks like data.
- **Operations that can fail → `Either`**, where the failure is expected and the
  caller must handle it: one malformed record in a batch, a projection that
  cannot be built, a poll the application has already decided to treat as a
  miss. One bad record should not take down the whole fetch.
- **Not at an I/O boundary.** A provider contract returns its payload and
  throws; the track starts on the application side of that call. A wrapper on
  the contract's own return type adds a second failure channel beside the
  exceptions the transport throws anyway
  ([`api-contract`](../api-contract/SKILL.md),
  [ADR-0008](../../.spec/adr/0008-the-contract-is-the-boundary-and-may-throw.md)).
- Unexpected failures stay exceptions. `Either` is for outcomes, not for
  replacing the exception system.

## Where it stops

A functional-style tour is not the point, and the boundaries are deliberate:

- **Bindings take plain values.** A binding cannot bind `Option<T>`. Projection
  to a display value — including the empty case — happens in the view model
  ([`mvvm`](../mvvm/SKILL.md)).
- **Actor messages stay plain and immutable**
  ([`akka-actor`](../akka-actor/SKILL.md)). A message is a data transfer object
  between actors, not a place for a monad transformer.
- **Cache keys are plain.** A key is never optional — an item without a key is
  not an item.
- No `LanguageExt` type in a public signature unless it is carrying its weight.
  Clarity beats purity.

## Keep it readable

- Prefer `Match`, `IfNone`, `Map` and `Bind` over unwrapping to a nullable and
  carrying on imperatively. Unwrapping immediately defeats the point of having
  wrapped.
- One idiom per situation, used consistently, beats three clever ones.
- If a reader would need to look up an operator, pick the plainer form.

## Never add

- A null-to-default mapping that invents a value the source never sent.
- `Option<T>` in an actor message, a cache key, or a binding target.
- `.Value` on an `Option` without handling the empty case.
- An `Either` standing in for a programming error that should throw.
