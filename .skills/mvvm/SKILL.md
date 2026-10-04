---
name: mvvm
description: Keep Transponder view models thin — take user input, hand it to an actor or the pipeline, project the result back for binding — with no business rules, I/O, or domain logic of their own. Use when adding or changing a view model or a command.
---

# MVVM and view models

A view model in this application does three things and nothing else:

1. **Take input from the user.**
2. **Hand it to the domain** — preferably an Akka actor
   ([`akka-actor`](../akka-actor/SKILL.md)), since that is the model this demo
   is built on.
3. **Project what comes back** into properties the view binds to.

That is the whole job. A view model is a translator between a human and the
domain, not a place the domain lives.
[`build-maui-ui`](../build-maui-ui/SKILL.md) owns the markup and binding side;
this file owns what sits behind it.

`ReactiveMarbles.Mvvm` is a decided technology
([`README.md`](../../README.md) § "Technology Decisions"). The shapes below
come from its documentation and the sample
[`DemoViewModel`](../../src/Transponder/Features/Demo/ViewModels/DemoViewModel.cs),
and are open to change.

## The shape

- Derive from `RxObject`, and use the `field`-keyword property form:

  ```csharp
  public string Count { get; set => RaiseAndSetIfChanged(ref field, value); } = "Click me";
  ```

- Take dependencies by constructor — `IActorRegistry`, an observable of
  domain state, a scheduler. Nothing is resolved from a static or a service
  locator.
- Live under `src/Transponder/Features/<Area>/ViewModels` (`AGENTS.md`),
  beside the actors they talk to.
- Registered through `AddUserInterface`
  ([`UserInterfaceBuilder`](../../src/Gui/Container/UserInterfaceBuilder.cs)),
  resolved into the page's constructor
  ([`MainPage`](../../src/Gui/MainPage.xaml.cs)).

## Thin means: input in, message out, projection back

**Belongs in a view model**

- The bound property surface, and commands the view invokes.
- Turning a keystroke, a selection, or a tap into **one message or one
  observable value**.
- Projecting domain values into display form — metres to feet, an instant to
  local time, an `Option` with no value to an empty string.
- Selection and view state: which row is selected, which group is expanded,
  which source the indicator shows.

**Does not belong in a view model**

- Business rules or domain invariants. Those live on `TransportVehicle` and
  its subclasses
  ([`transponder-domain-model`](../transponder-domain-model/SKILL.md)).
- `HttpClient`, a WebSocket, a poll loop, a timer, a token refresh — actors
  own time and failure.
- Filtering, sorting, grouping or expiry logic. The pipeline owns those
  ([`dynamic-data-pipeline`](../dynamic-data-pipeline/SKILL.md)); the view
  model only supplies the predicate or comparer the user chose.
- Any write to the cache.
- Mapping wire payloads ([`mapping`](../mapping/SKILL.md)).
- Staleness arithmetic. Derive it on the domain base, bind the result.

A useful test: **if a rule would still be true with no UI at all, it does not
belong in the view model.**

## Two kinds of input, two routes

This distinction is most of what keeps a view model thin:

| Input | Route | Why |
|---|---|---|
| Continuous — search text, chosen comparer, selected group | An **observable** feeding `Filter` / `Sort` / `Group` | A keystroke is not a domain event. Pushing each one through an actor adds a hop and buys nothing. |
| Discrete with an effect — swap the source, start, stop, refresh now | An **actor message** | These change what the system is doing. An actor owns that, including failure and retry. |

So the search box sets a property that a subject publishes; the swap button
tells an actor. Both end up influencing the same bound collection, and
neither puts logic in the view model.

## Talking to an actor

Resolve from `IActorRegistry` and prefer `Tell` for a command whose result
arrives as state:

```csharp
registry.Get<SourceActor>().Tell(new SwapSource(selected));
```

Use `Ask<T>` only for genuine request/response, and **always with an explicit
timeout** — the sample uses five seconds. An `Ask` with no timeout is a hang
waiting for a bad network.

**The sample's continuation is not the pattern to copy.**
`DemoViewModel.ExecuteClick` does:

```csharp
registry.Get<ClickActor>().Ask<string>(Click.Instance, TimeSpan.FromSeconds(5))
        .ContinueWith(click => Count = click.Result);
```

It is fine for a click counter and wrong for anything real: `.Result` on a
faulted or timed-out task throws inside the continuation where nothing
observes it, and the property is set from whatever thread the continuation
landed on. For new code, `await` the `Ask`, handle the timeout, and marshal the
property set onto the UI scheduler — or better, let the actor publish state and
have the view model observe it, so there is no continuation at all.

## Projecting state back

- Subscribe to the domain's observable, project, assign through
  `RaiseAndSetIfChanged`. One subscription per view model, disposed with it.
- **Marshal to the UI scheduler at the view model boundary**, not deep inside
  the pipeline, and take the scheduler by constructor so a test can substitute
  it.
- The grid binds the pipeline's collection directly; the view model does not
  copy it into a list of its own. Two collections of the same items is the
  bug this rule prevents.
- A view model exposes the abstract `TransportVehicle`, not a subclass — the
  detail pane is the only exception
  ([`build-maui-ui`](../build-maui-ui/SKILL.md) "The swap test").

## Testing

A thin view model is a plain class, so it tests without a UI:

- Substitute `IActorRegistry` (NSubstitute) or use a real `TestKit` probe, and
  assert **the message that was sent** — a view model's job is the translation,
  so the translation is what a test checks.
- Feed a controlled observable in and assert the projected properties out.
- Inject the scheduler and advance it; never wait on real time.
- xUnit, `GivenX_WhenY_ThenZ`, AwesomeAssertions — see
  [`test-from-scenarios`](../test-from-scenarios/SKILL.md).

If a view model needs a page, a dispatcher it did not receive, or a network to
be tested, it is doing something that belongs elsewhere.

## Never add

- A domain rule, validation, or calculation that is not display formatting.
- `HttpClient`, a socket, a timer, or a poll in a view model.
- A cache write from a view model.
- A blocking call — no `.Result`, no `.Wait()`, no `GetAwaiter().GetResult()`.
- An `Ask` without a timeout, or a `ContinueWith` that reads `.Result`.
- A second copy of the bound collection.
- A static or service-locator lookup instead of a constructor dependency.
- A view model that knows which source is live beyond what it displays.
