---
name: spec-and-traceability
description: Where a Transponder specification lives and how claims trace to scenarios and tests — the .spec/README.md twelve-section template with B-00n claim IDs. Use when authoring, amending, or reviewing a specification or a claim.
---

# Specification and traceability

Project companion to the global `specification` skill, which owns the
`.spec/README.md` contract, its twelve sections, and the section-ownership
rules. This file says where that lands in this repository and which parts are
real here today.

## Today: the README is the specification

[`README.md`](../../README.md) is currently the only specification Transponder
has — audience, core idea, the app, the operator list, the data source and its
limits, demo resilience, the closing act, alternatives considered, and the open
items. Treat it as authoritative until a `.spec/` exists, and **keep it true**:
a decision made in conversation that contradicts it is a README edit in the
same change.

`AGENTS.md` points at `features/README.md`, which does not exist. That is
drift, not a location to create by hand.

## Where a specification goes when one is written

The repository has **no GitHub remote yet** (`AGENTS.md`), so the
`local-epic-manager` layout applies:

```
epics/<epic-id>-<epic-slug>/epic.md
epics/<epic-id>-<epic-slug>/F<n>-<feature-slug>/.spec/README.md
epics/<epic-id>-<epic-slug>/F<n>-<feature-slug>/.spec/<feature-slug>.feature
epics/<epic-id>-<epic-slug>/F<n>-<feature-slug>/.spec/{decisions,lessons,adr}/
```

Once a remote exists and issues are created, the document root becomes
`features/<feature-name>/.spec/` instead. Either way `.spec/README.md` is
**permanent** — never archived, never frozen — and the `specification` skill
owns its content.

## Claim IDs are `B-00n`

Claims are numbered rows in § 3 Acceptance Criteria of `.spec/README.md`, and
scenarios in the companion `.feature` file carry the matching `@B-00n` tag. § 9
Traceability Matrix is the gate: every claim ID appears there exactly once, and
a `Missing` row blocks ship.

**`AGENTS.md` describes a different scheme** — `REQ-<AREA>-<NNN>` tags with a
generated `docs/traceability.md` that fails the build. There is no `docs/`
directory and no generator, and the global tooling is built around `B-00n`. Use
`B-00n`; `AGENTS.md` is being corrected to match rather than a generator being
written for a demo.

## Records

Inside the Feature's `.spec/`:

- `decisions/` — product and scope calls that were decided, reneged, or
  redirected.
- `adr/` — durable technical choices. **Nothing in this repo has one yet**, so
  no skill links to an ADR; the technology list in README § "Technology
  Decisions" is the closest thing, and it is a list, not a record.
- `lessons/` — one file per lesson: symptom, root cause, spec delta, and the
  claim proving the delta held. A bug fix that reveals a specification gap
  ships its lesson in the same change (`AGENTS.md`).

## Decisions this demo still owes a record

Open questions that will want writing down when the specification is authored,
each already named in a skill:

- the bounding box and polling interval, which are one credit-budget decision
  ([`api-contract`](../api-contract/SKILL.md));
- what the cache does on a source swap — clear-and-refill or expiry-drain
  ([`hot-swap-source`](../hot-swap-source/SKILL.md));
- `ClientWebSocket` versus `AISStream.NET`
  ([`ais-stream`](../ais-stream/SKILL.md));
- whether a view model reaches its actor with `Tell` or `Ask`, per command
  ([`mvvm`](../mvvm/SKILL.md));
- whether the build gets a `Test` target ([`nuke-build`](../nuke-build/SKILL.md)).

## The roles

Four role agents own the chain, declared under
[`.agents/`](../../.agents/README.md): spec author → test writer →
implementer → spec reviewer. Each trusts only the artifact from the role before
it. Their project detail lives in the skills they name —
[`test-from-scenarios`](../test-from-scenarios/SKILL.md) and
[`coding-conventions`](../coding-conventions/SKILL.md).

## Never add

- A specification file outside `.spec/`, or a second place claims live.
- A `REQ-`-style ID, or a hand-written `docs/traceability.md`.
- A renumbered or reused claim ID. A withdrawn claim is marked withdrawn, not
  deleted — § 9 and the `.feature` file are anchored to it.
- A scenario un-ignored but unimplemented, or an obsolete one parked behind
  `@ignore` instead of deleted.
- A link to an ADR that does not exist.
