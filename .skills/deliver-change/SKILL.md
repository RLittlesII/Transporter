---
name: deliver-change
description: Deliver a change through a work item, a worktree, the specification, the build, and a pull request. Use when picking up work, filing an item, committing, or opening and watching a pull request.
---

# Deliver a change

**Project rules.** A project may extend this skill with a companion skill that
names this one; its agent instructions (`AGENTS.md`) list it. Read both. The
companion holds the tracker, the item schema, the commands, the branch and
report shapes, and the paths for each step here, and wins where they differ.

A change moves through three phases. Read the reference for the phase you are
in; they are ordered, and skipping one is what produces work nobody can review.

| Reference | When | Holds |
|---|---|---|
| [start](references/start.md) | before the first edit | which way in — specification, item, bug or chore; claiming the item; settling the requirement; filing one; the worktree and branch; what to read first |
| [document](references/document.md) | while building | the specification, the scenarios and their coverage exemptions, lessons, decision records, markdown and agent files |
| [publish](references/publish.md) | finishing | keeping the item true, the verify steps in order, and the continuous-integration traps |

## The shape of it

- **A Feature starts with its specification**, authored with no work item: the
  specification is the agreement and items are cut from its claims afterwards
  ([`spec-and-traceability`](../spec-and-traceability/SKILL.md)). **A bug, spike
  or chore starts with an item** — its trigger is an observation, not an
  agreement.
- **One item at a time, and taken before the first edit.** The item's status is
  what says it is taken; an item that cannot be finished and reviewed on its own
  is a decomposition defect rather than a small item.
- **The specification is the authority on content, the item on delivery state.**
  Neither restates the other.
- **Nothing is done until it is verified the way the project verifies it**, and
  a verification that was skipped is reported as skipped.

## Never add

- A second place for a claim, a status, or a rule this skill or its companion
  already holds.
- A phase skipped because the change feels small. The references are short; the
  defects they prevent are not.
