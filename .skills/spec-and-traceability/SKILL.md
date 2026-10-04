---
name: spec-and-traceability
description: Where a Transponder specification lives, who owns each of its twelve sections, and how B-00n claims trace through scenarios to xUnit tests. Also the .issues/ tracker that stands in for GitHub issues. Use when authoring, amending, or reviewing a specification, a claim, or a work item.
---

# Specification and traceability

This file owns the specification model: where a specification lives, who owns
each section, how claims trace to tests, and how work is tracked. The blanks it
describes are committed in [`.spec/templates/`](../../.spec/templates/) —

| Template | Produces |
|---|---|
| [`feature.md`](../../.spec/templates/feature.md) | a Feature's `.spec/README.md`, twelve sections in order |
| [`adr.md`](../../.spec/templates/adr.md) | one `adr/` record — a durable technical choice |
| [`decision.md`](../../.spec/templates/decision.md) | one `decisions/` record — a product or scope call |
| [`lesson.md`](../../.spec/templates/lesson.md) | one `lessons/` record |
| [`item.yml`](../../.spec/templates/item.yml) | one `.issues/` work item |

**`.spec/` means three things, so keep them straight**:

| Path | Holds |
|---|---|
| `.spec/templates/` | the blanks, listed above |
| `.spec/adr/` | **cross-cutting** technical decisions — ones that bind every Feature, numbered repo-wide from `0001` |
| `features/<slug>/.spec/` | one Feature's specification, its `.feature` file, and its own `decisions/`, `lessons/` and `adr/` |

A decision goes in the root `adr/` or a Feature's by **blast radius**: a
library, a transport or a layout rule binds everything, while a choice about
one Feature's internals does not. The first root ADR is
[`0001-flurl-for-http.md`](../../.spec/adr/0001-flurl-for-http.md), which also
shows the shape.

## Today: the README is the project specification

[`README.md`](../../README.md) is the project-wide specification — audience,
core idea, the app, the operator list, the data source and its limits, demo
resilience, the closing act, alternatives considered, and the open items. Treat
it as authoritative wherever no Feature `.spec/` covers the question, and **keep
it true**: a decision made in conversation that contradicts it is a README edit
in the same change.

One Feature specification exists so far —
[`features/aircraft-source/.spec/README.md`](../../features/aircraft-source/.spec/README.md),
`spec_status: draft` — and it is authoritative over its own subject: the API
types, the snapshot, the seam, the cache and the tracker. Where it and the
README overlap, the Feature spec is the narrower and more recent record.

## Where a spec starts

**A Feature starts with its specification, not with a work item.** The
specification is the design authority (`AGENTS.md` § "Core Principles"), so it
cannot be made to wait on the tracker that delivers it.

- **The trigger is a decided need** — today that means
  [`README.md`](../../README.md) and a conversation that settled something.
  Not an item; there is no item yet.
- **§§ 1-5 come first**: business goal, user needs, the `B-00n` claims,
  constraints, out of scope. That is the agreement.
- **§§ 6-7 follow**: concern separation and technical design.
- **Items are cut from § 3 afterwards**, each citing the claims it delivers.
  A feature item cannot exist before its claims do, because it references
  them.

So: **a feature item names its spec; a spec never names an item.** One
direction, nothing to keep in sync. The spec's `## Tasks` lists the ids cut
from it, and `"None yet."` is the honest state while the agreement is still
being reached.

**Features only.** A **bug, spike or chore starts with an item** — its trigger
is an observation, not an agreement — and leaves `spec:` null. A bug that
reveals a specification gap then produces a spec delta: a § 10 lesson and the
§ 3 claim that proves the delta held.

Put the other way round: the spec is where *agreement* starts; the item is
where *delivery* starts. They are different questions, and the chain in
`AGENTS.md` answers them in that order.

## Two records, one authority each

Work is tracked **locally**. There are no GitHub issues, labels or milestones in
this workflow — `.issues/` does that job — while code still pushes to GitHub as
branches and pull requests.

