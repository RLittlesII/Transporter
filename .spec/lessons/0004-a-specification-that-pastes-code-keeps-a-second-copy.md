---
title: "Lesson 0004: A specification that pastes code keeps a second copy of it"
description: "Section 7 wrote out the declarations of seven types that now exist as files, so one signature change had to be made twice in one session and nothing would have reported it if the second edit had been forgotten."
type: lesson
---

# Lesson 0004: A specification that pastes code keeps a second copy of it

**Date:** 2026-10-04
**Kind:** process

## Symptom

§ 7 of the aircraft specification carried the full C# for `IOpenSkyApi`, its
envelope, the positional row, the throttle type and the HTTP transport. Those
files were written in the same session, from that section.

Changing the contract's return shape then took two edits of the same
signature — once in
[`IOpenSkyApi.cs`](../../src/Transponder/Integrations/OpenSky/Contracts/IOpenSkyApi.cs)
and once in § 7 — and it happened twice in a day: `Either` went in, then
[ADR-0008](../adr/0008-the-contract-is-the-boundary-and-may-throw.md) took it
out. The Mermaid diagrams held the type names a third time.

Nothing would have reported the second edit being skipped. Both copies are
syntactically fine because only one of them is compiled, and a specification
carrying a stale signature reads exactly like one carrying a current signature.
The section's own `Status`, § 9's rows, the build and the link check are all
unaffected by it.

## Root cause

A specification says what must be true. A pasted declaration says what the code
is — and once the code exists, saying what the code is belongs to the code.

The paste is not wrong when it is written. § 7 chose every one of those names,
and before the file existed the declaration was the only statement of the
design anywhere; there was nowhere else to put it. What is missing is a marker
for the moment it stops being that. The file appearing is the event, and it
arrives in the same change that wrote the paste — so the writer is looking
straight at it and has no reason to act, because the two agree that day.

The same failure in a smaller key had already happened twice on this branch and
been fixed without being named.
[ADR-0006](../adr/0006-an-analyzer-enforces-the-layer-boundaries.md) argued from
the build having no test target, which item `0017` falsified hours later; the
companion skill stated that the repository had no git remote, which it has.
Both are a document holding a fact that belongs to something else, and both
went false the day the something else changed.

## Spec delta

§ 7's "Interface changes" lost the declarations of the seven types that are
built, and gained a `| Type | File | Claims it makes visible |` table in their
place, each file linked rather than named so that a move is a broken link. Every
prose paragraph stayed: the naming argument, why four loose coordinates rather
than a box, why the row is a `class` and not a `record`. That reasoning is the
specification's own and lives nowhere else — it is not what the compiler
states.

The first draft of that edit reproduced
[lesson 0003](0003-a-dedupe-is-a-move-and-a-move-has-a-destination.md) exactly.
The deleted `OpenSkyHttpApi` block declared a constructor taking a token source
and a logger; the built file takes neither, because B-026 and B-027 are
`0004`'s. That is design the file does not hold and the table cannot carry, so
deleting the block deleted it — a destination that holds less than the source
is not a destination. § 7's prose now says the transport is unfinished and what
arrives with `0004`.

The one declaration still written out is `IAircraftTrackerSource`, under a
sentence saying `0005` has not built it yet. § 7's layout block lost
`Contracts/` and `Container/` for the same reason and now lists only folders
whose contents do not exist.

## Claim

- None. A process lesson with no product delta owes no claim, following
  [lesson 0001](0001-a-skill-holds-the-rule-not-the-facts.md).

## Skill

[`spec-and-traceability`](../../.skills/spec-and-traceability/SKILL.md) gained
§ "A declaration belongs to the file that compiles", immediately after
§ "One answer, one section", which already says that no section stores what
another derives. This is the same rule reaching one store further out. Its
core, quoted because a lesson keeps the text it was written against:

> A specification designs types before they exist, and writing the declaration
> out is how that design gets stated — there is nowhere else for it to live
> yet. That stops being true the moment the file exists. From then on the
> compiler is the authority on the signature, and the copy in the document is a
> second store with no build to keep it honest.
>
> - **Replace the declaration with a row**: the type, the file, and what the
>   declaration was there to make visible. The reasoning stays — why that name,
>   why that shape, which claims it answers — because that lives nowhere else
>   and is the specification's own.
> - **A diagram is not a declaration.** Naming a type to show a relationship is
>   not a second statement of its signature. Leave diagrams alone.
> - **A record is not a living document.** A decision record argues from the
>   declaration as it stood on its date, and an accepted one is immutable
>   regardless. Only the documents that claim to describe the present track the
>   code.
>
> The change that creates the file is where this happens, not a tidy-up
> afterwards.

Two paragraphs follow it in the skill and are not quoted here, because they
answer objections rather than state the rule: that the table is not the
hand-kept file index § "Never add" rules out, and that deleting a declaration
is a move owing lesson 0003's destination check.

The diagram carve-out is there because the obvious over-correction is to strip
the type names out of the Mermaid too, which would cost the section the only
picture of how the layers connect and buy nothing: a renamed type leaves a name
grep finds, not a signature that silently disagrees.

The record carve-out is what keeps this compatible with an immutable accepted
ADR. A record argues from what was true on its date; only a document claiming
to describe the present owes the code anything.

[`transponder-conventions`](../../.skills/transponder-conventions/SKILL.md)
§ `spec-and-traceability` gained the repository's shape of it — which section,
the table's columns, and the layout block shrinking as the table grows — and,
separately, lost the sentence claiming this repository has no git remote. The
second edit is the same defect as the first, found while fixing it.

[`deliver-change`](../../.skills/deliver-change/SKILL.md) § "Decision records"
gained the half of this that applies to records rather than specifications —
reason from what is durable, and follow a cite before you commit it. That is
ADR-0006's two defects stated as one rule.
