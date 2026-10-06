---
title: "Lesson 0016: An untested assumption is not a decision"
description: "ADR-0003 chose Scrutor on a sentence about what Decorate<> does, and the sentence was wrong — found by the item that went to build it, after two specifications and two items had written rules against the behaviour nobody had run."
type: lesson
---

# Lesson 0016: An untested assumption is not a decision

**Date:** 2026-10-06
**Kind:** convention

## Symptom

`0006` was taken to build the source-swap decorator. Its first step was adding
`Scrutor` to `Directory.Packages.props`, which ADR-0003 named and which the
item's summary repeated. Before writing the decorator, the item ran Scrutor
7.0.0 against the registration shape the record described — two strategies
registered as `ITrackerSource`, then `Decorate<ITrackerSource, T>()`:

| Decorator constructor            | Result                                                                     |
| -------------------------------- | -------------------------------------------------------------------------- |
| `(ITrackerSource inner)`         | **Two** decorators, one per registration; the seam resolves the last one   |
| `(IEnumerable<ITrackerSource> )` | `InvalidOperationException: A suitable constructor … could not be located` |

ADR-0003 said `Decorate<>` "rewrites the prior registrations so the decorator
receives them". It wraps one registration at a time. The record's whole decision
rested on a thing the library does not do, and the shape it rejected
alternatives in favour of could not be built.

The item stopped before writing any decorator code, and the decision went back
to the record it came from.

## Root cause

The record was written the way a reader writes one: from the library's purpose
rather than from its behaviour. Decoration is Scrutor's subject, the registration
shape ADR-0002 wanted is decoration-shaped, and the sentence that joined them was
never executed. Nothing in the chain after it executed either — `aircraft-source`
§ 7 wrote the registration prose, `replay-source` § 4 row 12 ruled a class of
registration out because of the ordering hazard, and `0012` and `0006` both
carried that hazard in their risk rows. Four documents deep, and the first
executable statement about it was the one the implementing item wrote.

The hazard those documents recorded is real for Scrutor and was accurately
described. It is simply not a hazard of a design that could not have been built.

## Spec delta

- [ADR-0003](../adr/0003-a-factory-registers-the-swap-decorator.md) is rewritten
  — it is `proposed`, so it changes in place — around the evidence: a factory
  over the per-type seams, no package, and the ordering hazard retired.
- `replay-source` § 4 row 12 and `aircraft-source` § Scoring, which were written
  against the ordering hazard, are amended by the same change.
- `.skills/hot-swap-source` § "The mechanism" states the registration shape, so
  it is corrected there too.

## The rule

**A record that rests on what a library does names the run that showed it.** Not
the documentation, not the package's purpose, not a remembered API — the
smallest executable thing that distinguishes the behaviour being relied on from
the one being assumed, and what it printed. A decision that cannot cite one says
so in its Context, so the next reader knows which sentence to test first rather
than inheriting it as settled.

It is cheap where it matters: the probe that retired this assumption was twenty
lines, lived for one build, and was deleted with the branch that wrote it.

`coding-conventions` § "Stop on a gap" carries the rule, because the moment it
applies is the moment an implementer finds the gap — and the cost of not
stopping is the four documents above, each of which would have had to be
amended anyway, later, with code written against them.
