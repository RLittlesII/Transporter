---
title: "ADR-0009: The pipeline publishes changesets; a consumer binds them"
description: "IFleetTracker exposes observables of changesets rather than bound collections: the operators stay in the pipeline, the chain is shared by DynamicData's cache-aware RefCount, and Bind plus the marshal to the UI scheduler happen at the view model boundary."
type: adr
---

# ADR-0009: The pipeline publishes changesets; a consumer binds them

**Status:** proposed

## Context

[`src/Transporter/Tracking`](../../src/Transporter/Tracking/.spec/README.md) § 7
declared `IFleetTracker` with two `ReadOnlyObservableCollection<T>` properties —
the fleet and the groups — alongside two `IObservable<T>` streams. Its § 11 row
4 held every declaration open for review, and concern 1 was that a consumer
cannot tell from the interface which members are safe to read off the UI thread:
a bound collection is UI-affine state, a stream is not, and the type says
nothing about which is which.

Reading that concern, the person raised the question the concern was a symptom
of. An object owning a `ReadOnlyObservableCollection` has already decided, on
its consumer's behalf, that there is a UI and that its collection is the one the
UI binds. `Bind` is a projection into a consumer's own state, and the projection
is the one part of the chain the consumer genuinely owns.

[`mvvm`](../../.skills/mvvm/SKILL.md) § "Projecting state back" had already
written the rule down: **"Marshal to the UI scheduler at the view model
boundary, not deep inside the pipeline."** `fleet-pipeline` B-005, as written,
required the opposite — the pipeline scheduling the work a view observes. The
skill and the claim were in conflict, and nothing had been built against either,
which is the cheapest moment to find it.

The same section's next bullet said "the view binds the pipeline's collection
directly; the view model does not copy it into a list of its own." That bullet
assumes the pipeline has a collection. It is the rule this record changes, and
the reason it exists — two collections of the same items — is satisfied by a
different mechanism here: there is one `Bind`, and it is the consumer's.

## Decision drivers

- A type should state its own thread affinity. A stream makes no claim about a
  UI; a bound collection makes several.
- The operators are what the talk is about, and § 1 promises them assembled in
  one readable place. Moving them into a view model would scatter them.
- Two subscribers must not mean two diff passes. The headline claim is that the
  snapshot-to-changeset step happens once.
- Every stage must stay provable with no view model, no page and no dispatcher.
- `mvvm`'s existing rule about where marshalling happens wins over a claim
  written against it.

## Considered options

| Option                                                                | Summary                                                                                                                                                                               | Why not                                                                                                                                                                                                                                                                                        |
| --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| The pipeline publishes changesets and the consumer binds **(chosen)** | Every member of the pipeline's surface is an observable. The operators — filter, sort, stale mark, group, aggregates — stay in the pipeline, which ends one operator short of `Bind`. | —                                                                                                                                                                                                                                                                                              |
| The pipeline owns the bound collections, as § 7 first declared        | One `Bind`, in the pipeline, and a consumer binds the property with no Rx at all.                                                                                                     | Puts UI-affine state behind a seam that also publishes streams, which is concern 1; contradicts `mvvm`'s marshalling rule; and decides for every future consumer that a `ReadOnlyObservableCollection` is the shape it wants — a background consumer, a test, or an export has no use for one. |
| Split the interface by thread affinity, keeping the collections       | Two interfaces: the bound collections on one, the streams on the other, so the type states the affinity.                                                                              | Answers the symptom and keeps the cause. The pipeline still owns a collection it cannot know anyone wants, and two registrations now describe one object.                                                                                                                                      |
| Four `IObservable<T>` constructor parameters for the inputs           | The inputs arrive as streams the consumer publishes and the constructor takes, with no interface between.                                                                             | Replaced by the methods above while this record was still `proposed`: it leaves B-007's and B-017's defaults to each caller's `StartWith`, and makes every caller — including every test — construct subjects to say "filter by this".                                                         |
| Move the operators into the view model too                            | The tracker publishes the seam's changesets; the view model filters, sorts, groups, counts and binds, so the audience reads the whole chain where they would write it.                | The operator claims (B-006 – B-015) would move to `fleet-dashboard`, § 1's "assembled in one readable place" would be false, and no stage would be provable without a view model. Rejected explicitly when the pivot's scope was put as a question.                                            |

## Decision

`IFleetTracker` publishes observables and owns no bound collection.

1. **The fleet and the groups are changeset streams.** The bound element of the
   fleet stream carries the vehicle **and** its derived stale mark, so a
   consumer binds one collection and reads the mark off the row it already has.
2. **The operators stay in the pipeline.** Filter, the stale mark, group and
   the aggregates are assembled once, in the tracker's constructor, from
   observable inputs. The pipeline stops one operator short of `Bind`.

    **Sorting is the exception, decided 2026-10-06 while this record is still
    `proposed`** (the person, on `0032`): the tracker owns the comparer and
    publishes it, and the sort itself happens in the consumer's
    `SortAndBind`. A sort stage inside the chain cannot be seen from outside it —
    DynamicData's `Sort` produces an `ISortedChangeSet`, the `Transform` that
    derives the stale mark returns a plain changeset, and the order is gone by the
    time the stream is published. So the pipeline would have sorted and no
    consumer could have honoured it. `SortAndBind` is also what DynamicData 9
    recommends for a bound collection. `SortBy(comparer)` stays on the tracker,
    one comparer is published for every consumer, and `fleet-pipeline` B-009 is
    unchanged — it claims that a new comparer reorders in place, which is what the
    consumer's bind now does. Rejected: publishing `ISortedChangeSet` from the
    tracker, which widens `Fleet`'s type and makes every consumer's `Bind`
    index-sensitive for a demo where one consumer sorts.

