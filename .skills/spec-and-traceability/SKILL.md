---
name: spec-and-traceability
description: Where a Transponder specification lives, who owns each of its twelve sections, and how B-00n claims trace through scenarios to xUnit tests. Also the .issues/ tracker that stands in for GitHub issues. Use when authoring, amending, or reviewing a specification, a claim, or a work item.
---

# Specification and traceability

Project companion to the global `specification` skill, which owns the
`.spec/README.md` contract, its twelve sections, and the section-ownership
table. This file says where that lands in this repository, who owns what here,
and which parts are real today.

## Today: the README is the specification

[`README.md`](../../README.md) is currently the only specification Transponder
has — audience, core idea, the app, the operator list, the data source and its
limits, demo resilience, the closing act, alternatives considered, and the open
items. Treat it as authoritative until a `.spec/` exists, and **keep it true**:
a decision made in conversation that contradicts it is a README edit in the
same change.

## Two records, one authority each

Work is tracked **locally**. There are no GitHub issues, labels or milestones in
this workflow — `.issues/` does that job — while code still pushes to GitHub as
branches and pull requests.

| Record | Authority over | Never holds |
|---|---|---|
| `.issues/<id>-<slug>.yml` | **delivery state** — status, priority, value/risk/rank, dependencies, what is being worked on | requirements, design, test plans |
| `features/<slug>/.spec/README.md` | **content** — the twelve sections: requirements, constraints, design, testing strategy, traceability | the live status of the work |

- The spec's `status` frontmatter is **mirrored** from its item's `status` — the
  global skill mirrors it from a GitHub `status:*` label, and here that label's
  job belongs to `.issues/`. Do not hand-edit it on the spec.
- `priority` stays authored in the spec's own frontmatter, exactly as the global
  skill says, because no label ever carried it.
- The spec's `## Tasks` section lists `.issues/` **ids**, not restated titles.
- One claim lives in one place: § 3 Acceptance Criteria. An item's `claims:`
  field **references** ids; it never restates the claim text.

## `.issues/` — the local tracker

```
.issues/.sequence                   # last id handed out
.issues/0001-poll-opensky.yml       # one file per work item
```

```yaml
id: "0001"
type: feature            # epic | feature | task | test | bug | spike
title: Poll OpenSky for aircraft snapshots
status: ready
priority: high
value: 4                 # optional on a child — inherits its parent's
risk: 3                  # required, never inherited
rank: 1
parent: null             # id of the epic or feature it hangs under
children: []
depends_on: []           # hard prerequisites, by id
blocks: []               # derived from other items' depends_on
spikes: []
spec: features/aircraft-source/.spec/README.md   # null until one exists
claims: [B-001, B-002]
created: "2026-10-04"
updated: "2026-10-04"
closed: null
github_issue: null       # stamped only if these ever migrate
```

Field names come from the installed `local-epic-manager`
(`assets/child.md`) so a later migration to real issues is mechanical rather
than a rewrite.

Below the frontmatter, markdown: a `## Summary` that is **user-story shaped**
for a feature (`As a … I want … so that …`), and acceptance criteria that cite
claim ids.

**Status enum**, from `github-project-manager/references/labels.md`:
`needs-decomposition`, `ready-for-architecture`, `ready-for-implementation`,
`ready`, `in-progress`, `in-review`, `blocked`, `done`.

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

`.spec/README.md` is **permanent** — never archived, never frozen. Because
`.issues/` stands in for the issue, the document root is the global skill's
issue-exists form (`features/<name>/`) rather than the `epics/` tree it uses
when there is no tracker at all. An epic is simply an item of `type: epic`
whose `children` list the features under it.

## Claim IDs are `B-00n`

Claims are numbered rows in § 3 Acceptance Criteria. Scenarios in the companion
`.feature` file carry the matching `@B-00n` tag, and **§ 9 Traceability Matrix
is the gate**: every claim appears there exactly once, and a `Missing` row
blocks ship.

**Scenarios are documentation.** The `.feature` file is the readable
specification; **xUnit tests are what execute**, each citing the claim it
proves. There is no Gherkin runner in this repository — no Reqnroll, no
bindings, no step definitions — and none is planned for a demo. See
[`test-from-scenarios`](../test-from-scenarios/SKILL.md).

So an unbuilt claim is marked in two places that already exist, and nowhere
else: the § 3 `Status` column, and a § 9 row reading `Missing`. There is no
runner to hide a scenario from, so there is no `@ignore`.

## Section ownership

The global skill assigns the twelve sections to eight council agents. This
repository has four local roles ([`.agents/`](../../.agents/README.md)), so the
table collapses onto them. **This is the only place the mapping is written**;
each role file names its own sections and links here.

| Section | Owner | Council counterpart |
|---|---|---|
| 1 Business Goal | `spec-author` | `business-analyst` |
| 2 User Needs | `spec-author` | `business-analyst` |
| 3 Acceptance Criteria | `spec-author` | `business-analyst` |
| 4 Constraints | `spec-author` | `business-analyst` |
| 5 Out of Scope | `spec-author` | `business-analyst` |
| 6 Concern Separation | `implementer` | `the-architect` |
| 7 Technical Design | `implementer` | `the-architect`, then `mr-anderson` |
| 8 Testing Strategy | `test-writer` | `behavior-driver` |
| 9 Traceability Matrix | `test-writer` | `behavior-driver` |
| 10 Lessons / Spec Deltas | the role that closed the bug | `the-orkin-man`, `thoth` |
| 11 Open Questions | any blocked role | any blocked agent |
| 12 Sign-off | `spec-reviewer` | `stinkmeaner`, `vane` |
| Decisions (index) | the role that made or reversed the call | `the-orkin-man`, `thoth` |
| `## Tasks` | `spec-author`, from the `.issues/` items | `vane` |
| `## Scoring` | `spec-author`, from the item's value and risk | `feature-prioritization` |

Two honest notes about the collapse:

- **`implementer` owning § 7 is a compromise.** There is no local architect
  role and § 7 must have exactly one owner. For a design decision bigger than
  the item in hand, escalate to the installed `the-architect` rather than
  settling it inside an implementation pull request.
- **A role writes only its own sections.** `implementer` filling in a missing
  § 3 claim is a boundary violation, not a favour — escalate to `spec-author`.
  A § 10 lesson that adds behavior also needs a § 3 row, which is
  `spec-author`'s to write.

## Records inside `.spec/`

- `decisions/` — product and scope calls that were decided, reneged, or
  redirected.
- `adr/` — durable technical choices. **Nothing in this repo has one yet**, so
  no skill links to an ADR; the technology list in README § "Technology
  Decisions" is a list, not a record.
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
