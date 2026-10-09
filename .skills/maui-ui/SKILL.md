---
name: maui-ui
description: Build the UI in .NET MAUI with Maui.Markup C# markup and RxObject view models, binding only the reactive collection so a live source swap needs no view change. Use for any UI work.
---

# Build the MAUI UI

For MAUI and the markup library's own API, see
[CommunityToolkit/Maui.Markup](https://github.com/CommunityToolkit/Maui.Markup).
This file covers **how UI is wired here and what a view is allowed to know**.
What the UI is _for_ — which surfaces exist, what each one shows — is the
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
- **The head registers only what is MAUI.** Everything else — the integration,
  the pipeline, the view models, the schedulers — is composed in one method a
  test can build a host from, and that test resolves the view model each page
  takes. What stays in the head is the settings it reads, its pages, and the
  thread that owns its window: no test project here can reference a MAUI head,
  so a registration left there is one nothing runs over
  ([lesson 0014](../../.spec/lessons/0014-a-composition-only-the-head-performs-is-one-nothing-proves.md)).

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
- **A view subscribes through an observable, never with `+=`** — any event,
  not only a view model's: a child control's `SizeChanged` or `SelectionChanged`
  is the same `+=`, and a layout event on the page's own child is no exception
  ([lesson 0004](../../src/Transponder/Features/Fleet/.spec/lessons/0004-two-written-rules-read-as-not-applying.md)).
  The page keeps a `CompositeDisposable`, and `OnHandlerChanged` disposes it
  when `Handler` is null. Where a view
  must react to a view-model property — rebuilding a column template from a new
  description, say — take the generated `Events()` observable over
  `PropertyChanged` (`ReactiveMarbles.ObservableEvents.SourceGenerator`),
  `.DisposeWith` the page's own disposables, and dispose them when the page's
  handler goes. A `+=` is never removed, so the handler holds the page for as
  long as the view model lives; nothing fails and nothing reports it, which is
  why it survives review unless the rule is written down.

## The swap test

The live source swap ([`hot-swap-source`](../hot-swap-source/SKILL.md)) is the
constraint every UI change is read against:

> **If flipping to a different source would require editing a view, the view
> knows too much.**

In practice:

- Views bind the abstract domain type plus a **source-supplied column and label
  description** ([`domain-model`](../domain-model/SKILL.md)). No concrete
  subclass in a grid cell, no cast.
- **No surface names a subclass, the detail pane included**
  ([ADR-0005](../../.spec/adr/0005-an-abstract-base-carries-the-tracked-item.md)
  item 6). A field only one kind of item reports reaches the pane as a line the
  source's description names, already in its display unit; the description is
  the one place a downcast is legitimate.
- A sort comparer or filter predicate is supplied by the source description too,
  so swapping sources swaps the available columns without touching markup.
- The grid, filters, sorts, groups and bindings are built once and survive the
  swap.

## Testing

View models are plain classes and get unit tests — a view model takes its data
as an observable and its scheduler by constructor, so a test drives both. See
[`mvvm`](../mvvm/SKILL.md) "Testing" and
[`test-from-scenarios`](../test-from-scenarios/SKILL.md).

A view is tested **built, never drawn**
([ADR-0014](../../.spec/adr/0014-a-view-is-built-and-asserted-without-a-platform.md)).
Pages live in a plain `net10.0` project, so a test constructs one with its view
model and reads the visual tree: what is bound to what, that a status carries a
word and an icon beside its colour, that a colour comes from a token, that the
page built against two source descriptions is the same page. A `BindableObject`
throws on a property change with no dispatcher, so the test registers one that
runs inline. Design tokens are tested as data — contrast and colour-vision
distinctness are arithmetic, with no page built.

What a platform draws — pixels, a map's rendering, an animation — is a review,
recorded in its matrix row. Keep the view empty of logic: anything it computes
is something only a reader can check.

## Never add

- XAML for new UI, or a markup file for something C# markup can express.
- A fetch, poll, timer or `HttpClient` call in a view or view model.
- A mutation of the cache from the UI.
- A downcast or type check on the domain base in a view or view model.
- A view that must be edited to show a different source.
- A UI test runner, a device or simulator test, or a screenshot comparison
  (ADR-0014).
- A page in a project a plain `net10.0` test cannot reference.
- An event handler attached with `+=`, or a subscription in a view with nothing
  that ends it.
- Blocking in a binding or command path — no `.Result`, no `.Wait()`.