3. **The chain is shared** by DynamicData's own `RefCount()` — the changeset
   operator, not Rx's `Publish().RefCount()` pair. Its documented behaviour is
   "cache-aware equivalent of `Publish().RefCount()`": an internal cache is
   created on the first subscriber, every subscriber shares one upstream
   subscription, and the cache is disposed when the last one unsubscribes
   (DynamicData 9.4.33, `ObservableCacheEx.RefCount`). So one `Connect()`, one
   diff pass and one filter/sort evaluation however many subscribers there are,
   **and a subscriber that joins while another is already bound receives the
   current fleet from that cache** rather than only subsequent changes. Rx's
   pair would have given it the latter, which is the difference that decides
   this.
4. **The inputs are methods on the tracker**, not a seam the consumer
   implements and not observables it has to construct: `Filter(predicate)`,
   `SortBy(comparer)`, `GroupBy(grouping)` and `StaleAfter(threshold)`, each
   ticking a `BehaviorSubject<T>` the tracker owns and seeds with the default the
   specification claims. The `IFleetQuery` interface § 7 declared is deleted — it
   was shaped by its only implementer, a view model, which is the input seam
   pointing at its consumer.

    This replaced an earlier form of the same decision, recorded here rather than
    rewritten away: the inputs were first made four `IObservable<T>` constructor
    parameters. Both remove the seam, but the parameters left `fleet-pipeline`
    B-007 ("every vehicle visible before a predicate arrives") and B-017 ("five
    minutes") to be kept by whatever `StartWith` each caller remembered — a
    promise made by the pipeline and discharged by its consumers, where a
    forgotten seed is an empty grid at startup and no test of the pipeline can
    see it. Methods put the defaults inside the thing that claims them, and let a
    caller drive the pipeline without owning any Rx.

5. **`Bind` and the marshal are the consumer's.** A view model calls
   `ObserveOn(ISchedulerProvider.UserInterfaceThread)` then `Bind`, and disposes
   that one subscription with itself. The pipeline reads no ambient scheduler and
   marshals to no UI thread on anyone's behalf.
6. **Pacing stays in the pipeline.** `Notices(IObservable<TimeSpan>)` keeps its
   caller-supplied rate cap: pacing is an operator with behaviour to test, not a
   projection, and one implementation is why two consumers cannot throttle
   differently by accident.
7. **The tracker is a container singleton**, disposed by the container at
   shutdown. Who starts the first poll, and how an actor above the seam gets its
   container-resolved collaborators, stays
   [item `0040`](../../.issue/0040-actor-wiring-and-tracker-lifetime-spike.yml)'s.

## Consequences

- The input surface is discoverable from the type: four methods naming four
  operations, each with the claim it serves in its summary. The cost is that the
  tracker now has settable state where an earlier draft had none, so
  `fleet-pipeline` B-003's line — a method hands a value to a stage and touches
  no collection — has to be stated rather than implied by the shape.
- A consumer that is not a UI — a test, an export, a future headless mode —
  consumes the pipeline without materialising a collection it does not want.
  Every pipeline stage stays provable with no view model.
- `mvvm` § "Projecting state back" is edited in place: marshalling stays at the
  view model boundary, and the bullet that assumed the pipeline owns the
  collection now says the view model creates it by binding and holds no second
  one.
- The comparer is part of the published surface: `Order` is an
  `IObservable<IComparer<TrackedVehicle>>`, adapted from the description's
  comparer over `TransportVehicle` with `fleet-pipeline` B-011's key tie-break
  appended, so a consumer cannot bind an unstable order by forgetting it.
  `fleet-dashboard` § 7 owes the `SortAndBind` call that uses it; its B-005 and
  B-012 are unchanged, since neither named the operator.
- `fleet-pipeline` B-002 and B-005 are amended, and a new claim covers the
  shared chain. `fleet-dashboard` B-005 changes from "bind the collection the
  tracker exposes" to "bind the stream and own the collection".
- Each view model's `Bind` subscription is its own to dispose, so a disposal bug
  now presents as one view's rows freezing rather than as the whole fleet
  stopping. That is the cost of the ownership being where it is.
- The chain still stops when nothing at all is watching, and the internal cache
  goes with it: the second page to open sees the current fleet, but the first one
  to open after every consumer has gone sees an empty grid until the next poll.
  `AsObservableCache()` is the alternative — it subscribes on first access and
  maintains a live snapshot regardless of subscribers, disposed with the tracker
  — and it was not taken, because a chain running with nobody watching spends
  credits for nothing. If that empty grid shows up on stage, that is the thing to
  revisit.
- **The tracker holds no subscription of its own.** An earlier draft of this
  record said it held the staleness tick and quiet-notice subscriptions "whether
  or not anyone is bound", which contradicted the bullet above it: such a
  subscription keeps the reference count above zero, so the chain would never
  stop and `AsObservableCache()` would have been the honest choice after all.
  The 2026-10-05 specification review caught it before anything was built. The
  staleness mark is a stage inside the shared chain, driven by the ticks seam,
  and the notice derivation — including the timer that measures silence — is
  built per `Notices(…)` call over the same shared stream. Silence is therefore
  reported while someone is listening and not otherwise.
- `IFleetTracker : IDisposable` still means something: `Dispose()` completes a
  shutdown signal that every published stream is `TakeUntil`-ed by, so a bound
  consumer's collection stops because its source completed. Disposal is
  observable in the streams rather than in a list of handles, which is
  `fleet-pipeline` B-004 as that review amended it.
