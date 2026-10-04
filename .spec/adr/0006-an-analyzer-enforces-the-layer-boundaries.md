---
title: "ADR-0006: An analyzer enforces the layer boundaries"
description: "The structural and boundary claims are proven by a Roslyn analyzer reporting at build time, not by reflection tests, because the claims are about references in method bodies and reflection cannot see them."
type: adr
---

# ADR-0006: An analyzer enforces the layer boundaries

**Status:** proposed

## Context

[ADR-0002](0002-contract-client-strategy-tracker.md) gives every layer one
responsibility and one name, and a third of this Feature's claims exist to keep
those layers apart. They are not claims about what the code computes; they are
claims about its shape — what may *name* what, and what a declaration or a
registration may look like:

- B-004 — no domain type, cache, strategy or view can name the positional row.
- B-045 — the envelope and the row are referenced only by the contract's
  implementation and the snapshot client.
- B-046 — the snapshot is referenced by the client, its cache and its
  projection, and by nothing downstream of that projection.
- B-047 — nothing downstream of `IFleetTracker` references a contract, a
  client, a cache, a snapshot or a concrete `ITrackerSource`.
- B-032, B-041 — the cache names no domain type; no view model names a
  strategy, a client, a cache or the decorator.
- B-006, B-007, B-037, B-048 — what the contract may name, how many classes may
  implement it and how, and what the per-type seam may not grow.
- B-002, B-005 — what the envelope may declare, and how many methods the
  contract may have.
- B-008 — what the container may resolve.
- B-014, B-030, B-031 — what the snapshot may declare, whether the cache is
  wrapped in a type of its own, and what lifetime it is registered with.
- B-010's second clause — what may produce the contract's double.

[`aircraft-source.feature`](../../features/aircraft-source/.spec/aircraft-source.feature)
phrases these as "when every reference to them is identified" and "no domain
type, cache, strategy, tracker or view can name either". Eighteen of the
fifty-two claims are of this kind — a third of the specification, and the third
that holds the rest apart — which is why the mechanism that proves
them is a decision rather than a detail.

Seven of those eighteen arrived late. B-002, B-005, B-008, B-010's second
clause, B-014, B-030 and B-031 were given xUnit tests when § 9 was written,
read this record as covering only the ids it had listed, and were moved here
after review rejected the first of them as brittle
([lesson 0006](../lessons/0006-a-row-is-not-a-reason-to-write-a-test.md)).

The constraint that forces the choice: **a reference lives in a method body,
and reflection cannot see method bodies.** `System.Reflection` reaches
signatures — parameter types, return types, field and property types, base
types, interfaces, accessibility, `sealed`, explicit implementation. It does
not reach a local variable, a `new`, a cast, a generic argument supplied at a
call site, or a type named only inside a method. A reflection test for B-045
would pass on a view model that constructs an envelope in a method and throws
it away, which is exactly the violation the claim exists to catch.

`0001` § Scoring already named this: the boundary claims are "the claims most
likely to be 'satisfied' by absence rather than by a test".

## Decision drivers

- A claim about references must be proven against references, not signatures.
- A boundary that is only tested is a boundary that is already crossed by the
  time anyone runs the suite.
- The repository is a conference talk's takeaway; the enforcement is part of
  what it demonstrates.
- No speculative generality — this is a small system with six layers and a
  handful of rules.

## Considered options

| Option | Summary | Why not |
| --- | --- | --- |
| Reflection tests in xUnit | Assert over loaded types in the test project. No new dependency, and the stack already mandated. | Cannot see method bodies, so B-045 – B-047 — the three claims this is most needed for — would be asserted over signatures and reported as covered. A § 9 row claiming a reference rule is proven when only its signatures are is the fake-gate failure [lesson 0002](../lessons/0002-metadata-about-a-rule-drifts-too.md) is about. |
| An architecture-rule library — NetArchTest, ArchUnitNET | A dependency-rule engine over compiled IL: "no type in `Model` may reference `Integrations.OpenSky.Contracts`" as one assertion that genuinely holds. | Reads IL, so it does see method bodies, and it would work. It reports at test time: the violation is already written and compiled before anything says so, and what fails is an assertion in another file rather than the line that broke the rule. It also answers only the dependency claims; B-007's explicit implementation and B-048's naming still need a second mechanism. |
| A Roslyn analyzer **(chosen)** | Diagnostics over the syntax and semantic model at compile time, shipped from this repository. | — |
| Code review | Write the rules down and enforce them by reading. | What the repository does today, and `domain-model` § "Never add" already lists the downcast rule. The swap breaks "one line at a time", which is precisely the failure review is worst at catching. |

