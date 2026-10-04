---
name: spec-and-traceability
description: The specification model — a specification starts before its work items, one record holds content and another holds delivery state, and every claim traces through a scenario to a test. Use when authoring, amending, or reviewing a specification, a claim, or a work item.
---

# Specification and traceability

**Project rules.** A project may extend this skill with a companion skill that
names this one; its agent instructions (`AGENTS.md`) list it. Read both. The
companion holds the paths, the section list, the claim-id and item-id schemes,
the templates, and the role that owns each section, and wins where they differ.

## Where a specification starts

**A Feature starts with its specification, not with a work item.** The
specification is the design authority, so it cannot be made to wait on the
tracker that delivers it.

- **The trigger is a decided need** — the project-wide specification plus a
  conversation that settled something. Not an item; there is no item yet.
- **The agreement sections come first**: the business goal, the user needs, the
  claims, the constraints, what is out of scope. That is the agreement.
- **The design sections follow**: how concerns are separated, and the technical
  design.
- **Items are cut from the claims afterwards**, each citing the claims it
  delivers. A feature item cannot exist before its claims do, because it
  references them.

So: **a feature item names its specification; a specification never names an
item.** One direction, nothing to keep in sync. The specification lists the item
ids cut from it, and "none yet" is the honest state while the agreement is still
being reached.

**Features only.** A **bug, spike or chore starts with an item** — its trigger
is an observation, not an agreement — and names no specification. A bug that
reveals a specification gap then produces a delta: a lesson, and the claim that
proves the delta held.

Put the other way round: the specification is where *agreement* starts; the item
is where *delivery* starts. They are different questions, answered in that
order.

## Two records, one authority each

| Record | Authority over | Never holds | Exists first? |
|---|---|---|---|
| The specification | **content** — requirements, constraints, design, testing strategy, traceability | the live status of the work | **yes, for a Feature** |
| The work item | **delivery state** — status, priority, value, risk, rank, dependencies | requirements, design, test plans | yes, for a bug, spike or chore |

- A specification carries its own **document** maturity and nothing about
  delivery. The work's status and scoring live in the item, and only there. Two
  copies of a status is two answers to one question.
- The specification lists item **ids**, not restated titles.
- **One claim lives in one place**: the acceptance-criteria section. An item
  *references* claim ids; it never restates the claim text. Those ids are always
  already there — the specification was written first.
- Derived fields are recomputed, never hand-edited. The inverse of a dependency
  is not a claim of its own.
- **A closed item stays**, with the date it closed. The record is permanent
  history, like the specification itself.
- **A risk rationale names the hazard, not the shape of the work.** The number is
  read later to decide how hard to test something, so "crosses two boundaries"
  or "no existing coverage" gives a test writer nothing to aim at. Name the
  specific wrong answer and the claim it breaks, and the rationale becomes an
  instruction instead of a restatement of the score.

## Claims and traceability

- Claims are numbered rows in the acceptance-criteria section. Each scenario
  carries the matching claim tag, and **the traceability matrix is the gate**:
  every claim appears there exactly once, and a missing row blocks ship.
- **A claim id is never renumbered and never reused.** A withdrawn claim is
  marked withdrawn, not deleted — the matrix and the scenarios are anchored to
  it.
- **Know what identifies a claim.** Where ids are scoped to one specification,
  two specifications may both number from the start and neither is renumbered
  for the other; what identifies the claim is then the pair of specification and
  id, and an id is only ever read against a named section. Per-scope numbering
  is what lets two Features be specified at once without colliding.
- Where scenarios are documentation rather than executable, **a scenario alone
  never satisfies a claim** — the matrix row does, and it points at a test
  ([`test-from-scenarios`](../test-from-scenarios/SKILL.md)). An unbuilt claim
  is then marked in the two places that already exist: its status column and its
  matrix row. Nothing else, and nothing implying the scenarios execute.

## Numbers from independent sequences collide

Several schemes number from the start independently, and a per-scope scheme
starts again in every scope, so a bare number is ambiguous on sight unless its
prefix identifies it.

**In prose, write the scheme with the number.** A number may appear bare only
where its field says what it is — a parent id in an item, or a table column that
names the scheme.

This is a writing rule, not a renumbering one. Per-scheme numbering is
deliberate: it is what lets a scope carry its own records without reserving
numbers from a shared pool. The collision is only ever in how a number is
*referred to*.

## Section ownership

**Every section has exactly one owning role.** The mapping is written in exactly
one place — the companion — and each role file links there rather than
restating its own sections.

- **A role writes only its own sections.** Filling in a missing claim because it
  was in the way is a boundary violation, not a favour; escalate to the role
  that owns it. A lesson that adds behavior also needs a claim, which belongs to
  the specification author.
- Where a section has no natural owner because the project has no such role, it
  sits with the nearest role and **the compromise is written down**. A decision
  bigger than the item in hand then stops and goes to the person, with the
  options and the tradeoff named, rather than being settled inside an
  implementation pull request.

## Records beside a specification

Three kinds, each with one job:

- **decisions** — product and scope calls that were decided, reneged, or
  redirected.
- **architecture decision records** — durable technical choices.
- **lessons** — one file each: symptom, root cause, specification delta, and the
  claim proving the delta held.

**Decision records and lessons split by blast radius**: one that binds every
area lives in the repository-wide directory, numbered repository-wide; one
scoped to a single area lives beside that area's specification. A library, a
transport or a layout rule binds everything; a choice about one area's internals
does not.

## Identifiers are claimed after rebasing

An id, a number or a slug is claimed the moment someone else merges it. Take it
after rebasing onto the fresh trunk, never from the tree as you started
([`deliver-change`](../deliver-change/SKILL.md)). Lost the race? Take the new
number and rewrite every reference; renaming is cheap.

## Never add

- A specification file outside the specification directory, or a second place
  claims live.
- Delivery status on a specification that disagrees with its item, or a claim
  restated in an item instead of referenced.
- A renumbered or reused claim id, or a withdrawn claim deleted rather than
  marked.
- A tracker-shaped record the project's workflow does not use.
- A link to a decision record that does not exist.
- A hand-kept index of files that a tool could generate. It falls behind.
