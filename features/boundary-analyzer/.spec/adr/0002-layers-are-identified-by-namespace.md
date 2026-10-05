---
title: "ADR-0002: A rule identifies a layer by namespace"
description: "The analyzer reads a type's layer from its containing namespace, which transponder-conventions already fixes to the folder it sits in, rather than from a marker attribute invented for the analyzer or from a project per layer."
type: adr
---

# ADR-0002: A rule identifies a layer by namespace

**Status:** proposed

## Context

Seven of the eighteen rules are about what may reference what — § 7's
`TRN0001` – `TRN0007`, and B-009 requires them to be evaluated over method
bodies. Every one of them is a sentence of the form "no *X* may name a *Y*",
so before any of them can be written the analyzer has to answer a question
neither [ADR-0006](../../../../.spec/adr/0006-an-analyzer-enforces-the-layer-boundaries.md)
nor § 3 settles: **given a type, which layer is it?**

The repository already has an answer for humans.
[`transponder-conventions`](../../../../.skills/transponder-conventions/SKILL.md)
§ "Project structure" fixes the folders — a provider's wire surface in
`Integrations/<Provider>/Contracts`, its implementation in `Http`, its
registration in `Container`, the domain model in `Model/`, the per-type
strategies in `Tracking/`, a feature's view models in
`Features/<Feature>/ViewModels` — and [`.editorconfig`](../../../../.editorconfig)
turns that into namespaces with `dotnet_style_namespace_match_folder`. The
question is whether the analyzer reads that, or something of its own.

This specification carried it as § 11 row 1, and it blocked `0022`: the answer
binds all seven reference rules, so writing them first would mean writing the
identification seven times against a guess.

## Decision drivers

- The rule has to classify a type the analyzer did not write and cannot annotate
  — one from a referenced assembly, or one a contributor just added.
- Seven rules share the answer, so it belongs in one place in the analyzer.
- No surface invented for the analyzer's benefit. `aircraft-source` B-037's own
  argument is that a seam is not widened to describe where its data came from,
  and the same reasoning applies to a type carrying a label for the benefit of
  the tool that inspects it.
- The repository is a talk's takeaway: the mechanism should be the one a reader
  would guess.

## Considered options

| Option | Summary | Why not |
| ------ | ------- | ------- |
| A marker attribute per layer | Each layer's types carry `[Layer(Layer.Contract)]`; the rule reads the attribute. | Unambiguous and it survives a reorganization, but it is a new public surface on every type the analyzer cares about, invented for the analyzer. It also fails open: a type that forgets the attribute is classified as nothing and every rule passes it, which is the "satisfied by absence" failure ADR-0006 exists to retire. |
| One project per layer | The compiler enforces the coarse cases with no analyzer at all; the rule reads the assembly. | It is a restructure of the whole repository for one Feature, and it breaks something deliberate: the integration lives inside `Transponder` so its contract, wire types and implementation can stay `internal` and be reached by tests through `InternalsVisibleTo`. Split into projects, they would have to be `public` to be composed — the visibility claim traded away for the enforcement of it. |
| **Namespace, from the folder convention (chosen)** | The rule reads the containing namespace of the declaring symbol and of the referenced symbol, against the layout `transponder-conventions` references/coding.md § "Project structure" already fixes. | — |

## Decision

**A rule identifies a layer by the containing namespace**, and the mapping from
namespace to layer lives in one place in the analyzer — `Layers.cs` — rather
than in each rule.

1. **It classifies every type, including ones we did not write.** A namespace is
   present on every symbol the semantic model hands back, whether it came from
   source, from a referenced project or from metadata.
2. **It reads the convention that already exists** rather than adding a second
   statement of the layout. The folders are fixed, `.editorconfig` requires the
   namespace to match the folder, and the Airframe set already reports when it
   does not.
3. **The classification has one store.** Seven rules ask "which layer is this"
   and get the answer from the same place, so a layer that moves is one edit.

A marker attribute is **not** ruled out for later: a type the convention cannot
classify — one that must live outside the layout for a reason — is the case that
would justify one, and `Layers.cs` is where it would be read. Nothing is built
for that case now (§ 5's "no rule no claim asks for").

## Consequences

- **A misfiled file silently changes which rules apply to it.** Move a view
  model into `Integrations/` and the reference rules stop asking it anything.
  This is the cost of reading the convention instead of an explicit label, and
  it is paid for with a test of its own rather than a different mechanism.
- **The analyzer now depends on the folder convention being real.** A rename of
  `Model/` or `Tracking/` is a change to `Layers.cs` in the same commit, the way
  a renamed section is a change to its cites.
- **Nothing is added to any type.** No attribute, no base class, no partial, and
  no new public surface on the domain model — which is what made option A
  unattractive given `aircraft-source` B-037.
- **The coarse cases are not compiler-enforced.** Option C would have made some
  of these references impossible rather than reported. They stay reported, at
  `error`, which ADR-0006 § Decision 2 already argued is the point: the
  diagnostic lands on the line that wrote the reference.
