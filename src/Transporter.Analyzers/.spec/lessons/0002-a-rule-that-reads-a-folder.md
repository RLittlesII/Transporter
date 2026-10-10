---
title: "Lesson 0002: a rule that reads a folder outlives the one implementation it was written against"
description: "TRN0002 reported the replay contract for naming the envelope its own signature returns, because the rule allowed the transport folder rather than the contract."
type: lesson
---

# Lesson 0002: a rule that reads a folder outlives the one implementation it was written against

**Date:** 2026-10-06
**Kind:** product

## Symptom

The first file `replay-source` `0011` added did not compile:

```
error TRN0002: 'Transporter.Integrations.OpenSky.Replay.ReplayOpenSkyApi' names
'OpenSkyStatesResponse', which only the contract's implementation and the snapshot
client may name (aircraft-source B-045)
```

Three references reported, and every one of them is in the signature the
contract itself declares: `ReplayOpenSkyApi` implements `IOpenSkyApi`, whose one
method returns `OpenSkyStatesResponse`. The rule forbade a class from naming the
type it is obliged to return.

## Root cause

`ReportWireTypeMention` allowed three homes: `Layers.IsTransport`, which answers
"anywhere under `.../Http`"; `Layers.IsClientHome`, which answers "a type whose
name ends in `Client`"; and `Layers.IsWireSurface`, the `Contracts` namespace
that declares the envelope. Two of the three read where a file was filed.

That was a true reading of the integration while it had one implementation of
the contract. `aircraft-source` B-045 does not say it: it says "the class
implementing the API contract", and
[ADR-0002](../../../../.spec/adr/0002-contract-client-strategy-tracker.md) is
what makes a second one ordinary — `replay-source` B-022 substitutes at the
contract, which means an implementation that reaches a recording rather than a
provider. There is no folder that both of them share, and there should not be:
one is a transport over HTTP and the other is not a transport at all.

The tell is that the descriptor was right the whole time. Its message reads
"which only the contract's implementation and the snapshot client may name" —
the claim as written. The analysis beneath it was stricter than the sentence it
printed, which is the same drift
[lesson 0001](0001-a-rule-that-reads-a-namespace.md) records one rule over: a
classification that happens to be true of today's single case, written where a
claim's own words were available.

## Spec delta

No claim changed and no descriptor changed. B-008 still fixes the set at
seventeen rules and `aircraft-source` B-045 is unamended. § 7's rule table now
says `TRN0002` examines references "against the implementations of the contract
and the snapshot client", because "the two types allowed to hold them" was the
count that caused this: a provider has as many as it has transports, which
`replay-source` made two.

## Claim

- `aircraft-source` B-045, unchanged, now enforced as it is written —
  `BoundaryAnalyzerTests.GivenASecondImplementationOfTheContractOutsideTheTransport_WhenAnalyzed_ThenTheEnvelopeIsItsToHoldAndANonImplementationBesideItIsNot`,
  whose second half is what keeps the fix the contract rather than the folder.

## Skill

[`spec-and-traceability`](../../../../.skills/spec-and-traceability/SKILL.md)
§ "Claims and traceability" gained: **a mechanism classifies by what the claim
names, not by where today's one instance lives.** A folder, a namespace or a
name suffix is a proxy that holds until the second instance arrives, and the
mechanism then reports work the claim permits — with the claim's own wording
printed in the message, which is where the proxy should have been read from.
