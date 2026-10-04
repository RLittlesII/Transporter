---
name: mapping
description: Map wire payloads to the Transponder domain with Mapperly at the boundary, keeping unit conversions and derived values in explicit named methods. Use when adding a mapper, a wire field, or a conversion.
---

# Mapping wire to domain

This file covers **where mapping is allowed to happen in this repository**. For
Mapperly's own API and attributes, see
[riok/mapperly](https://github.com/riok/mapperly). `Riok.Mapperly` is a decided
technology ([`README.md`](../../README.md) § "Technology Decisions") and is
already referenced in
[`Directory.Packages.props`](../../Directory.Packages.props).

## One mapper per boundary

- A `[Mapper]` partial class sits at each wire boundary: OpenSky payload →
  domain, AISStream payload → domain. That is it.
- **No domain-to-domain mappers.** If two domain types need converting
  between each other, one of them is wrong — fix the model instead
  ([`transponder-domain-model`](../transponder-domain-model/SKILL.md)).
- **No view-model mappers.** View models project from the bound collection;
  they do not receive a mapped copy.
- Generated mapper source is never hand-edited (`AGENTS.md`). If the output is
  wrong, the input shape or an attribute is wrong.

## The positional array stops here

OpenSky's `states` entries are positional arrays, so the custom
`JsonConverter` ([`api-contract`](../api-contract/SKILL.md)) produces a named
wire DTO and **the mapper runs from the DTO, not from the array**. Mapperly
maps names; it has nothing to say about index 7 meaning barometric altitude.

Two steps, each testable: converter (array → named DTO), mapper (DTO →
domain).

## Unmapped members are errors

A new wire field must be **deliberately mapped or deliberately ignored**.
Configure Mapperly's unmapped-member diagnostics as errors rather than
warnings, so adding a field to the DTO and forgetting the domain side fails
the build instead of silently dropping data. A field we genuinely do not want
(OpenSky's `sensors`) is explicitly ignored, which records the decision.

## Conversions are explicit, never implicit

- **Units stay SI through the mapper.** Metres in, metres stored. Feet and
  knots are a display concern.
- A conversion gets a named method a human can read and a test can call —
  `MetresToFeet`, `MetresPerSecondToKnots` — invoked from the view layer or
  from an explicitly declared user-implemented mapping. Never buried in a
  generated member mapping where nobody will find it.
- **Derived values are not mappings.** Staleness derives from last contact
  against an injected clock; display labels derive from callsign or vessel
  name. Those are domain or view behavior with their own tests, not mapper
  configuration.
- Trim OpenSky's 8-character-padded callsign on the way in, and say so at the
  mapping so the next reader knows the padding was not lost by accident.

## Optional values

The wires are full of nulls. The mapper's job is turning a nullable wire
field into the model's `Option<T>` deliberately — see
[`language-ext-usage`](../language-ext-usage/SKILL.md). A nullable that maps
to a default (`0`, `false`, `DateTime.MinValue`) is data loss dressed up as a
value: a missing altitude is not sea level.

## Never add

- A hand edit to generated mapper source.
- A domain-to-domain or view-model mapper.
- A mapper reading a positional array.
- A unit conversion or a derived value inside a generated mapping.
- A null-to-default mapping that invents data the source never sent.
