---
title: "ADR-0016: Views and tokens stay in the head, and what a view binds is read"
description: "Pages, their parts and the design tokens stay in src/Gui rather than moving to a plain net10.0 library, so no test builds a page or reads a token; what a view binds and the tokens' contrast are reviews recorded in the matrix."
type: adr
---

# ADR-0016: Views and tokens stay in the head, and what a view binds is read

**Status:** accepted

**Accepted 2026-10-09.** The person's call, made while
[item `0075`](../../.issue/0075-views-in-a-plain-library.yml) was being planned.

Supersedes
[ADR-0014](0014-a-view-is-built-and-asserted-without-a-platform.md), whose
decision was a plain `net10.0` library for the pages and tokens and two kinds of
test over it. What of that record still holds is listed under Decision.

## Context

ADR-0014 moved the pages, their parts and the design tokens out of `src/Gui`
into a library a test could reference, and `0075` was cut to make the move. It
was never made. In the day between, `0069`, `0070` and `0073` built the theme,
the cards and the readout's pulse in `src/Gui`, where the app runs them.

Planning the move found one thing ADR-0014's spike did not meet.
[`Motion`](../../src/Gui/Components/Motion.cs) reads
`UIKit.UIAccessibility.IsReduceMotionEnabled` under `#if IOS || MACCATALYST`,
and `Readout` calls it before it animates (`fleet-dashboard` B-038). In a plain
`net10.0` library that condition is never true, so the file would compile and
answer `false` on stage. Keeping the claim would need a seam between the library
and the head that exists for no reason but the move.

The person decided on 2026-10-09: it does not need to be a library, and
everything lives in `src/Gui`.

## Decision drivers

- The fewest projects that hold the demo. A second MAUI-referencing project is
  one more thing a reader of the codebase has to place.
- No test may need a MAUI head, so CI stays on Linux (`fleet-dashboard` § 4
  row 4).
- A view holds no logic. What it would be tested for is decided in
  `src/Transporter`, where a test already reaches it.

## Considered options

| Option                                                 | Summary                                                                                                                         | Why not                                                                                                                                                                            |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Views and tokens stay in `src/Gui`                     | Nothing moves. What a view binds and what the tokens measure are reviews                                                        | Chosen.                                                                                                                                                                            |
| A plain `net10.0` library for views and tokens         | ADR-0014's decision: a library referencing the MAUI packages with no `UseMaui`, referenced by the heads and by `test/UnitTests` | The person declined it. It adds a project and a seam for `Motion`, to test views that are kept empty of logic.                                                                     |
| Compile `src/Gui`'s view sources into `test/UnitTests` | The test project links the view and token files and references the MAUI packages, so the tests exist and no project is added    | One file compiled twice, under two sets of symbols: `#if IOS \|\| MACCATALYST` reads one way in the head and the other in the test, so the test proves a build the app never runs. |
| Add `net10.0` to `src/Gui`'s targets                   | A plain target beside the platform ones, as ADR-0014 considered                                                                 | `UseMaui` on a plain target still asks for the MAUI workload, which a Linux SDK cannot install.                                                                                    |

## Decision

**Pages, their parts and the design tokens stay in `src/Gui`.** There is no
views library, and `test/UnitTests` references no MAUI package. No test builds
a page, and no test reads a token.

**What a view binds is a review**, and so is what the tokens measure: that a
status carries a word and an icon beside its colour, that a colour is read from
a token, the contrast ratios and colour-vision distinctness. A review is
recorded in its § 9 row the way
[lesson 0011](../lessons/0011-a-review-nobody-performed-is-not-a-review-nobody-can-perform.md)
gives: what was looked at, on which item, and what change re-does it.

**What a view would decide is decided in `src/Transporter` and tested there** —
a card's status, whether a readout pulses, the text a card shows — as
`FleetCardStatus`, `FleetCardMotion` and `FleetCardText` already are.

Still holding from
[ADR-0014](0014-a-view-is-built-and-asserted-without-a-platform.md):

- There is no UI test runner, no device or simulator test, no screenshot
  comparison, and no macOS CI.
- What a platform draws — pixels, a map's rendering, a font's metrics, an
  animation's timing — is a review.
- `src/Transporter` takes no dependency on MAUI
  ([item `0076`](../../.issue/0076-no-maui-in-the-feature-assembly.yml)).

## Consequences

- `0075` closes with nothing built, and `0069` no longer waits on it.
- The view claims of `fleet-dashboard` decision 0002 keep the mechanism their
  § 9 rows already name: a test over a static or a view model for the half that
  is a value, a review for the half that is a view.
- A view that reaches for a service, a platform API or `Application.Current`
  is found by a reader or on stage, not by a test.
- A token changed to a colour that fails contrast is found when the review is
  re-done, and the review is re-done only if the change's author knows it is
  owed. B-033's § 9 row says which change that is.
- `Motion` stays where the platform is, with no seam.
- `transporter-conventions` references/coding.md and references/testing.md do
  not change: they were to change when the library landed.
