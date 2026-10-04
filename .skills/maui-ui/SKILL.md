---
name: build-maui-ui
description: Build the Transponder fleet dashboard in .NET MAUI with Maui.Markup C# markup and RxObject view models, binding only the DynamicData-bound collection so a live source swap needs no view change. Use for any UI work.
---

# Build the Transponder MAUI UI

The dashboard is the demo's whole visible surface: a live grid of tracked
items that an audience watches update in place. MAUI,
`CommunityToolkit.Maui.Markup` and `ReactiveMarbles.Mvvm` are decided
technologies ([`README.md`](../../README.md) § "Technology Decisions"); the
patterns below come from those packages' documentation and the sample code
already in `src/Gui`, and are open to change.

## Stack and wiring

- **C# markup**, not XAML, via `CommunityToolkit.Maui.Markup`.
  `UseMauiCommunityToolkitMarkup()` is already wired in
  [`MauiProgram`](../../src/Gui/MauiProgram.cs). Existing `.xaml` files are
  leftovers from the template, not a precedent to extend.
- **Pages take their view model by constructor** and set `BindingContext`
  before `InitializeComponent`, as
  [`MainPage`](../../src/Gui/MainPage.xaml.cs) does.
- **View models derive from `RxObject`** and use the `field`-keyword
  property form from
  [`DemoViewModel`](../../src/Transponder/Features/Demo/ViewModels/DemoViewModel.cs):

  ```csharp
  public string Count { get; set => RaiseAndSetIfChanged(ref field, value); } = "Click me";
  ```

  What belongs in one — and what does not — is [`mvvm`](../mvvm/SKILL.md).

- **Register pages and view models through `AddUserInterface`**
  ([`UserInterfaceBuilder`](../../src/Gui/Container/UserInterfaceBuilder.cs)),
  not ad-hoc in `MauiProgram`. Registration order in `MauiProgram` today:
  `UseMauiApp` → `UseMauiCommunityToolkitMarkup` → `AddAkkaHost` →
  `AddUserInterface` → `ConfigureFonts`.
- View models live under
  `src/Transponder/Features/<Area>/ViewModels` (`AGENTS.md`); the MAUI host
  and pages live in `src/Gui`.

## The dashboard surface

From [`README.md`](../../README.md) § "The app":

- A live grid, one row per tracked item, **updating in place** — rows must not
  flicker or re-create on every snapshot, or the headline point is lost.
- A search box and dropdown filters, driving an observable predicate.
- Column sorting the user chooses.
- A grouped view (origin country or category for aircraft; flag or type for
  vessels).
- Master-detail: the selected item in a detail pane.
- Summary counts and aggregates per group.
- A stale indicator for items that have stopped reporting.
- A source indicator and the swap control — the audience should be able to see
  which feed is live.
- Optional: a map view.

## The UI reads; it never drives

- Bind to the collection DynamicData populates — see
  [`dynamic-data-pipeline`](../dynamic-data-pipeline/SKILL.md). The view
  **never polls, never fetches, never mutates the cache.**
- User intent flows the other way as observables: the search text, the chosen
  comparer, the selected group become inputs to `Filter` and `Sort`, not
  imperative list surgery.
- Let `AutoRefresh` re-evaluate filters and sorts when item properties change
  rather than rebuilding a list in a handler.

## The swap test

The ships stretch goal is a live source swap mid-demo
([`hot-swap-source`](../hot-swap-source/SKILL.md)). So, for any UI change:

> **If flipping from planes to ships would require editing a view, the view
> knows too much.**

In practice:

- Views bind the abstract `TransportVehicle` plus a **source-supplied column
  and label description**. No `Aircraft` in a grid cell, no `as Vessel`.
- **The detail pane is the only place a subclass appears.** It is the one
  surface allowed to care whether it is showing a squawk code or an MMSI, and
  the one place a downcast is legitimate.
- A sort comparer or filter predicate is supplied by the source description
  too, so swapping sources swaps the available columns without touching markup.
- The grid, filters, sorts, groups and bindings are built once and survive the
  swap. Rebuilding them on swap would prove the opposite of what the talk
  claims.

## Testing

View models are plain classes and get unit tests — a view model takes its data
as an observable and its scheduler by constructor, so a test drives both. See
[`mvvm`](../mvvm/SKILL.md) "Testing" and
[`test-from-scenarios`](../test-from-scenarios/SKILL.md). There is no UI test
runner in this repo and none is planned for a demo.

## Never add

- XAML for new UI, or a markup file for something C# markup can express.
- A fetch, poll, timer or `HttpClient` call in a view or view model.
- A mutation of the DynamicData cache from the UI.
- A downcast or type check on `TransportVehicle` outside the detail pane.
- A view that must be edited to show a different source.
- Blocking in a binding or command path — no `.Result`, no `.Wait()`.
