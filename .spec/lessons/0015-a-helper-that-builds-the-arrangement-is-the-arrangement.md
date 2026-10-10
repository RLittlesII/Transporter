---
title: "Lesson 0015: A helper that builds the arrangement is the arrangement"
description: "Lesson 0013's rule was read as being about doubles, so a Host() and a Settings() helper hid a composition test's whole Given a day after it was written."
type: lesson
---

# Lesson 0015: A helper that builds the arrangement is the arrangement

**Date:** 2026-10-06
**Found by:** the person, reviewing pull request 32 (`0047`)

## Symptom

`TransporterCompositionTests` carried two private helpers. `Host()` built the
`HostBuilder`, called `AddTransporter` and returned the host; `Settings()` built
the configuration it was called with. Every test read `using var host = Host();`
and said nothing about what the application had been composed over. The review:

> This should be configured IN the test.
>
> This should be passed in as Class or Member Data. Can we learn this lesson?!

The lesson it names is [0013](0013-a-fake-built-in-a-helper-hides-its-arrangement.md),
written the same day.

## Root cause

Lesson 0013's rule landed in `transporter-conventions` references/testing.md as
a sentence about doubles — "a fake is configured in the test that uses it" —
with a closing clause that a private helper "may still build **data** a case
needs". Neither helper here built a double, and configuration reads as data, so
the rule appeared to permit both. It does not: what those helpers built was the
test's **arrangement**, which is the thing the rule exists to keep visible. The
`// Given` had one line in it and the reader had to leave the test to learn
anything.

The second half is the same shape in the other direction. The one thing that
varied — which service of the composition is resolved — was not expressed as
variation at all: two tests, each resolving one type it named inline, where the
window needs five. `WindowServiceCases` is five rows, so a missing registration
fails the case that names it rather than reporting whichever service the
container reached first.

A rule written from one incident describes that incident. This one now says
what it is about.

## Spec delta

None. No claim changed; the review changed how `0047`'s test is written, not
what it proves.

## The rule

`transporter-conventions` references/testing.md § "A test that stands up a host"
now carries it: **the host is built in the test body** — the `HostBuilder`, the
configuration handed to it and the registration call are the `// Given`, never a
private `Host()` or `Settings()`. A helper may build one domain value a case
needs; the arrangement is not one. What varies is `[ClassData]`, as 0013 already
said.
