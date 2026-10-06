---
name: coding-conventions
description: Conventions for any code, test, documentation or diagram change in a specification-driven repository — orient first, stop on a gap, keep the design direct, put a rule where it runs, and keep a skill to the rule. Use for any change.
---

# Coding conventions

**Project rules.** A project may extend this skill with a companion skill that
names this one; its agent instructions (`AGENTS.md`) list it. Read both. The
companion holds the project's language, framework, paths and naming specifics,
and wins where they differ.

## Before implementing

- **Orient**, even when the task looks small or familiar — drift comes from
  skipping this, not from the change itself:
    - the project's code graph or index, when it has one;
    - the specification, the affected pages, and the area's **out of scope**
      section;
    - every architecture decision record that bears on the change;
    - the project's lessons.
- When they conflict, source and tests are current state; decision records and
  work items are history.
- **Stop on a gap.** If the reading leaves a decision unsettled, or two sources
  in tension, ask before proceeding — even under a "spike" or "just get it
  working" framing. Name the gap, the options, and your recommendation. Never
  resolve it by assumption or by picking the easiest option to build.
- **Run the assumption a decision rests on, before building on it.** Where a
  record relies on what a library, a container or a generator does, the first
  step of the item that builds it is the smallest executable thing that tells
  the relied-on behaviour apart from the assumed one — then the record names
  that run, or says it has none. A wrong sentence about a dependency reaches
  specifications, items and risk rows long before it reaches a compiler
  ([lesson 0016](../../.spec/lessons/0016-an-untested-assumption-is-not-a-decision.md)).
- **Specify first.** Say what is out of scope for the area you touched, not only
  what you built.

## Design

- Keep the implementation direct. Use plain code until a real external boundary
  or a second implementation makes an abstraction useful.
- Add an interface only at a real external boundary, or when two implementations
  already need a shared contract.
- When a seam is justified, use SOLID and named Gang-of-Four patterns as the
  vocabulary. The seam earns the pattern; naming a pattern never earns the seam.
- Speculative extensibility is a cost paid now for a requirement that may never
  arrive. A member nothing overrides and a layer nothing varies are both it.
- **A member that touches a file, a socket or a database is asynchronous, and
  its seam says so.** The decision is the interface's, not the
  implementation's: a synchronous signature over real input or output forces
  every implementation to block a thread, and the caller that is already
  asynchronous cannot tell that it is. Declaring it the other way costs nothing
  where there is nothing to await — a null object returns a completed task.
- **A record written a field at a time is a record another writer can split.**
  Compose the whole line, row or message and write it in one call. Two writes
  in flight interleaving is the kind of defect that appears under load, in a
  file nobody reads until the demo, and never in a test.

## Naming and layout

- **The configuration the build reads is the authority.** Formatter and
  analyzer settings outrank any prose about naming or layout, including a
  skill's: they are what the compiler enforces. The companion names the file.
- A naming rule carrying warning severity is a rule, not a preference.
- A suppression carries a reason beside it saying why the rule does not apply
  here. One without is indistinguishable from an accident.

## Dependencies

- **A dependency's version is declared in one place for the whole repository.**
  An individual project file naming its own version is the drift that central
  declaration exists to prevent.
- Adding a dependency means the version declaration and the reference that
  needs it, in the same change.

## Generated files and guards

- Never hand-edit a generated file. If the output is wrong, the input or the
  generator is wrong.
- **Put a rule where it runs, not only where it is checked.** A convention
  enforced by a continuous-integration flag holds only there; a local run, an
  editor run, or another agent's session escapes it. Enforce it in code, a hook,
  or the tool that owns the artifact; continuous integration is the backstop.
- **A committed generated file merges the way its sources do.**
    - Every line derives from one source item; no whole-tree totals or counts.
    - Independent items are sorted by a stable key and separated by unchanged
      lines, so a merge of two correct copies is correct.
    - A count belongs in the generator's output or a job summary.

## Documentation and diagrams

- Every tracked markdown file declares frontmatter saying what it is; the
  companion names the keys and the permitted types.
- Draw every diagram in the one notation the project chose, inline in the
  markdown that needs it.
- Test and example data is synthetic. Never real user content.

## Skills

A skill is one of three things, and never a mixture: a **method** skill, which
is portable to another repository; a **technology** skill, about a library or
tool; or the **project companion**, which holds what is specific to this
repository under the section names of the method skill it extends.

> A skill holds the rule, the trap, and the `Never add` list. Facts live where
> they are authoritative — the specification, a decision record, or the
> library's own documentation — and the skill links them. Four things never go
> in a skill: a fact restated from somewhere else in the repository, the
> current state of the repository, a project path or command in a skill that is
> not the project companion, and the reason the product wants the rule.

- **A fact belongs to one file.** A skill repeating a specification, a decision
  record or a provider's documentation is a second place for it to drift from,
  and a reader who finds both has no way to tell which is current.
- **Removing a copy is half a move.** Before deleting content because another
  document owns it, open that document and find the content in it. A commit
  message naming the owner is not a transfer, and a cite is not a copy. What
  survives a dedupe is whatever the keeper actually says — and a document whose
  rules name no particulars reads exactly like one with no particulars to name,
  so nothing downstream reports the loss.
- **The current state of the repository is not a rule.** "There is no test
  target yet", "that package is not referenced yet", "this shape is
  provisional" are all true until the next change lands, and silently false
  afterwards. State the rule; let the reader read the repository.
- Cross-link to the skill that owns a neighbouring rule rather than restating
  it. A rule restated twice is a rule that will be corrected once.

## Before finishing

- Run the narrowest relevant checks.
- Inspect the diff for unrelated changes.
- Update the specification whenever the target design changes.

## Never add

- A hand edit to a generated file.
- A dependency version pinned in a single project file.
- A suppression with no reason beside it.
- A blocking wait on an asynchronous call.
- A fact in a skill that is authoritative somewhere else.
