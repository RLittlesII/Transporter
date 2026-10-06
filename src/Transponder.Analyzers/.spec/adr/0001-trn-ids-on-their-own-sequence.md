---
title: "ADR-0001: Diagnostics are TRN ids on their own sequence"
description: "The analyzer's diagnostics carry the TRN prefix and are numbered from TRN0001 in a sequence of their own, because a claim id is per-Feature and encoding one into a diagnostic id would make two Features' claims share a diagnostic."
type: adr
---

# ADR-0001: Diagnostics are TRN ids on their own sequence

**Status:** proposed

## Context

[ADR-0006](../../../../.spec/adr/0006-an-analyzer-enforces-the-layer-boundaries.md)
§ 4 requires that this analyzer's diagnostics "carry their own prefix so they
are never confused with the Airframe set", and that each one names the claim it
enforces. It fixes neither the prefix nor how the diagnostics are numbered.

The prefix is settled: **TRN**, chosen by the person on 2026-10-04. `RSA` is
Airframe's, and `CS`, `CA` and `IDE` are the compiler's and the SDK's.

The numbering is not a detail, because of what a claim id is here.
[`spec-and-traceability`](../../../../.skills/spec-and-traceability/SKILL.md)
§ "Claims and traceability" scopes claim ids to one specification: every
specification numbers from `B-001`, so
[`aircraft-source`](../../../aircraft-source/.spec/README.md) `B-004` and
[`replay-source`](../../../replay-source/.spec/README.md) `B-004` are different
claims and neither is renumbered for the other. What identifies a claim is the
pair — the specification and the id.

A diagnostic id, by contrast, is global. It appears in a build message, in an
`.editorconfig` severity line, and in a `#pragma` written years later by
someone who has read neither specification. It has no second half to resolve
against.

The same skill states the general form of this under § "Numbers from
independent sequences collide": several schemes number from the start
independently, so a bare number is ambiguous unless its prefix identifies it.

## Decision drivers

- A diagnostic id is read where no specification is in scope, so it must mean
  one thing on sight.
- `replay-source` has claims of its own and no § 9 rows yet; the analyzer is
  the stated mechanism for a reference claim, so a second Feature assigning
  work to it is expected, not hypothetical.
- Nothing may require renumbering a claim id, which is permanent.

## Considered options

| Option                                                                                            | Summary                                                                                                                                                           | Why not                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| ------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| The diagnostic id carries the claim number — `TRN0004` enforces `B-004`                           | One number to read, and the mapping needs no table: the id _is_ the claim reference.                                                                              | The claim number is only unique inside one specification. `aircraft-source` `B-004` and `replay-source` `B-004` would both want `TRN0004`, and the second one to arrive has three bad choices: share the id and merge two unrelated rules under one severity switch, invent a suffix scheme (`TRN0004b`), or renumber a permanent claim id. It is also wrong the first time it is read: `TRN0004` names no specification, so a reader cannot tell which `B-004` it means. |
| A diagnostic id per _rule family_ — one id for all reference rules, one for all declaration rules | Three or four ids total, each severity-switchable as a group.                                                                                                     | ADR-0006 § 4 requires one diagnostic per claim so a build message names the agreement that broke. A family id reports "a boundary rule failed" and sends the reader to a table to find out which. It also makes the `.editorconfig` escape hatch coarse: suppressing one over-broad rule suppresses five that were right.                                                                                                                                                 |
| **TRN on its own sequence, mapped (chosen)**                                                      | `TRN0001` – `TRN0018`, numbered from `0001` in a sequence belonging to the analyzer, with § 7's table mapping each id to the specification and claim it enforces. | —                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |

## Decision

**Diagnostics are `TRN` ids numbered from `TRN0001` in their own sequence, and
the specification-and-claim each one enforces is read from § 7's mapping
table.** A diagnostic id encodes nothing: not the claim number, not the
Feature, not the rule family.

1. **The id stays unambiguous as the analyzer grows.** A claim arriving from a
   second specification takes the next free `TRN` number, and no existing id,
   claim id or severity line moves.
2. **The mapping has one store.** § 7's table is the only place the claim a
   diagnostic enforces is written. The diagnostic's own description names that
   claim in words for the person reading the build output
   ([`spec-and-traceability`](../../../../.skills/spec-and-traceability/SKILL.md)
   § "One answer, one section" — the description is a message, not a second
   index).
3. **Numbers are permanent, like claim ids.** A withdrawn rule's id is retired,
   not reused, so an `.editorconfig` line or a `#pragma` written against it
   never silently starts suppressing something else.

## Consequences

- **A reader needs the table.** `TRN0007` does not say what it enforces without
  § 7, where `TRN0004`-for-`B-004` would have. That is the cost of the id
  meaning one thing; the build message carries the claim in its description so
  the table is needed for an audit, not for a fix.
- **The table is a cite that has to stay true.** Eighteen rows pointing at
  another specification's claim ids is eighteen cites, and nothing checks them
  — the hazard
  [`spec-and-traceability`](../../../../.skills/spec-and-traceability/SKILL.md)
  § "A cite names something that exists" names. § 3's `B-019` is the claim that
  holds the table to one store and forbids restating the foreign claim's text
  beside it, which is the form that drifts.
- **Two sequences now number from `0001` in this Feature** — `TRN` ids and this
  specification's own `B-` claims — on top of the item ids and the ADR numbers
  the repository already carries. § 3 `B-002` requires the prefix on every
  diagnostic for exactly this reason, and the repository's rule to write the
  scheme with the number applies to prose about both.
