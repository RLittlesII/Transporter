---
name: language-ext-usage
description: Use LanguageExt Option and Either at Transponder's wire boundaries, where both providers report sparse data, and stop at the MAUI binding and Akka message edges. Use when modelling an optional value or an operation that can fail.
---

# LanguageExt in Transponder

Extends the global `language-ext` skill, which owns the library's API.
`LanguageExt.Core` is a decided technology ([`README.md`](../../README.md) §
"Technology Decisions") and is referenced by `src/Transponder`.

The reason it earns its place here is specific: **both wires are full of holes.**
OpenSky reports aircraft it has no position fix for, no altitude for, no
velocity for — nine of its eighteen fields are nullable. Making "not
reported" a value rather than a null to be remembered is most of the
correctness work in this demo.

## Where it applies

- **Optional wire fields → `Option<T>`**, at the mapping boundary
  ([`mapping`](../mapping/SKILL.md)). `Option<Position>`,
  `Option<double>` for altitudes and velocity, `Option<string>` for callsign
  and squawk.
- **A missing value is not a default.** A null altitude is not sea level; a
  null position is not `0,0` — the Gulf of Guinea is a real place. Mapping a
  null to a default is data loss that looks like data.
- **Operations that can fail → `Either`**, where the failure is expected and
  the caller must handle it: a token refresh that was refused, a `429`, a
  malformed row in a snapshot. One bad row should not take down a poll of four
  hundred aircraft.
- Unexpected failures stay exceptions. `Either` is for outcomes, not for
  replacing the exception system.

## Where it stops

This is a demo for line-of-business .NET developers, and a functional-style
tour is not the lesson. The boundaries:

- **MAUI bindings take plain values.** A binding cannot bind `Option<double>`.
  Projection to a display value — including the empty case — happens in the
  view model ([`build-maui-ui`](../build-maui-ui/SKILL.md)).
- **Akka messages stay plain and immutable**
  ([`akka-actor`](../akka-actor/SKILL.md)). A message is a DTO between actors,
  not a place for a monad transformer.
- **DynamicData keys are plain.** A cache key is never optional — an item
  without a key is not an item.
- No `LanguageExt` type in a public signature the audience will read on a
  slide unless it is carrying its weight. Clarity beats purity here, by
  design.

## Keep it readable

- Prefer `Match`, `IfNone`, `Map` and `Bind` over unwrapping to a nullable and
  carrying on imperatively. Unwrapping immediately defeats the point of having
  wrapped.
- One idiom per situation, used consistently, beats three clever ones.
- If a reader of the talk's source would need to look up an operator, pick the
  plainer form.

## Never add

- A null-to-default mapping that invents a value the source never sent.
- `Option<T>` in an Akka message, a DynamicData key, or a binding target.
- `.Value` on an `Option` without handling the empty case.
- An `Either` standing in for a programming error that should throw.