| Record | Authority over | Never holds | Exists first? |
|---|---|---|---|
| `features/<slug>/.spec/README.md` | **content** — the twelve sections: requirements, constraints, design, testing strategy, traceability | the live status of the work | **yes, for a feature** |
| `.issues/<id>-<slug>.yml` | **delivery state** — status, priority, value/risk/rank, dependencies, what is being worked on | requirements, design, test plans | yes, for a bug, spike or chore |

- A spec carries `spec_status` — its own **document** maturity (`draft`,
  `in-review`, `approved`, `superseded`) — and nothing about delivery. The
  work's `status`, `priority`, `value`, `risk` and `rank` live in the item, and
  only there. Two copies of a status is two answers to one question.
- The spec's `## Tasks` section lists `.issues/` **ids**, not restated titles.
- One claim lives in one place: § 3 Acceptance Criteria. An item's `claims:`
  field **references** ids; it never restates the claim text. Those ids are
  always already there — the spec was written first.

## `.issues/` — the local tracker

```
.issues/.sequence                   # last id handed out
.issues/0001-poll-opensky.yml       # one file per work item
```

**The schema is [`.spec/templates/item.yml`](../../.spec/templates/item.yml)**,
commented field by field. Copy it; do not re-derive it from an example. In
outline:

```yaml
id: "0001"               # string, so leading zeros survive
type: feature            # epic | feature | task | test | bug | spike
title: Poll OpenSky for aircraft snapshots
status: ready
priority: high           # derived from value and risk
value: 4                 # optional on a child — inherits its parent's
risk: 3                  # required, never inherited
rank: 1                  # derived; orders items inside a priority bucket
parent: null
children: []
depends_on: []           # hard prerequisites, by id
blocks: []               # derived from other items' depends_on
spec: features/aircraft-source/.spec/README.md   # required for a feature
claims: [B-001, B-002]   # ids only; they already exist, the spec came first
summary: |
  As a <persona> I want <capability> so that <outcome>.
```

The whole file is YAML — `summary`, `acceptance_criteria`, `decisions` and
`out_of_scope` are fields, not a markdown body, so an item can be read by a
script as easily as by a person.

**Status enum**: `needs-decomposition`, `ready-for-architecture`,
`ready-for-implementation`, `ready`, `in-progress`, `in-review`, `blocked`,
`done`. An item moves forward through those and back to `blocked` whenever a
gate fails.

- `priority`, `rank` and `blocks` are **derived** — recompute, never hand-edit.
- **A closed item stays.** `status: done` and a `closed:` date; the file is
  permanent history, like the spec itself.
- Ids come from `.issues/.sequence`, are claimed **after** rebasing like any
  shared identifier, and are never reused.
- `status: in-progress` is the only signal an item is taken. Never pick up an
  item that already carries it, and set it before the worktree, the branch, or
  the first edit — see [`deliver-change`](../deliver-change/SKILL.md).

## Where a specification goes

```
features/<feature-slug>/.spec/README.md
features/<feature-slug>/.spec/<feature-slug>.feature
features/<feature-slug>/.spec/{decisions,lessons,adr}/
```

`.spec/README.md` is **permanent** — never archived, never frozen. One
directory per Feature under `features/`, named for the Feature's slug; there is
no epic directory tree, because an epic is simply an item of `type: epic` whose
`children` list the features under it.

## Claim IDs are `B-00n`

Claims are numbered rows in § 3 Acceptance Criteria. Scenarios in the companion
`.feature` file carry the matching `@B-00n` tag, and **§ 9 Traceability Matrix
is the gate**: every claim appears there exactly once, and a `Missing` row
blocks ship.

**Ids are scoped to their Feature, not to the repository.** Every spec numbers
from `B-001`, so `aircraft-source`'s B-007 and `replay-source`'s B-007 are
different claims and neither is renumbered for the other. What identifies a
claim is the pair — an `.issues/` item carries `claims:` alongside the `spec:`
those claims live in, and § 3 and § 9 are both per-Feature sections, so an id is
only ever read against a named § 3. Permanence is per-Feature too: within one
spec an id is never renumbered and never reused. Compare the two schemes that
*are* repo-wide, each of which says so and has a mechanism — the root
[`adr/`](../../.spec/adr/) is numbered repo-wide from `0001`, and item ids come
from `.issues/.sequence`. Claims need no sequence file, which is what lets two
Features be specified at once without colliding.

