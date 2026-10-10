---
title: "Lesson 0001: a rule that reads a namespace outlives the namespace's meaning"
description: "TRN0006 called the published element a source internal, because Tracking/ held only internals the day the rule was written."
type: lesson
---

# Lesson 0001: a rule that reads a namespace outlives the namespace's meaning

**Date:** 2026-10-06
**Kind:** product

## Symptom

The first view model `fleet-dashboard` added did not compile:

```
error TRN0006: the view model 'Transporter.Features.Fleet.ViewModels.FleetViewModel'
names 'TrackedVehicle' (aircraft-source B-041)
```

Three types reported, all of them the ones `IFleetTracker` publishes —
`TrackedVehicle`, `FleetColumn`, `FleetGrouping` — and the page in `src/Gui`
would have taken `TRN0004` for the same references. Binding them is `B-005` and
`B-007`, so the analyzer forbade what the specification requires.

## Root cause

`Layers.IsConcreteTracking` answered "a non-interface type under
`Transporter.Tracking`". That was a true reading of the layer in September: the
namespace held the strategies, the sources and the decorator, and nothing a
consumer was meant to name. [ADR-0009](../../../../.spec/adr/0009-the-pipeline-publishes-changesets-a-consumer-binds.md)
put published values in the same namespace — the element a consumer binds and
the description it builds columns from — and the rule had no way to tell them
apart, because a namespace cannot say which of its types are an interface's
output.

Neither claim behind the rule was wrong. `aircraft-source` B-041 names a
strategy, a client, a cache and a decorator; `fleet-dashboard` B-020 lists the
forbidden set in full. The published element is on neither list. The analyzer
was enforcing something stricter than any claim said, and the stricter reading
was invisible until a Feature downstream of the tracker existed to trip it.

## Spec delta

`src/Transporter.Analyzers/.spec/README.md` § 7's diagnostic table says what
`TRN0004` and `TRN0006` exempt: the types `IFleetTracker` publishes, and the
types those carry. No claim changed — the rule now reports what its claim
always said, and the exemption is read from the seam rather than listed, so a
member added to `IFleetTracker` carries its types across with it.

## Claim

- `aircraft-source` B-041 — `BoundaryAnalyzerTests.GivenAViewModelNamingWhatTheSeamPublishes_WhenAnalyzed_ThenItIsNotReported`, beside the unchanged `GivenAViewModelNamingAStrategyClientCacheOrDecorator_WhenAnalyzed_ThenItIsReported`
