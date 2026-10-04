---
name: clarify-requirements
description: Resolve genuinely material requirement ambiguity in a specification-driven repository. Use when two plausible readings would change user-visible behavior, the data shape, a public surface, or a provider's terms.
---

# Clarify requirements

**Project rules.** A project may extend this skill with a companion skill that
names this one; its agent instructions (`AGENTS.md`) list it. Read both. The
companion holds the specification's location, the triggers that are material in
this project, and where an answer lands, and wins where they differ.

## First, look

Read the specification, the relevant source and tests, and the work item. Most
questions are already answered there, and a question already known and simply
undecided is recorded as an open question rather than asked again.

## Decide or ask

- **Reversible and no behavior change?** Proceed on a documented local
  assumption. Routine implementation detail is not a product question: the shape
  of a class or the name of a folder is yours to choose, and the less of the
  design is adopted, the more of it is.
- **Ask one concise question** only when the answer cannot be found and the
  choice would materially change:
    - what a user sees, or whether the system survives a failure it is supposed
      to survive;
    - the data shape, or a public surface other code is written against;
    - credential handling, a provider's terms, or a metered budget;
    - a guarantee the design exists to make.

  State:
    - the evidence already available;
    - the two concrete readings;
    - the impact of each;
    - the smallest decision needed to continue.

- **Never resolve a material ambiguity by assumption**, or by picking whichever
  reading is easiest to build, even under a "spike" or "just get it working"
  framing.

## Write the answer back

In the same change, so the next run starts from the answer rather than
re-asking:

- the answer becomes a claim, a scenario, a specification edit, or an
  out-of-scope line;
- **the specification is where it lands first.** A Feature is specified before
  any item exists, so the agreement sections are the record; an item's decisions
  section is for a call made after the item was filed, not a substitute for the
  agreement ([`spec-and-traceability`](../spec-and-traceability/SKILL.md));
- a design change updates the affected specification section — owned by one role
  — and the item's acceptance criteria
  ([`deliver-change`](../deliver-change/SKILL.md) "Keep the item true while you
  work").

An answer that lives only in the conversation was not recorded.