**Scenarios are documentation.** The `.feature` file is the readable
specification; **xUnit tests are what execute**, each citing the claim it
proves. There is no Gherkin runner in this repository — no Reqnroll, no
bindings, no step definitions — and none is planned for a demo. See
[`test-from-scenarios`](../test-from-scenarios/SKILL.md).

So an unbuilt claim is marked in two places that already exist, and nowhere
else: the § 3 `Status` column, and a § 9 row reading `Missing`. There is no
runner to hide a scenario from, so there is no `@ignore`.

## Section ownership

Every section has **exactly one owning role** — four roles, declared in
[`.agents/`](../../.agents/README.md). **This is the only place the mapping is
written**; each role file names its own sections and links here.

| Section | Owner |
|---|---|
| 1 Business Goal | `spec-author` |
| 2 User Needs | `spec-author` |
| 3 Acceptance Criteria | `spec-author` |
| 4 Constraints | `spec-author` |
| 5 Out of Scope | `spec-author` |
| 6 Concern Separation | `implementer` |
| 7 Technical Design | `implementer` |
| 8 Testing Strategy | `test-writer` |
| 9 Traceability Matrix | `test-writer` |
| 10 Lessons / Spec Deltas | the role that closed the bug |
| 11 Open Questions | any blocked role |
| 12 Sign-off | `spec-reviewer` |
| Decisions (index) | the role that made or reversed the call |
| `## Tasks` | `spec-author`, from the `.issues/` items |
| `## Scoring` | `spec-author`, from the item's value and risk |

Two honest notes:

- **`implementer` owning § 7 is a compromise.** There is no separate architect
  role here and § 7 must have exactly one owner, so it sits with the role that
  writes the code. A design decision bigger than the item in hand stops and
  goes to the person, with the options and the tradeoff named, rather than
  being settled inside an implementation pull request.
- **A role writes only its own sections.** `implementer` filling in a missing
  § 3 claim is a boundary violation, not a favour — escalate to `spec-author`.
  A § 10 lesson that adds behavior also needs a § 3 row, which is
  `spec-author`'s to write.

## Records inside `.spec/`

- `decisions/` — product and scope calls that were decided, reneged, or
  redirected.
- `adr/` — durable technical choices **scoped to this Feature**. One that binds
  every Feature goes in the root `.spec/adr/` instead.
- `lessons/` — one file per lesson: symptom, root cause, spec delta, and the
  claim proving the delta held. A bug fix that reveals a specification gap
  ships its lesson in the same change (`AGENTS.md`).

## Decisions this demo still owes a record

Each is already named in the skill that raised it:

- the bounding box and polling interval, which are one credit-budget decision
  ([`api-contract`](../api-contract/SKILL.md));
- what the cache does on a source swap — clear-and-refill or expiry-drain
  ([`hot-swap-source`](../hot-swap-source/SKILL.md));
- `ClientWebSocket` versus `AISStream.NET`
  ([`ais-stream`](../ais-stream/SKILL.md));
- whether a view model reaches its actor with `Tell` or `Ask`, per command
  ([`mvvm`](../mvvm/SKILL.md));
- whether the build gets a `Test` target
  ([`nuke-build`](../nuke-build/SKILL.md)).

## Never add

- A specification file outside `.spec/`, or a second place claims live.
- A GitHub issue, label or milestone as part of this workflow. `.issues/` is
  the tracker; a remote is for code.
- Delivery status on the spec that disagrees with its item, or a claim restated
  in an item instead of referenced.
- A renumbered or reused claim id, or a withdrawn claim deleted rather than
  marked `Withdrawn` — § 9 and the `.feature` file are anchored to it.
- A `@ignore` tag, a step definition, or anything else implying the scenarios
  execute.
- A link to an ADR that does not exist.
