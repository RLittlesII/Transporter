# Specifications and traceability

Where every record lives, the templates, claim ids, the numbering schemes, and who owns which section.

## Where things live

| Path                                             | Holds                                                                                                                                     |
| ------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------- |
| [`README.md`](../../../README.md)                | the project-wide specification                                                                                                            |
| `features/<slug>/.spec/README.md`                | one Feature's specification, twelve sections in order — permanent, never archived                                                         |
| `features/<slug>/.spec/<slug>.feature`           | that Feature's scenarios                                                                                                                  |
| `features/<slug>/.spec/{decisions,lessons,adr}/` | that Feature's own records                                                                                                                |
| [`.spec/adr/`](../../../.spec/adr/)              | **cross-cutting** technical decisions, numbered repository-wide from `0001`                                                               |
| [`.spec/lessons/`](../../../.spec/lessons/)      | **cross-cutting** lessons, numbered repository-wide from `0001`                                                                           |
| [`.spec/templates/`](../../../.spec/templates/)  | the blanks                                                                                                                                |
| `features/<slug>/.issue/<id>-<slug>.yml`         | one work item cut from that Feature's specification                                                                                       |
| [`.issue/`](../../../.issue/)                    | **cross-cutting** work items — a bug, spike or chore with no specification; `.issue/.sequence` is the last id handed out, repository-wide |

There is no epic directory: an epic is an item of `type: epic` whose `children`
list the Features under it.

## The templates

| Template                                              | Produces                      |
| ----------------------------------------------------- | ----------------------------- |
| [`feature.md`](../../../.spec/templates/feature.md)   | a Feature's `.spec/README.md` |
| [`adr.md`](../../../.spec/templates/adr.md)           | one `adr/` record             |
| [`decision.md`](../../../.spec/templates/decision.md) | one `decisions/` record       |
| [`lesson.md`](../../../.spec/templates/lesson.md)     | one `lessons/` record         |
| [`item.yml`](../../../.spec/templates/item.yml)       | one `.issue/` work item       |

**[`item.yml`](../../../.spec/templates/item.yml) is the item schema**, commented
field by field — `type`, `status`, `risk` and `title` are never omitted, and
`risk` is never inherited. Copy it; do not re-derive it from an example.

**Status enum**: `needs-decomposition`, `ready-for-architecture`,
`ready-for-implementation`, `ready`, `in-progress`, `in-review`, `blocked`,
`done`. `priority`, `rank` and `blocks` are derived, and
[`item.yml`](../../../.spec/templates/item.yml) carries the derivation beside the
fields it applies to.

## Declarations in § 7

§ 7 Technical Design writes a type's declaration out only while its file does
not exist. Once the file exists the declaration becomes a row in § 7's type
table — `| Type | File | Claims it makes visible |`, the file as a relative
link so the link check catches a move — and the folder the type sat in drops
out of § 7's layout block. The prose around it stays: the naming argument and
the claim it answers are the specification's own and live nowhere else.

## Claim ids are `B-00n`

Claims are numbered rows in § 3 Acceptance Criteria; scenarios carry the
matching `@B-00n` tag; **§ 9 Traceability Matrix is the gate**, and a `Missing`
row blocks ship.

**Ids are scoped to their Feature, not to the repository.** Every specification
numbers from `B-001`, so one Feature's `B-007` and another's are different
claims and neither is renumbered for the other. What identifies a claim is the
pair: an item carries `claims:` alongside the `spec:` those claims live in.
Within one specification an id is never renumbered and never reused. Claims need
no sequence file, which is what lets two Features be specified at once.

An unbuilt claim is marked once, in § 9 — a row naming no test. § 3 carries no
build state, because it would be a second store for what § 9 already decides.
A **withdrawn** claim is the exception: § 9 has no row for a claim that was
never going to be built, so it is marked on the § 3 claim itself, which opens
`**Withdrawn** —`.

## Four-digit ids collide — always write the scheme

Claims carry a `B-` prefix and identify themselves. Nothing else here does:
three schemes number from `0001` independently, and the per-Feature schemes
start again in every Feature.

| Written in full                                            | Scheme                              |
| ---------------------------------------------------------- | ----------------------------------- |
| `features/aircraft-source/.issue/0001-aircraft-source.yml` | work item, repository-wide sequence |
| `ADR-0001`                                                 | root ADR, repository-wide           |
| `lesson 0001`                                              | root lesson, repository-wide        |
| `features/<slug>/.spec/decisions/0001`                     | that Feature's decisions            |
| `features/<slug>/.spec/adr/0001`                           | that Feature's own ADRs             |

**In prose, write the scheme with the number.** A number may appear bare only
where its field says what it is — `parent: "0001"` in an item, or a `## Tasks`
table whose column reads _Item_.

## Section ownership

Four roles own the specification chain, declared under
[`.agents/`](../../../.agents/README.md). **This is the only place the mapping is
written**; each role file names its own sections and links here.

| Section                  | Owner                                              |
| ------------------------ | -------------------------------------------------- |
| 1 Business Goal          | `spec-author`                                      |
| 2 User Needs             | `spec-author`                                      |
| 3 Acceptance Criteria    | `spec-author`                                      |
| 4 Constraints            | `spec-author`                                      |
| 5 Out of Scope           | `spec-author`                                      |
| 6 Concern Separation     | `implementer`                                      |
| 7 Technical Design       | `implementer`                                      |
| 8 Testing Strategy       | `test-writer`                                      |
| 9 Traceability Matrix    | `test-writer`                                      |
| 10 Lessons / Spec Deltas | the role that closed the bug                       |
| 11 Open Questions        | any blocked role                                   |
| 12 Sign-off              | `spec-reviewer`                                    |
| Decisions (index)        | the role that made or reversed the call            |
| `## Tasks`               | `spec-author`, from that Feature's `.issue/` items |
| `## Scoring`             | `spec-author`, from the item's value and risk      |

**`implementer` owning § 7 is a compromise.** There is no separate architect
role here and § 7 must have exactly one owner, so it sits with the role that
writes the code. A design decision bigger than the item in hand stops and goes
to the person, with the options and the tradeoff named, rather than being
settled inside an implementation pull request.

## Never add

- A GitHub issue, label or milestone as part of this workflow. An `.issue/` is the
  tracker; a remote is for code.
- An `@ignore` tag, a step definition, or anything else implying the scenarios
  execute.
