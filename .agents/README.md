---
title: Role agents
description: The four role contracts that own the specification chain, what each owns, and how work is tracked locally.
type: instructions
---

# Role agents

Four roles own the specification chain for Transponder. Each is a **documented
contract for whoever takes that role** — a person or an agent. This repository
ships no loadable agents: the directory is `.agents/`, not `.claude/agents/`,
so nothing here is resolved by name. Read the file, take the role, honour the
boundary.

## The chain

```
need  →  spec-author  →  test-writer  →  implementer  →  spec-reviewer
         (spec, §§1-5)    (§§8-9, tests)   (§§6-7, code)   (§12)
```

**`spec-author` goes first, with nothing upstream but the need.** A Feature's
specification is authored before any work item exists — it is the agreement
that items are cut from — so the chain starts at a decided need, not at a
tracker entry. Items appear after § 3 carries claims to cut them from.

**Each role trusts only the artifact from the role before it.** The test writer
works from the claim, not from the conversation that produced it. The
implementer works from the failing test and the claims it cites. The reviewer
works from the diff and the claims it cites. That is what makes the chain worth
having: no stage has to reconstruct the reasoning of the stage before.

| Role | File | Owns |
|---|---|---|
| `spec-author` | [`spec-author.md`](spec-author.md) | § 1-5, the `.feature` file, `## Tasks`, `## Scoring` |
| `test-writer` | [`test-writer.md`](test-writer.md) | § 8, § 9, and the xUnit tests |
| `implementer` | [`implementer.md`](implementer.md) | § 6, § 7, and the production code |
| `spec-reviewer` | [`spec-reviewer.md`](spec-reviewer.md) | § 12 |

Section numbers are the twelve sections of a Feature's `.spec/README.md`, whose
blank is [`.spec/templates/feature.md`](../.spec/templates/feature.md). **The
authoritative ownership table — including § 10, § 11 and Decisions — lives in
[`transponder-conventions`](../.skills/transponder-conventions/SKILL.md)**, in one
place, so it has nowhere to drift from. A role writes only its own sections;
filling in someone else's is a boundary violation, not a favour.

## How work is tracked

**Locally.** A `<id>-<slug>.yml` item stands in for a GitHub issue and holds
delivery state — status, priority, scoring, dependencies. The specification
holds the content, and **comes first**: a feature item names its spec, a spec
never names an item. A bug, spike or chore starts at an item instead. Code
still pushes to GitHub as branches and pull requests; issues, labels and
milestones are not part of this workflow.

**An item sits beside the specification it was cut from**, in that Feature's
`.issue/`, a sibling of its `.spec/`. One with no specification goes in the
repository-root `.issue/`, where `.sequence` hands out ids repository-wide.

`status: in-progress` on an item is the only signal it is taken. The schema,
the status enum, and the authority split are in
[`transponder-conventions`](../.skills/transponder-conventions/SKILL.md), under the model
[`spec-and-traceability`](../.skills/spec-and-traceability/SKILL.md) describes; the
mechanics of claiming an item and publishing the work are in
[`deliver-change`](../.skills/deliver-change/SKILL.md).

## Two standing facts

- **Scenarios are documentation**, and the xUnit tests are what execute —
  [`transponder-conventions`](../.skills/transponder-conventions/SKILL.md) has
  the rule.
- **`src/` is a demo, not an adopted architecture.** The sample code came from
  package documentation. The skills say which of their guidance is a fact about
  the outside world and which is a provisional pattern; roles should respect
  that distinction rather than defending a shape nobody chose.
