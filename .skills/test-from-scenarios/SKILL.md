---
name: test-from-scenarios
description: Turn a specification claim into a failing xUnit test in test/UnitTests — GivenX_WhenY_ThenZ naming, synthetic data, injected schedulers for every time-based operator, and no network. Use when writing or reviewing tests.
---

# Tests from scenarios

Project companion for the `test-writer` role
([`.agents/test-writer.md`](../../.agents/test-writer.md)). That agent turns a
claim into a test that fails for the right reason; this file says what the
test looks like in this repo.

**Scenarios are documentation; the tests execute.** A Feature's `.feature`
file is the readable specification, and each xUnit test cites the `@B-00n`
claim it proves. **There is no Gherkin runner here** — no Reqnroll, no
bindings, no step definitions — and none is planned for a demo. So "a scenario
exists" never means a claim is covered: the § 9 Traceability Matrix row does,
and it points at a test
([`spec-and-traceability`](../spec-and-traceability/SKILL.md)).

## Where and what

- **xUnit**, in [`test/UnitTests`](../../test/UnitTests) — root namespace
  `Transponder.UnitTests`, `Xunit` is a global using in the project file.
- **`GivenX_WhenY_ThenZ` method names** (`AGENTS.md`). The name states the
  claim; a reader should not need the body to know what broke. Where the
  global `xunit` skill's `Condition_Action_Outcome` naming differs, this
  repository's form wins; its `// Given` / `// When` / `// Then` body comments
  and its local-setup rule apply unchanged.
- **AwesomeAssertions** for assertions and **NSubstitute** for test doubles —
  the stack the global `xunit` skill documents. Neither is in
  [`Directory.Packages.props`](../../Directory.Packages.props) yet; the first
  issue that writes a test adds them, and nothing in the repository asserts
  anything until then.
- **Set the system under test up inside the test**, in its `// Given`, not in a
  constructor or a field. No state shared between tests.
- `coverlet.collector` is referenced, so coverage is collectible. No coverage
  threshold is enforced and none is proposed for a demo.
- **A claim comes first.** The specification is the brief — see
  [`spec-and-traceability`](../spec-and-traceability/SKILL.md). A test that
  encodes a fact no claim states is a question for the specification author,
  not a decision to make in the test file.

## Time is injected, always

Half of what this demo teaches is time-based — poll intervals, staleness,
`ExpireAfter`, throttled search input. So:

- **Every time-based operator takes an injected scheduler.** A test advances
  it deliberately and asserts what happened.
- **Never `DateTime.UtcNow` inline.** The staleness clock is a dependency, in
  production as well as in tests — which is also what lets replay age items
  the same way live does ([`api-mock`](../api-mock/SKILL.md)).
- **No `Thread.Sleep`, no real delay, no retry-until-true.** If a test takes a
  second, it is reading the wrong clock.

## No network, ever

No test reaches `opensky-network.org`, `stream.aisstream.io`, or the backup
source. A test that needs a credential to pass is a test that will fail on
someone else's machine and on CI.

## Fixtures are synthetic

- Invented callsigns, MMSIs, positions, countries (`AGENTS.md`). Committed as
  JSON beside the tests that use them.
- A fixture for the OpenSky converter keeps the **positional array exactly as
  the wire sends it**, nulls included — surviving a sparse row is that
  converter's whole job.
- A rehearsal recording is operational data, not a fixture. Scrub before
  reuse.

## What is worth testing here

The demo is small; these are the parts where a bug would be invisible on
stage until it is not:

- **`EditDiff`**: two snapshots — one item added, one updated, one gone —
  asserting exactly three changes and no churn on the untouched rows.
- **The positional `JsonConverter`**: a full row, a sparse row, a row without
  `category` (no `extended=1`), and a padded callsign that must come out
  trimmed.
- **Staleness and expiry**: advance the scheduler past the threshold and
  assert the indicator, then removal.
- **The source swap**: swap the selector and assert the cache and
  subscriptions behave as
  [`hot-swap-source`](../hot-swap-source/SKILL.md) says — including that an
  in-flight result from the outgoing source never lands.
- **View models**: plain classes taking an observable and a scheduler.

## Never add

- A test that touches a network, a credential, or the wall clock.
- `Thread.Sleep` or a real delay.
- A weakened assertion to get to green. A test that cannot fail proves
  nothing.
- Production code written to pass your own test — that is the implementer's
  job ([`.agents/implementer.md`](../../.agents/implementer.md)).
- Real recorded traffic as a fixture without scrubbing it.