## Decision

**A Roslyn analyzer reports the structural and boundary claims as compiler
diagnostics.** Each claim it carries gets one diagnostic id; a violation is a
build error, not a test failure.

1. **It sees what the claims are about.** An analyzer works on the semantic
   model, so a type named anywhere — a local, a `new`, a cast, a generic
   argument, a method body — is a reference it can report. This is the whole
   reason for the choice, and the only one that rules the alternatives out.
2. **It reports where the violation is written**, at the line that wrote it,
   while it is being written. A boundary enforced at test time is enforced
   after the fact: the code compiles, the editor stays quiet, and the first
   signal is a red assertion somewhere else that has to be read backwards to
   the reference that caused it.
3. **The repository already builds analyzers.** `Directory.Build.props`
   references `Rocket.Surgery.Airframe.CodeAnalysis` for every project, and
   `.editorconfig` already configures RSA diagnostics by severity — including
   the five it raises to `error`. An analyzer of our own is the same mechanism
   with our rules in it, configured the same way.
4. **Its diagnostics carry their own prefix** so they are never confused with
   the Airframe set, and each one names the claim it enforces in its
   description. A diagnostic that cannot say which claim it serves is a lint
   rule, not an enforcement of this specification.
5. **It proves itself by test, like anything else.** The analyzer's own tests
   are what § 9 names for the claims it carries, written the way every other
   test here is
   ([`test-from-scenarios`](../../.skills/test-from-scenarios/SKILL.md)).
6. **Only the claims that are about structure.** A claim about a computed value
   — a padded callsign, an absent category, a deferred poll — is an xUnit test
   and nothing changes for it. The analyzer is not a second place to assert
   behaviour.

**Two claims survive neither mechanism**, because they constrain code that does
not exist. B-049 says a push provider gets no contract layer and none is
invented for it; there is no push provider, so there is nothing for an analyzer
to look at and nothing for a test to load. B-009 says a new provider version
produces a new interface and an implemented one is never edited; OpenSky has
published no version, so the precondition cannot be arranged. Both are review
obligations on the change that creates the precondition, and § 9 records them
that way rather than naming something that would assert nothing.

## Consequences

- **The analyzer is work this specification does not contain.** It is its own
  Feature, with its own specification and items — not a task inside
  `0002` – `0007`. Until it exists, every claim it carries stands `Missing` in
  § 9, which is the honest state and is what blocks ship. § 11 carries the
  question of when it is scheduled.
- **The boundary claims stop depending on the order the code is written in.**
  `0001` § Scoring expected B-045 – B-047 to be unverifiable until both sides
  of each boundary existed. A diagnostic fires on the side that violates it, so
  the far side no longer has to exist first — which retires the "satisfied by
  absence" hazard rather than merely testing around it.
- **A diagnostic is a build error for everyone**, including a contributor who
  has not read this specification. That is the point, and it is also the cost:
  a rule written too broadly stops work that was never wrong, and the escape
  hatch is a severity override in `.editorconfig`, which is a visible edit
  rather than a silent suppression.
- **Two enforcement mechanisms now exist for one specification** — diagnostics
  and tests — and a reader has to know which proves what. § 9's Test column
  names the analyzer's test for the claims it carries, so the matrix stays the
  single place that answers it.
- **This is not the testing stack changing.** xUnit, AwesomeAssertions and
  NSubstitute remain what
  [`transponder-conventions`](../../.skills/transponder-conventions/SKILL.md)
  says they are, and the analyzer's own tests are ordinary tests. No
  architecture-rule library joins the repository, so nothing new appears in
  `Directory.Packages.props` for this decision beyond what the analyzer project
  itself needs.
