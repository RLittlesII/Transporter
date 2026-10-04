---
title: "ADR-0005: An abstract base carries the tracked item"
description: "TransportVehicle is an abstract class, not an interface, so shared derivation lives once and the per-source parts are compiler-enforced; one level of derivation, and no downcast outside the detail pane."
type: adr
---

# ADR-0005: An abstract base carries the tracked item

**Status:** proposed

## Context

[ADR-0002](0002-contract-client-strategy-tracker.md) puts the strategy seam at
the domain boundary: every source projects its own records into
`TransportVehicle`, and the cache, the filters, the comparers, the aggregates
and the bindings downstream are written against that one type. It takes
`TransportVehicle` as given and records no decision about its shape.

The shape is a decision, and it is the one the live source swap rests on. A
source swap only proves anything if nothing downstream names a concrete item
type, so what `TransportVehicle` can and cannot carry decides whether the swap
is a registration change or a sweep through every view.

Two facts force it:

- **Some derivation is identical for every source.** Staleness from a last
  contact against an injected clock is the same question for any item that
  reports and then stops.
- **Some answers are per-source and must not be forgotten.** What a row is
  labelled with, and what a view groups by, differ by source and have no
  sensible default. A new source that silently inherits a wrong answer is worse
  than one that will not compile.

## Decision drivers

- A swap must not require editing a view.
- Shared derivation is written and tested once.
- A new source cannot forget to answer what only it can answer.
- No speculative extensibility: this is a small system.

## Considered options

| Option | Summary | Why not |
| --- | --- | --- |
| An interface with default implementations | `ITransportVehicle`, with shared derivation as default interface members. | A default is exactly the mechanism that lets a new source forget an answer: `Label` and the grouping key would silently resolve to something generic rather than failing to compile. It also offers no way to guarantee a key was set at construction. |
| A single concrete type with nullable per-source fields | One `TrackedItem` carrying every field either source might report. | Every consumer then reads fields that are null for half the sources, and the model grows a column per source forever. The compiler stops helping. |
| A record hierarchy with value equality | Make the domain type a record, as the snapshots are. | Value equality is what the *snapshot* needs, so a differ can answer "unchanged" without configuration. A domain item is keyed and mutable in its reported values; giving it value equality invites it to be used as a cache key's identity. |
| Two unrelated types and a common interface per consumer | No shared base at all; each consumer declares the subset it needs. | Multiplies the seam ADR-0002 deliberately kept to one, and the swap then has to bridge them. |
| **An abstract base class, one level deep** | **Chosen.** Shared derivation non-abstract on the base; the per-source answers abstract; key and last contact set through a protected constructor. | — |

## Decision

`TransportVehicle` is an **abstract class**, and the polymorphism is the point.

1. **Shared behavior lives once.** Staleness derivation against an injected
   clock is identical for any silent item, so it is a non-abstract member on the
   base, implemented once and tested once. It is **derived, never stored**: a
   persisted flag goes wrong the moment the clock moves.
2. **The per-source parts are `abstract`.** The label a row shows and the key a
   view groups by must be answered by each concrete type, and the compiler says
   so — which a default implementation would have hidden.
3. **Key and last contact are set on construction**, through a `protected`
   constructor, so no derived type can produce a keyless item. A key is stable
   for the life of the item, never null, and never reused.
4. **One level of derivation.** One abstract base, one concrete type per source,
   and that is the whole hierarchy.
5. **The base carries no `virtual` member nothing overrides**, and no member
   hoisted onto it so that a view can reach it. A field only one source reports
   is not shared.
6. **The detail pane is the only place a concrete type appears.** Everywhere
   else — cache, filters, comparers, aggregates, bindings — is written against
   the base, and a downcast outside that one surface is the swap breaking a line
   at a time.
7. **Canonical units are canonical in the model.** The model stores what the
   wire reported; display units are a view concern, converted in an explicitly
   named method, and a converted value never replaces the canonical one.

## Consequences

- **The swap becomes a registration change.** Nothing downstream names a
  concrete type, so a new source is a projection and a registration.
- **A thin base costs expressiveness.** A column that genuinely needs
  source-specific data cannot reach it from the base, and must come from a
  source-supplied column description instead of a cast. That is the trade
  bought deliberately.
- **A new source is a compile error until it is finished**, which is the
  intended failure mode.
- **Two hierarchies exist** — snapshots per provider, and this one. They are not
  mirrored: a snapshot is a record with value equality, this is a keyed abstract
  base, and ADR-0002 already rejected giving the snapshots a shared base of
  their own.
