---
name: mapping
description: Map wire payloads to the domain with Mapperly at one boundary, keeping unit conversions and derived values in explicit named methods. Use when adding a mapper, a wire field, or a conversion.
---

# Mapping wire to domain

This file covers **where mapping is allowed to happen**. For Mapperly's own API
and attributes, see [riok/mapperly](https://github.com/riok/mapperly).

## One mapper per boundary

- A `[Mapper]` partial class sits in each per-type strategy, which is **the only
  place a domain object is built**
  ([`api-contract`](../api-contract/SKILL.md)). That is it.
- **No domain-to-domain mappers.** If two domain types need converting between
  each other, one of them is wrong — fix the model instead
  ([`domain-model`](../domain-model/SKILL.md)).
- **No view-model mappers.** A view model projects from the bound collection; it
  does not receive a mapped copy ([`mvvm`](../mvvm/SKILL.md)).
- Generated mapper source is never hand-edited. If the output is wrong, the
  input shape or an attribute is wrong.

## A positional wire format stops before the mapper

**Mapperly maps names.** It has nothing to say about which index of an array
means which field, so a positional payload never reaches it: the client reads
the payload into a named record by index, by hand, as its first act, and the
mapper runs from that record.

Two mappings, two owners, two mechanisms — the arrangement
[ADR-0002](../../.spec/adr/0002-contract-client-strategy-tracker.md) records:

| Mapping | Owner | Mechanism |
|---|---|---|
| wire payload → snapshot | the client, as its first act | hand-written, by index |
| snapshot → domain | the per-type strategy | Mapperly |

The cache in between stores snapshots and maps nothing.

## Unmapped members are errors

A new wire field must be **deliberately mapped or deliberately ignored**.
Configure Mapperly's unmapped-member diagnostics as errors rather than
warnings, so adding a field on one side and forgetting the other fails the build
instead of silently dropping data. A field the domain genuinely does not want is
explicitly ignored, which records the decision.

## Conversions are explicit, never implicit

- **Canonical units stay canonical through the mapper.** Display units are a
  view concern ([`domain-model`](../domain-model/SKILL.md)).
- A conversion gets a named method a human can read and a test can call, invoked
  from the view layer or from an explicitly declared user-implemented mapping.
  Never buried in a generated member mapping where nobody will find it.
- **Derived values are not mappings.** Staleness derives from last contact
  against an injected clock; a display label derives from whichever identifying
  field is present. Those are domain or view behavior with their own tests, not
  mapper configuration.
- Where the wire pads or decorates a value, trim it on the way in and **say so
  at the mapping**, so the next reader knows the padding was not lost by
  accident.

## Optional values

The mapper's job is turning a nullable wire field into the model's `Option<T>`
deliberately — see [`language-ext-usage`](../language-ext-usage/SKILL.md). A
nullable that maps to a default (`0`, `false`, a minimum date) is data loss
dressed up as a value.

## Never add

- A hand edit to generated mapper source.
- A domain-to-domain or view-model mapper.
- A mapper reading a positional payload.
- A unit conversion or a derived value inside a generated mapping.
- A null-to-default mapping that invents data the source never sent.
