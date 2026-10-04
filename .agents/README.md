---
title: Role agents
description: The four role contracts that own the specification chain, what each owns, and how work is tracked locally.
type: instructions
---

# Role agents

Four roles own the specification chain for Transponder. Each is a **documented
contract**, not a loadable agent: this directory is `.agents/`, not
`.claude/agents/`, so Claude Code does not resolve these by name. The agents
that *are* loadable are the user-level council in `~/.claude/agents/`, and each
file here names its counterpart there so an escalation has an address.

## The chain

```
spec-author  →  test-writer  →  implementer  →  spec-reviewer
```

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

Section numbers are the twelve sections of a Feature's `.spec/README.md`. **The
authoritative ownership table — including § 10, § 11, Decisions, and the
council counterparts — lives in
[`spec-and-traceability`](../.skills/spec-and-traceability/SKILL.md)**, in one
place, so it has nowhere to drift from. A role writes only its own sections;
filling in someone else's is a boundary violation, not a favour.

## How work is tracked

**Locally.** `.issues/<id>-<slug>.yml` stands in for a GitHub issue and holds
delivery state — status, priority, scoring, dependencies. The specification
holds the content. Code still pushes to GitHub as branches and pull requests;
issues, labels and milestones are not part of this workflow.

`status: in-progress` on an item is the only signal it is taken. The schema,
the status enum, and the authority split are in
[`spec-and-traceability`](../.skills/spec-and-traceability/SKILL.md); the
mechanics of claiming an item and publishing the work are in
[`deliver-change`](../.skills/deliver-change/SKILL.md).

## Two standing facts

- **Scenarios are documentation.** There is no Gherkin runner in this
  repository — no Reqnroll, no step definitions. The `.feature` file is the
  readable specification; the xUnit tests in
  [`test/UnitTests`](../test/UnitTests) are what execute.
- **Nothing in `src/` is an adopted architecture yet.** It is a demo, and the
  sample code came from package documentation. The skills say which of their
  guidance is a fact about the outside world and which is a provisional
  pattern; roles should respect that distinction rather than defending a shape
  nobody chose.
