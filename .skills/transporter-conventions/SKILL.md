---
name: transporter-conventions
description: Everything specific to this repository — paths, layout, naming, the local tracker, the build and test commands, the specification scheme and its section owners — extending the five method skills. Use for any change, alongside the method skill the work belongs to.
---

# Transporter conventions

Everything specific to this repository. Each section below extends one method
skill — read that skill first, then the reference here, which wins where they
differ.

| Reference                            | Extends                                                      | Holds                                                                      |
| ------------------------------------ | ------------------------------------------------------------ | -------------------------------------------------------------------------- |
| [coding](references/coding.md)       | [`coding-conventions`](../coding-conventions/SKILL.md)       | naming, layout, project structure, dependencies, generated files, comments |
| [delivery](references/delivery.md)   | [`deliver-change`](../deliver-change/SKILL.md)               | the local tracker, branch and worktree shapes, verify and publish          |
| [testing](references/testing.md)     | [`test-from-scenarios`](../test-from-scenarios/SKILL.md)     | xUnit, assertions, doubles, generated fixtures, analyzer tests             |
| [questions](references/questions.md) | [`clarify-requirements`](../clarify-requirements/SKILL.md)   | what is worth asking here, and where the answer already is                 |
| [specs](references/specs.md)         | [`spec-and-traceability`](../spec-and-traceability/SKILL.md) | where records live, the templates, claim ids, section ownership            |

## Before anything

- **Read the specification for what you are touching**:
  [`README.md`](../../README.md) project-wide, and
  the `.spec/README.md` beside the code where a Feature covers it.
- **Read the lessons**: [`.spec/lessons/`](../../.spec/lessons/) repository-wide,
  and a Feature's own `.spec/lessons/`.
- Code graph, when one exists: `graphify query` / `graphify explain`
  (`AGENTS.md` § "Knowledge Graph Integration").

## The rules that bind every change

These are here rather than in a reference because they decide whether work may
start at all, or stop it being started twice.

- **An item does not move to `in-progress` while its specification's § 12 rows
  are 🟡.** The person may waive it for a named item, and the waiver is a row in
  that item's `decisions`. What `approved` requires is
  [`feature.md`](../../.spec/templates/feature.md) § 12; a `Missing` row in § 9
  is not part of it
  ([lesson 0007](../../.spec/lessons/0007-a-gate-that-waits-on-what-it-gates-never-closes.md)).
- **One item `in-progress` at a time, per Feature.** An item that cannot be
  finished, demonstrated and reviewed without a sibling is a decomposition
  defect, not a small item
  ([lesson 0008](../../.spec/lessons/0008-an-item-that-cannot-be-worked-alone-is-a-decomposition-defect.md)).
- **Branch off `main`. If the work can be done off `main`, it is.** Stack only
  where the later work cannot compile or be tested without the earlier; one
  dependent level at most, and a dependent pull request is retargeted to `main`
  the moment its base merges
  ([lesson 0009](../../.spec/lessons/0009-a-stack-drains-into-its-base-not-into-main.md)).
- **Terse, and XML over inline.** A `<summary>` is one line, `<remarks>` one
  sentence and only for what the code cannot state, an inline comment one line
  saying a _why_. Reasoning belongs in the specification, an ADR or a lesson —
  cite instead of explaining.
- **`./build.sh`** is what CI runs and what a change is verified with; read
  `Format`'s output rather than its exit code
  ([`nuke-build`](../nuke-build/SKILL.md)).

## Never add

- A GitHub issue, label or milestone as part of this workflow. An `.issue/` item
  is the tracker; a remote is for code.
- An `@ignore` tag, a step definition, or anything else implying the scenarios
  execute.
- A rule in this file that belongs in a reference. `SKILL.md` loads whole on
  activation; a reference loads when its subject is in play.
