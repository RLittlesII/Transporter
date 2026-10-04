---
name: domain-model
description: Model a tracked item as a subclass of the abstract domain base so the reactive pipeline and the UI never learn which source is live. Use when adding or changing a domain type, a unit, or a grouping key.
---

# The domain model

The model is the one layer everything else leans on, and a live source swap is
what stresses it: the same grid, filters, sorts and groups must refill from a
different source, which only works if nothing downstream names a concrete item
type.

**The shape is recorded in
[ADR-0005](../../.spec/adr/0005-an-abstract-base-carries-the-tracked-item.md)**;
the seam it serves is
[ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md). This file
is the working summary. What fields a given source reports is the
specification's to say — `README.md` and the Feature's `.spec/README.md` carry
the wire tables, and a skill that restated them would be a second place for them
to drift from.

## One abstract base

The base is an **abstract class**, not an interface, carrying what every tracked
item has:

| Member | Why it exists |
|---|---|
| Key | The cache key. Stable for the life of the item, never null, never reused. |
| Position | Optional — a source reports items it has no fix for. |
| Last contact | The instant the source last heard from the item. **Staleness is derived from this**, never stored as a flag. |
| Label | What the identity column shows: whichever identifying field is present, or the key when nothing better exists. |
| Grouping key | What `Group` groups by. |

- **Shared derivation is non-abstract on the base** — written once, tested once.
  Staleness against an injected clock is identical for any item that goes
  silent.
- **The per-source answers are `abstract`.** A new source cannot forget to
  answer them, and the compiler says so.
- **Key and last contact are set on construction** through a `protected`
  constructor, so no subclass can produce a keyless item.
- **One level of derivation**: one abstract base, one concrete type per source.
- A derived type adds its own detail, and **the detail pane is the only place a
  derived type appears**.

The tradeoff is deliberate: a thin base buys the swap and costs expressiveness.
A column that genuinely needs source-specific data comes from a source-supplied
column description, never from a downcast
([`maui-ui`](../maui-ui/SKILL.md) "The swap test").

## Units

- **Canonical units are canonical in the model** — the model stores what the
  wire reported.
- Display units and local times are a *view* concern. Convert at the view or in
  an explicit named conversion, never silently inside a mapper
  ([`mapping`](../mapping/SKILL.md)).
- A converted value never replaces the canonical one on the model.

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
