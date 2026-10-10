---
title: "Lesson 0003: A review is a convention nobody wrote down"
description: "Fifteen review comments on 0004's tests, and twelve of them were one convention each that the repository held nowhere — so the tests were written to a reasonable guess instead, and every later item would have guessed again."
type: lesson
---

# Lesson 0003: A review is a convention nobody wrote down

**Date:** 2026-10-05
**Kind:** process

## Symptom

Pull request #18 came back with fifteen comments. One was praise and two were
design calls. The other twelve were each a **convention this repository already
had an opinion about and had written down nowhere**:

- Prose belongs in an XML doc comment on a test, not in a paragraph above
  `// Given` (six comments, across four files).
- A unit test reads no files; a payload is a static field holding raw JSON.
- Parsing a payload is encapsulated once — an `IJson` with a default
  implementation — rather than a `Deserialize` helper per test class.
- Building a payload is encapsulated too, rather than a private string builder
  per test class.
- Varying data goes through xUnit's class or member data pattern, not a pile of
  `[InlineData]` attributes, and a static class named `...TestData` that is
  really a helper is misnamed.
- A collaborator the application composes — the scheduler provider — is built by
  a fixture and handed in whole, not substituted member by member.
- A test that stands up a host stands up the application's own composition, so
  there is one of it rather than two to keep in step.

None of these is a matter of taste once stated, and `transponder-conventions`
§ testing — the companion skill whose whole job is this — carried none of them.
It had the test stack, the naming scheme, the generated-fixture rule and the
`InternalsVisibleTo` rule, and stopped there.

## Root cause

**The skill recorded what to use and not how to use it.** Every rule above is
the second kind, and the first kind is what gets written down because it is what
gets noticed when it is missing: a test cannot compile without the assertion
library, so the assertion library is in the skill. A test compiles fine with its
payloads on disk, so nothing forced the question until a reader asked it.

Two of the twelve are worth separating out, because they are not stylistic at
all and the review caught real duplication:

- The startup test built its own `HostBuilder` graph with `Configure<T>` calls
  beside `AddOpenSky`. That is a second composition of the integration, and the
  first production scenario it diverged from would have passed. `AddOpenSky` now
  takes `IConfiguration` and does the whole wiring, so an application and a test
  differ only in what configuration they supply.
- `SchedulerProvider` read `CurrentThreadScheduler` and `TaskPoolScheduler`
  inside its members, which left a test substituting `ISchedulerProvider` rather
  than building the real type. Both schedulers now arrive by constructor, chosen
  once at the composition root.

A third signal, in hindsight: the first draft of the tests needed a
hand-written helper in **three** test classes to do the same JSON read. Three
copies of one behaviour is the shape of a missing abstraction, and it was
visible before review.

## Spec delta

§ 8's Fixtures row said a fixture is "a JSON array … two files rather than two
code paths". That is now two static payloads rather than two files, and the row
says so. § 7 gains `SchedulerProvider`'s constructor and `AddOpenSky`'s
configuration parameter, and `OpenSkyOptions` gains `BaseUrl` — defaulted,
because the URL is the provider's and nobody here chooses it, unlike the box
B-050 leaves undefaulted. No claim changed and no § 9 row moved: every claim
the item delivers is proven by the test § 9 names, under the same name.

## Process delta

[`transponder-conventions`](../../../../../../.skills/transponder-conventions/references/testing.md)
§ testing now carries all twelve as rules, in the place a test writer reads
before writing rather than after. The ones that generalise past this repository —
a test reads no files, and a test stands up the application's own composition —
are written there rather than in
[`test-from-scenarios`](../../../../../../.skills/test-from-scenarios/SKILL.md) only
because the companion is where this project's test stack already lives.

## Claim

- No claim changed. The delta is § 8's Fixtures row and § 7's declarations; the
  conventions are the skill's.
