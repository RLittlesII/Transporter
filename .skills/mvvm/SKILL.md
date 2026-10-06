---
name: mvvm
description: Keep view models thin with ReactiveMarbles.Mvvm — take user input, hand it to an actor or the pipeline, project the result back for binding — with no business rules, I/O or domain logic of their own. Use when adding or changing a view model or a command.
---

# MVVM and view models

A view model does three things and nothing else:

1. **Take input from the user.**
2. **Hand it to the domain** — an actor where something must happen again later
   ([`akka-actor`](../akka-actor/SKILL.md)).
3. **Project what comes back** into properties the view binds to.

That is the whole job. A view model is a translator between a human and the
domain, not a place the domain lives. [`maui-ui`](../maui-ui/SKILL.md) owns the
markup and binding side; this file owns what sits behind it. For the library's
own API, see
[reactivemarbles/ReactiveMarbles.Mvvm](https://github.com/reactivemarbles/ReactiveMarbles.Mvvm).

## The shape

- Derive from `RxObject`, and use the `field`-keyword property form:

    ```csharp
    public string Count { get; set => RaiseAndSetIfChanged(ref field, value); } = "Click me";
    ```

- Take dependencies by constructor — `IActorRegistry`, an observable of domain
  state, a scheduler. Nothing is resolved from a static or a service locator.
- Live beside the actors they talk to, and are registered through the container's
  user-interface builder block
  ([`transponder-conventions`](../transponder-conventions/SKILL.md)).

## Thin means: input in, message out, projection back

**Belongs in a view model**

- The bound property surface, and the commands the view invokes.
- Turning a keystroke, a selection, or a tap into **one message or one
  observable value**.
- Projecting domain values into display form — a canonical unit into a display
  unit, an instant into local time, an empty `Option` into an empty string.
- View state: which row is selected, which group is expanded, which source the
  indicator shows.

**Does not belong in a view model**

- Business rules or domain invariants — those live on the domain types
  ([`domain-model`](../domain-model/SKILL.md)).
- `HttpClient`, a socket, a poll loop, a timer, a token refresh. Actors own time
  and failure.
- Filtering, sorting, grouping or expiry logic. The pipeline owns those
  ([`dynamic-data-pipeline`](../dynamic-data-pipeline/SKILL.md)); the view model
  only supplies the predicate or comparer the user chose.
- Any write to the cache.
- Mapping wire payloads ([`mapping`](../mapping/SKILL.md)).
- Staleness arithmetic. Derive it on the domain type, bind the result.

A useful test: **if a rule would still be true with no UI at all, it does not
belong in the view model.**

## Two kinds of input, two routes

This distinction is most of what keeps a view model thin:

| Input                                                               | Route                                                 | Why                                                                                               |
| ------------------------------------------------------------------- | ----------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| Continuous — search text, chosen comparer, selected group           | An **observable** feeding `Filter` / `Sort` / `Group` | A keystroke is not a domain event. Pushing each one through an actor adds a hop and buys nothing. |
| Discrete with an effect — swap the source, start, stop, refresh now | An **actor message**                                  | These change what the system is doing. An actor owns that, including failure and retry.           |

So a search box sets a property that a subject publishes; a swap button tells an
actor. Both end up influencing the same bound collection, and neither puts logic
in the view model.

## Talking to an actor

Resolve from `IActorRegistry` and prefer `Tell` for a command whose result
arrives as state:

```csharp
registry.Get<SourceActor>().Tell(new SwapSource(selected));
```

Use `Ask<T>` only for genuine request/response, and **always with an explicit
timeout**.

**Never continue off an `Ask` with `ContinueWith` and `.Result`.** On a faulted
or timed-out task it throws inside the continuation where nothing observes it,
and the property is then set from whatever thread the continuation landed on.
`await` the `Ask`, handle the timeout, and marshal the property set onto the UI
scheduler — or better, let the actor publish state and have the view model
observe it, so there is no continuation at all.

## Projecting state back

- Subscribe to the domain's observable, project, assign through
  `RaiseAndSetIfChanged`. One subscription per view model, **kept with
  `.DisposeWith(…)`** into the view model's own disposables, so the disposal is
  stated at the subscription rather than remembered at the bottom of the
  constructor.
- **Marshal to the UI scheduler at the view model boundary**, not deep inside
  the pipeline, and take the scheduler by constructor so a test can substitute
  it.
- **The pipeline publishes an observable; the view model binds it.** A domain
  object does not own a `ReadOnlyObservableCollection` — that type decides its
  consumer has a UI. The view model calls `ObserveOn(UserInterfaceThread)` then
  `Bind(out _rows)`, disposes that one subscription with itself, and exposes the
  collection it produced. One `Bind` per collection, and no copy of a collection
  anything else already holds: two collections of the same items is the bug this
  rule prevents
  ([ADR-0009](../../.spec/adr/0009-the-pipeline-publishes-changesets-a-consumer-binds.md)).
- A view model exposes the abstract domain type, not a subclass — the detail
  pane is the only exception ([`maui-ui`](../maui-ui/SKILL.md) "The swap test").

## Testing

A thin view model is a plain class, so it tests without a UI:

- Substitute `IActorRegistry` or use a real `TestKit` probe, and assert **the
  message that was sent** — a view model's job is the translation, so the
  translation is what a test checks.
- Feed a controlled observable in and assert the projected properties out.
- Inject the scheduler and advance it; never wait on real time
  ([`test-from-scenarios`](../test-from-scenarios/SKILL.md)).

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
