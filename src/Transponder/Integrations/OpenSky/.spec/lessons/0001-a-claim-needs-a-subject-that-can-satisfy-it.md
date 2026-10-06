---
title: "Lesson 0001: A claim needs a subject that can satisfy it"
description: "Three claims named the snapshot client for behaviour only the thing holding the HTTP response can perform, and no component could satisfy them as written; the design was right and the subject was wrong."
type: lesson
---

# Lesson 0001: A claim needs a subject that can satisfy it

**Date:** 2026-10-05
**Kind:** product

## Symptom

§ 7 could not place B-026 – B-028 without contradicting a claim. Each named the
snapshot client for something only the holder of the HTTP response can do — a
`401`, the `X-Rate-Limit-Remaining` header, the `X-Rate-Limit-Retry-After-Seconds`
header — while B-006 forbids a credential on the contract, so the client could
not hold the token either. The poll diagram put all three in the transport and
disagreed with § 3 in writing. B-028 was worse: the sentence forbade an
exception outright, its scenario forbade one only at the subscriber, and
[ADR-0008](../../../../.spec/adr/0008-the-contract-is-the-boundary-and-may-throw.md)
makes the contract throw, so the transport as built satisfied the scenario and
contradicted the claim. The item that carries all three, `0004`, could not
start.

## Root cause

The claims were written with "the snapshot client" standing in for the whole
provider chain, before the chain had parts. Nothing was wrong with the design
the words were reaching for; the subject was a placeholder that nobody went
back to replace, and § 3 is `spec-author`'s while the component boundary is
§ 7's. So the contradiction could be _seen_ by the role that could not fix it
and had to be routed through § 11 — which is why it sat open as a question
rather than being quietly corrected in the pull request that found it.

Three signals were available and none of them was a test: a diagram disagreeing
with a claim, a scenario narrower than the sentence above it, and a § 9 row
naming `OpenSkyHttpApiTests` for a claim whose subject was the client. The
matrix had been written to the real subject all along.

## Spec delta

B-026 and B-027 now name **the HTTP transport** — not "the transport", because
B-007 counts implementations per transport and a recording one has no token to
refresh. B-028 names both: the `429` is inspected in the HTTP transport and the
deferral is the snapshot client's, and its ban on an exception is scoped to a
subscriber of the client's stream, which is what the scenario always said.

No claim id was added or renumbered. One sentence carries both halves of B-028,
and all three remain `0004`'s to deliver: a claim's subject is which component
satisfies it, not which item builds it.

## Claim

- B-028 — `OpenSkyHttpApiTests.GivenAThrottledResponseAndGivenAServerError_WhenEachIsHandled_ThenTheFirstDefersByTheHeaderAndTheSecondStaysAnException`
