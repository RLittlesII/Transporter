---
name: clarify-requirements
description: Resolve genuinely material requirement ambiguity in a specification-driven repository. Use when two plausible interpretations would change user-visible behavior, data shape, the demo's stage reliability, or a provider's terms and limits.
---

# Clarify requirements

## First, look

Read [`README.md`](../../README.md) — the specification today — plus the
relevant source, tests, and the `.issues/` item. Most questions are already
answered there; the README carries the data source's limits, the resilience
plan, and the open items list.

## Decide or ask

- **Reversible and no behavior change?** Proceed on a documented local
  assumption. Routine implementation details are not product questions, and
  **this is a demo where almost nothing is adopted yet** — the shape of a class
  or the name of a folder is yours to choose.
- **Ask one concise question** only when the answer cannot be found and the
  choice would materially change:
    - what the audience sees, or whether the demo survives the venue network;
    - the data shape or the domain model's public surface;
    - credential handling, a provider's terms, or the credit budget;
    - whether the live source swap still works.

  State:
    - the evidence already available;
    - the two concrete interpretations;
    - the impact of each;
    - the smallest decision needed to continue.

## Write the answer back

In the same change:

- the answer becomes a claim, a scenario, a README edit, or an out-of-scope
  line, so the next run starts from the answer rather than re-asking;
- a design change updates the affected specification —
  [`spec-and-traceability`](../spec-and-traceability/SKILL.md) says where that
  lives and which role owns the section — and the `.issues/` item's acceptance
  criteria (see [`deliver-change`](../deliver-change/SKILL.md) "Keep the item
  true while you work").

Open questions this demo already owes answers to are listed in
[`spec-and-traceability`](../spec-and-traceability/SKILL.md). Check there
before asking; the question may already be known and simply undecided.
