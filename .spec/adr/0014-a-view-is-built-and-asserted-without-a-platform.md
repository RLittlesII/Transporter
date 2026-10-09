---
title: "ADR-0014: A view is built and asserted without a platform, and what a platform draws is read"
description: "Pages and design tokens move to a plain net10.0 library, so a test builds a page with no MAUI head and asserts its bindings and visual tree, and tokens are tested as data; nothing runs on a device or compares a screenshot, and the drawn result stays a review."
type: adr
---

# ADR-0014: A view is built and asserted without a platform, and what a platform draws is read

**Status:** proposed

## Context

`fleet-dashboard` decision 0002 puts cards, a trail map, one dark theme and a
status vocabulary told by icon and word on the page (`fleet-dashboard` B-029 –
B-038). Until now a view-model test was the whole of the UI's coverage
([`maui-ui`](../../.skills/maui-ui/SKILL.md) § "Testing"), and everything a view
does with what it is handed was a review (`fleet-dashboard` § 4 rows 4, 10 and
13). That was enough for a grid of four text columns. It is thin for a page
whose claims are mostly about what the view binds: that a status shows a word
and an icon, that a colour comes from a token, that a card is built from the
description and nothing else.

The pages cannot be reached from a test today. `src/Gui` targets
`net10.0-ios` and `net10.0-maccatalyst` on a Mac and plain `net10.0` on Linux,
where `.build/Build.cs` leaves it out of the build because there is no MAUI
head a Linux SDK can build (`fleet-dashboard` § 4 row 4,
[item `0018`](../../.issue/0018-linux-ci-build-scope.yml)). `test/UnitTests`
targets plain `net10.0`, so it can reference neither head.

`src/Transponder` does carry a reference to `Microsoft.Maui.Controls.Core`,
added with the Akka demo in `4e33882` and used by nothing in it: the assembly
and all 219 tests build and pass without it. It is a defect, and
[item `0076`](../../.issue/0076-no-maui-in-the-feature-assembly.yml) removes
it. It is also the one evidence on hand that the package restores and builds
on Linux CI with no workload.

A spike on 2026-10-08 built a C# markup page — a `ContentPage`, a bound `Label`, a
`CollectionView` with a `GridItemsLayout`, `CommunityToolkit.Maui.Markup`'s
`Bind` — inside an xUnit test on plain `net10.0`, against
`Microsoft.Maui.Controls.Core` 10.0.110 and `CommunityToolkit.Maui.Markup`
8.0.0, and asserted the label's text before and after a property change and its
colour. It passed. It needed one thing: a `BindableObject` throws on a property
change unless a dispatcher is registered, so the test registers one that runs
inline through `DispatcherProvider.SetCurrent`.

The person decided on 2026-10-08: tokens are tested as data, views are tested
built and unrendered, there is no macOS CI, and nothing tests the UI on a device.

## Decision drivers

- A claim about what a view binds is proven by something that runs, where it
  can be. A review is for what nothing here can run.
- No test may need a MAUI head, so CI stays on Linux (`fleet-dashboard` § 4
  row 4).
- B-002: views stay C# markup, so there is no XAML to load in a test.
- `src/Transponder` takes no dependency on MAUI. The model, the pipeline and
  the view models are plain .NET, and a page is the host's concern.

## Considered options

| Option                                         | Summary                                                                                                                                               | Why not                                                                                                                                                                                                                                                                       |
| ---------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A plain `net10.0` library for views and tokens | Pages, their parts and the design tokens move out of `src/Gui` into a library referencing the MAUI packages with no `UseMaui`; the heads reference it | Chosen.                                                                                                                                                                                                                                                                       |
| Add `net10.0` to `src/Gui`'s targets           | Microsoft's documented pattern for unit testing a MAUI app: a plain target beside the platform ones, `OutputType` set only for the platforms          | `UseMaui` on a plain target still asks for the MAUI workload, which a Linux SDK cannot install — the reason `src/Gui` is outside the Linux build. CI would need a Mac or the test would only run on one.                                                                      |
| Views in `src/Transponder`                     | The plain `net10.0` assembly that already builds on Linux holds the pages too                                                                         | `src/Transponder` takes no dependency on MAUI. The feature assembly would hold the host's markup, and a view model would share an assembly with the view it must not name, so the boundary `BoundaryAnalyzer` enforces (ADR-0006) would have to be drawn inside one assembly. |
| Tokens tested, views reviewed                  | Contrast and colour-vision checks over the token values; every view assertion stays a review                                                          | The person chose views tested as well. A status told by colour alone, or a hard-coded colour, is exactly what a reader misses on a page with many cards.                                                                                                                      |
| A device runner with screenshot comparison     | The app on a Mac Catalyst or iOS simulator against the replay source, screenshots compared with a reference set                                       | The person declined it: it needs macOS CI, and reference images and animation timing make it the most expensive and least stable check in the repository.                                                                                                                     |

## Decision

**Pages and design tokens live in a plain `net10.0` library**, referencing the
MAUI packages as packages — no `UseMaui`, no workload — and the heads in
`src/Gui` reference it. It is the only project outside `src/Gui` that
references MAUI. `src/Gui` keeps what only a head can hold:
`MauiProgram`, the container wiring, the platform folders and the bundled
resources. Two kinds of test follow, both in xUnit on Linux:

- **Tokens are data.** Colours, contrast ratios and colour-vision distinctness
  are arithmetic over the token values, asserted with no page built.
- **A view is built, never drawn.** A test constructs the page with its view
  model, registers an inline dispatcher, and asserts the visual tree: what is
  bound to what, that every status carries a word and an icon beside its
  colour, that a colour is read from a token, that the page built against two
  source descriptions is the same page — the swap test, run.

**What a platform draws stays a review**: pixels, the map's rendering, a font's
metrics, an animation's timing. There is no UI test runner, no device or
simulator test, no screenshot comparison, and no macOS CI. A review is recorded
in its § 9 row the way
[lesson 0011](../lessons/0011-a-review-nobody-performed-is-not-a-review-nobody-can-perform.md)
gives: what was looked at, on which item, and what change re-does it.

## Consequences

- Most of decision 0002's view claims become testable on the Linux build. The
  rendering halves of `fleet-dashboard` B-025, B-032, B-033 and B-034 stay
  reviews, and § 4 rows 4 and 13 say which half is which.
- A page is a plain class built in a test, so a view that reaches for a service,
  a platform API or `Application.Current` fails its own test before it fails on
  stage.
- `src/Gui` shrinks to a host. The project layout in `transponder-conventions`
  references/coding.md, and where a view test lives in references/testing.md,
  change when the library lands, not before.
- Every view test registers the inline dispatcher before it builds a page.
  It is the one double a view test needs that has nothing to do with the claim.
- `BoundaryAnalyzer` gains a project to read. Whether its rules about what a
  view may name apply there unchanged is `boundary-analyzer`'s to decide.
- A drawn regression — a map that renders nothing, a colour a platform
  overrides — is found by a reader or not at all. That is the cost of no macOS
  CI, accepted by the person.
