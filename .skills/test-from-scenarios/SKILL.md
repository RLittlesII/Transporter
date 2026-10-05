---
name: test-from-scenarios
description: Turn a specification claim into a test that fails for the right reason — synthetic fixtures, an injected clock, no network, and an assertion that can actually fail. Use when writing or reviewing tests.
---

# Tests from scenarios

**Project rules.** A project may extend this skill with a companion skill that
names this one; its agent instructions (`AGENTS.md`) list it. Read both. The
companion holds the test stack, the runner, the naming, the project's
boundaries, and what its tests must cover, and wins where they differ.

## A claim comes first

**The specification is the brief.** A test that encodes a fact no claim states
is a question for the specification author, not a decision to make in the test
file ([`spec-and-traceability`](../spec-and-traceability/SKILL.md)).

Where scenarios are documentation rather than executable — no runner binds them
— **"a scenario exists" never means a claim is covered.** The traceability row
does, and it points at a test. Know which of the two the project is before
claiming coverage for anything.

- **A built claim counts only when its test passes in a run.** One that fails,
  is skipped at run time, or never runs — filtered out, or in a suite nobody
  runs — is not covered, whatever the specification says.
- A claim may be marked unbuilt while its test is still missing, naming the item
  that will build it. **A decision that supersedes a claim deletes its
  scenario**, in the change that records the decision; parking it leaves the
  repository stating something false.

## Write from the scenario, not the conversation

- A test that needs a fact the scenario does not state means the scenario is
  incomplete — amend it rather than encoding the fact in test code.
- **A test that reads a declaration is not a test.** Asserting over a type
  object, a member list, a parameter order or an assembly checks the shape of
  the code rather than anything it does. It breaks on a rename that changed no
  behaviour, it passes on a body that does the wrong thing, and when it fails
  it names a missing member instead of a broken rule — so a reader has to
  reconstruct what the rule was. Where a claim really is about structure, the
  mechanism is a compiler diagnostic; where the project has none yet, the
  coverage row says so rather than naming a test that asserts the shape.
- **A test that asserts nothing proves nothing.** Never point at another suite
  ("covered by the integration tests"). Prove it here, or remove the sentence.
- The name states the claim. A reader should not need the body to know what
  broke.
- **Set the system under test up inside the test.** No state shared between
  tests, and no setup hidden in a base class a reader has to go find.
- A claim proven only against an inner method does not prove the outer boundary
  calls it. When the claim describes what a request or a submission does, one
  test goes through that boundary.

## Time is injected, always

- **Every time-based operator takes an injected clock or scheduler.** A test
  advances it deliberately and asserts what happened.
- **Never read the ambient clock inline.** A staleness or expiry clock is a
  dependency in production as well as in tests — which is also what lets a
  recorded source age items the same way a live one does.
- **No sleeps, no real delays, no retry-until-true.** If a test takes a second,
  it is reading the wrong clock.

## No network, ever

- No test reaches a live provider. A test that needs a credential to pass is a
  test that will fail on someone else's machine and in continuous integration.
- Use the transport library's own test double rather than a hand-rolled one. It
  queues responses, simulates a timeout, and **asserts the request that was
  made** — which is the half a hand-rolled stub usually leaves untested.
- Know where that interception reaches. A faking mechanism that follows the
  logical call context does not follow a message handed to something processing
  on its own thread; test the plain class directly and give the concurrent part
  a stubbed dependency.

## Fixtures are synthetic

- Invented identifiers, positions and names, committed beside the tests that use
  them. Never real personal data.
- A fixture for a wire-format converter keeps the payload **exactly as the
  provider sends it**, nulls and all — surviving a sparse payload is that
  converter's whole job.
- **Operational data is not a fixture.** A recording captured from a live
  provider is scrubbed before it is reused as one.
- A rule over seeded data is tested against what the project's own seeding
  produces, loaded the way production loads it, never against a stand-in built
  by a factory. The two can differ in key, flags or wording, and a test on the
  stand-in proves nothing about the real one.

## What is worth testing

Pick the places where a bug would be invisible until it is expensive:

- **The differ** — the component that decides what changed between two sets.
  Two sets, one item added, one updated, one gone: assert exactly three changes
  and no churn on the untouched items.
- **The wire-format converter** — a full payload, a sparse one, one missing an
  optional section, and a padded field that must come out trimmed.
- **Anything derived from time** — advance the clock past the threshold and
  assert the state, then the removal.
- **A swap or substitution seam** — assert that a result from the outgoing
  implementation never lands after the switch.
- **Translators** — a class whose job is turning one shape into another is
  tested on the translation, not on what the far side did with it.

## Never add

- A test that touches a network, a credential, or the ambient clock.
- A sleep or a real delay.
- A weakened assertion to get to green. A test that cannot fail proves nothing.
- Production code written to pass your own test — that is the implementing
  role's job.
- Real recorded traffic as a fixture without scrubbing it.
