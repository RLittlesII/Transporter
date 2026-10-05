---
name: domain-model
description: Model a tracked item as a subclass of the abstract domain base so the reactive pipeline and the UI never learn which source is live. Use when adding or changing a domain type, a unit, or a grouping key.
---

# The domain model

The model is the one layer everything else leans on, and a live source swap is
what stresses it: the same grid, filters, sorts and groups must refill from a
different source, which only works if nothing downstream names a concrete item
type.

The shape is recorded in
[ADR-0005](../../.spec/adr/0005-an-abstract-base-carries-the-tracked-item.md),
and the seam it serves in
[ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md). This file
carries the traps around them, not a second copy of either.

## The shape

`TransportVehicle` is an abstract class, one level deep, with shared derivation
non-abstract on the base and the per-source answers `abstract`.
[ADR-0005](../../.spec/adr/0005-an-abstract-base-carries-the-tracked-item.md)
§ Decision states the seven rules that follow from that — the members, the
`protected` constructor, the one level, and the detail pane being the only place
a concrete type appears. Read it rather than a summary of it; what fields a
given source reports is the specification's, in `README.md` and the Feature's
`.spec/README.md`.

The tradeoff is deliberate and worth knowing before you fight it: a thin base
buys the swap and costs expressiveness. A column that genuinely needs
source-specific data comes from a source-supplied column description, **never
from a downcast** ([`maui-ui`](../maui-ui/SKILL.md) "The swap test").

Canonical units are canonical in the model (ADR-0005 item 7). Conversion happens
at the view or in an explicitly named method, never silently inside a mapper
([`mapping`](../mapping/SKILL.md)).

## Optional values

A sparse wire is the normal case. Use `Option<T>` at the boundary so "not
reported" is a value rather than a null to be remembered
([`language-ext-usage`](../language-ext-usage/SKILL.md)).

A missing position and a position of `0,0` are not the same thing. The Gulf of
Guinea is a real place.

## Never add

- A UI, actor, reactive-collection, HTTP or serialization type to the model. It
  is referenced by everything and references nothing but the framework and the
  functional-extensions library.
- Mutable collection state. Collections are the cache's job.
- A wire-shaped positional payload past the client boundary. That shape is a
  transport detail and dies at the converter.
- A stored staleness flag. Derive it on the base.
- A downcast, `is` check or `switch` on a subclass outside the detail pane. That
  is the swap breaking, one line at a time.
- A third level of inheritance.
- A subclass-specific member hoisted onto the base so a view can reach it.
