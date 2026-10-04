---
name: maui-ui
description: Build the UI in .NET MAUI with Maui.Markup C# markup and RxObject view models, binding only the reactive collection so a live source swap needs no view change. Use for any UI work.
---

# Build the MAUI UI

For MAUI and the markup library's own API, see
[CommunityToolkit/Maui.Markup](https://github.com/CommunityToolkit/Maui.Markup).
This file covers **how UI is wired here and what a view is allowed to know**.
What the UI is *for* — which surfaces exist, what each one shows — is the
specification's to say, not this file's.

## Stack and wiring

- **C# markup, not XAML**, via `CommunityToolkit.Maui.Markup`, wired with
  `UseMauiCommunityToolkitMarkup()`. A `.xaml` file left from a project template
  is not a precedent to extend.
- **Pages take their view model by constructor** and set `BindingContext` before
  `InitializeComponent`.
- **View models derive from `RxObject`** and use the `field`-keyword property
  form. What belongs in one — and what does not — is
  [`mvvm`](../mvvm/SKILL.md).
- **Register pages and view models through the container's user-interface
  builder block**, not ad-hoc in the host's startup
  ([`transponder-conventions`](../transponder-conventions/SKILL.md)).

## The UI reads; it never drives

- Bind to the collection the reactive pipeline populates
  ([`dynamic-data-pipeline`](../dynamic-data-pipeline/SKILL.md)). The view
  **never polls, never fetches, never mutates the cache.**
- User intent flows the other way as observables: the search text, the chosen
  comparer, the selected group become inputs to `Filter` and `Sort`, not
  imperative list surgery.
- Let `AutoRefresh` re-evaluate filters and sorts when item properties change
  rather than rebuilding a list in a handler.
- Rows update **in place**. A list that re-creates its items on every snapshot
  flickers, and loses the point of binding a changeset at all.

## The swap test

The live source swap ([`hot-swap-source`](../hot-swap-source/SKILL.md)) is the
constraint every UI change is read against:

> **If flipping to a different source would require editing a view, the view
> knows too much.**

In practice:

- Views bind the abstract domain type plus a **source-supplied column and label
  description** ([`domain-model`](../domain-model/SKILL.md)). No concrete
  subclass in a grid cell, no cast.
- **The detail pane is the only place a subclass appears**
  ([ADR-0005](../../.spec/adr/0005-an-abstract-base-carries-the-tracked-item.md)
  item 6). It is the one surface allowed to care which kind of item it is
  showing, and the one place a downcast is legitimate.
- A sort comparer or filter predicate is supplied by the source description too,
  so swapping sources swaps the available columns without touching markup.
- The grid, filters, sorts, groups and bindings are built once and survive the
  swap.

## Testing

View models are plain classes and get unit tests — a view model takes its data
as an observable and its scheduler by constructor, so a test drives both. See
[`mvvm`](../mvvm/SKILL.md) "Testing" and
[`test-from-scenarios`](../test-from-scenarios/SKILL.md). Where a project runs
no UI test runner, the view model test is the whole of the UI's coverage, which
is another reason to keep the view empty of logic.

## Never add

- XAML for new UI, or a markup file for something C# markup can express.
- A fetch, poll, timer or `HttpClient` call in a view or view model.
- A mutation of the cache from the UI.
- A downcast or type check on the domain base outside the detail pane.
- A view that must be edited to show a different source.
- Blocking in a binding or command path — no `.Result`, no `.Wait()`.
