---
title: "Specification: Replay source"
description: "Record a live provider's payloads verbatim and feed them back at their recorded cadence through the same seam and the same selector the live sources use, so the demo survives a dead venue network and the closing act has a fallback."
type: spec
spec_status: draft
---

# Specification: Replay source

## 1. Business Goal

<!-- Owner: spec-author. One paragraph. The outcome, not the implementation.
     Name the failure state being removed. -->

The talk's claim is that polled data can still be reactive, and it is proved by a grid filling on stage. That proof currently depends on a venue network and on a provider that blocks hyperscaler IPs, rate-limits by credit, and hands out tokens that expire in thirty minutes — so the demo's headline mechanism and the venue wifi share a single point of failure. This feature removes it by recording what a live provider actually sent during rehearsal and feeding those payloads back at their original spacing through the same `ITrackerSource` seam and the same selector the live sources use. The outcome is a fallback the presenter can reach with the control they already have, for aircraft and for vessels both, and a second thing for free: because nothing downstream can tell a recording from a live feed, replay *is* the demonstration that the pipeline does not care where the data comes from. The failure state removed is a demo that only works on a good network — which, as `run-the-demo` puts it, is not a demo but a gamble.

## 2. User Needs

<!-- Owner: spec-author. The audience for this project is specific — see
     README.md § "Audience" — so do not write a generic persona. -->

| #   | Persona                                                                                                                      | Need                                                                                                     | Pain point today                                                                                                                                              |
| --- | ---------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | The presenter running the demo on stage (README.md § "Demo resilience")                                                      | To finish the talk when the venue network dies mid-sentence                                              | The only source of data is a provider reached over a network nobody in the room controls, and there is nothing to fall back to                                 |
| 2   | The presenter at the closing act (README.md § "Closing act"; run-the-demo § "Never add")                                     | The ships ending to survive the same failure as the main demo                                             | The closing act is the one part with no fallback at all, so the riskiest minute of the talk is also the least protected                                        |
| 3   | Line-of-business .NET developer in the talk audience (README.md § "Audience")                                                | To see that the pipeline is indifferent to its source, not be told it                                    | "Swap the source and nothing downstream changes" is an assertion until the audience watches a recording drive the same grid the live feed drove                |
| 4   | The same developer, reading this repository afterwards — the repository is the takeaway                                      | One switch between sources, not a live path and a separate offline path                                  | Most codebases grow an "offline mode" flag that only runs under pressure, and it is the code least exercised and most likely to be broken when it is needed    |
| 5   | The same developer, whose CI cannot reach the provider                                                                       | To exercise the pipeline end to end with no network and no credential                                    | OpenSky blocks hyperscaler IPs (README.md § "Gotchas"), so a build agent can never run the live path, and a test that needs a key passes only on one machine   |
| 6   | The presenter rehearsing the day before, inside the credit budget (README.md § "Limits")                                     | To capture rehearsal traffic without paying twice for it                                                 | Credits are the budget; a recorder that polls on its own doubles the spend of every rehearsal it observes                                                      |

## 3. Acceptance Criteria

<!-- Owner: spec-author. Numbered, falsifiable, SHALL / SHALL NOT. One claim
     per row. These ids are what § 9 and the .feature file are anchored to, so
     they are permanent: never renumbered, never reused. A withdrawn claim is
     marked Withdrawn, not deleted. -->

Twenty-three claims in five groups: **B-001 – B-006** the recording;
**B-007 – B-014** playback; **B-015 – B-018** selection and swap;
**B-019 – B-021** the vessel half; **B-022 and B-023** the substitution point
of each half, appended as § 11 rows 1 and 2 were answered rather than
renumbered into the groups they belong to — ids here are permanent.

Claim ids are per-Feature, per spec-and-traceability § "Claim IDs are
`B-00n`". `B-001` here and `B-001` in
[`aircraft-source`](../../aircraft-source/.spec/README.md) are different
claims: an `.issues/` item names its spec, and its `claims:` ids resolve
against that spec.

| ID    | Claim                                                                                                                                                                                                                                      | Source                                                        | Status |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------- | ------ |
| B-001 | A recording SHALL be written in the format [ADR-0004](../../../.spec/adr/0004-ndjson-recording-format.md) fixes: one line per payload, the provider's payload verbatim, beside the instant it arrived.                                       | ADR-0004; api-mock § "Recording"                               | Draft  |
| B-002 | The recorder SHALL NOT reshape, filter, reorder or omit a payload, including an empty or sparse one; what the provider sent SHALL be what the line holds.                                                                                   | api-mock § "Recording" — raw, not parsed                       | Draft  |
| B-003 | Recording SHALL NOT issue a request of its own; it SHALL record what a live source already fetched, so recording costs no additional credit and opens no additional socket.                                                                  | README.md § "Limits"; § 4 row 10                               | Draft  |
| B-004 | Recording SHALL be observationally transparent: the changesets reaching the fleet SHALL be identical whether recording is on or off.                                                                                                        | Decided call — a recorder is a tap, not a second source        | Draft  |
| B-005 | A recording intended for stage use SHALL span at least the staleness threshold, so appearance, update and staleness are all reproducible from it.                                                                                            | api-mock § "Recording"; aircraft-source B-051                  | Draft  |
| B-006 | A rehearsal recording SHALL NOT be committed to the repository, and SHALL NOT be used as a test fixture unless it has been scrubbed of real traffic first.                                                                                   | api-mock § "Never add"; § 4 row 7                              | Draft  |
| B-007 | Replay SHALL emit payloads in recorded order at the recorded inter-arrival spacing, derived from the recorded instants, and SHALL NOT substitute a fixed interval.                                                                           | api-mock § "Replay"                                            | Draft  |
| B-008 | On reaching the end of a recording, replay SHALL continue from its first payload; the stream SHALL NOT complete, fault, or emit a changeset that removes every vehicle at the loop boundary.                                                 | api-mock § "Replay"                                            | Draft  |
| B-009 | The observed instant downstream SHALL be the replayed payload's own reported time — never the recorded arrival instant, and never an ambient clock.                                                                                          | aircraft-source B-003; ADR-0004 § "Decision"                   | Draft  |
| B-010 | Staleness under replay SHALL be driven by the recording's time base, so a vehicle becomes observably stale at the same point in the recording at which it did live; the staleness clock SHALL be injected, not ambient.                      | api-mock § "Replay"; dynamic-data-pipeline § "Staleness"       | Draft  |
| B-011 | Replay SHALL make no network request and SHALL require no credential; it SHALL run to completion with the provider unreachable and no secrets configured.                                                                                   | api-mock § "Strategies on the same seam"; § 4 row 2            | Draft  |
| B-012 | A torn or unparseable final line SHALL be discarded and the stream SHALL continue; a recording truncated mid-write SHALL NOT fault replay.                                                                                                  | ADR-0004 § "Decision"                                          | Draft  |
| B-013 | Replay SHALL produce domain vehicles through the same projection the live strategy uses; there SHALL NOT be a second converter, parser or mapper for recorded payloads.                                                                      | mapping § "One mapper per boundary"; api-mock § "Recording"     | Draft  |
| B-014 | A recording SHALL remain replayable after a converter fix, with no re-recording — which is what recording the payload raw is for.                                                                                                           | api-mock § "Recording"; ADR-0004 § "Decision drivers"          | Draft  |
| B-015 | Replay SHALL be selected through the same mechanism as any live source, and there SHALL be no offline-mode flag, no replay-only selector, and no second selection path.                                                                      | api-mock § "Replay"; hot-swap-source § "Never add"             | Draft  |
| B-016 | No consumer SHALL be able to observe from the seam that replay rather than a live provider is selected.                                                                                                                                      | aircraft-source B-039; hot-swap-source § "Never add"           | Draft  |
| B-017 | Swapping to replay SHALL stop the outgoing live source, so a swapped-out poller stops spending credits and a swapped-out socket stops reading.                                                                                               | aircraft-source B-040; hot-swap-source § "Disposal discipline" | Draft  |
| B-018 | Swapping to or from replay SHALL NOT rebuild the tracker, its collection, its filters, its sorts, its groups or its bindings.                                                                                                                | hot-swap-source § "What must not be rebuilt"                   | Draft  |
| B-019 | A vessel recording SHALL use the same format and the same selection path as an aircraft recording, with one line per received message; messages SHALL NOT be batched into poll-shaped snapshots to make a push feed resemble a polled one.   | api-mock § "Strategies on the same seam"; ais-stream           | Draft  |
| B-020 | A recorded vessel fallback SHALL exist before the closing act is presented.                                                                                                                                                                  | run-the-demo § "Never add"                                     | Draft  |
| B-021 | Replay SHALL introduce no vehicle, record or snapshot type of its own; it SHALL reproduce whatever the recorded provider's own Feature defines.                                                                                               | aircraft-source § 5 row 3; § 5 row 2 below                     | Draft  |
| B-022 | Replay SHALL substitute at the provider's own API contract, handing back recorded envelopes, and SHALL introduce no snapshot client, cache, converter or projection of its own; the live path's snapshot client SHALL be the one constructed over it. | ADR-0002 § "Decision" item 2; § 11 row 1 | Draft  |
| B-023 | Vessel replay SHALL substitute at the strategy — an `ITrackerSource` fed by a recording — because a push provider has no contract to stand in for; it SHALL reach the seam the same way every other source does. | ADR-0002 § "Consequences"; § 4 row 5 | Draft  |

<!-- Status: Draft | Built | Withdrawn. "Built" means a test cites it and § 9
     says Verified. -->

## 4. Constraints

<!-- Owner: spec-author. Impact states what the constraint rules out, so § 7
     has something concrete to satisfy. Many of this project's constraints are
     the data provider's and are not ours to simplify — credit budgets, token
     expiry, blocked hyperscaler IPs (README.md). -->

| #   | Constraint                                                                                                                                                              | Source                                            | Impact                                                                                                                                                                                                                                                              |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | The recording format is fixed repository-wide: NDJSON, one line per payload, payload verbatim beside its arrival instant.                                               | ADR-0004                                          | Rules out choosing a format here, and rules out a per-feed format. An accepted ADR is immutable, so a different format is a new ADR superseding it — not a claim in this section.                                                                                     |
| 2   | OpenSky blocks AWS and other hyperscaler IPs, and the venue network is outside our control.                                                                             | README.md § "Gotchas"; run-the-demo                | Rules out treating replay as a test convenience: it is the stage contingency and a CI prerequisite. Also rules out any replay path that needs the provider reachable to start.                                                                                        |
| 3   | The observed instant comes from the provider's own envelope, and no consumer reads an ambient clock to supply one.                                                       | aircraft-source B-003                             | Rules out the recorded arrival instant being used as the observed instant. The arrival instant paces playback (B-007) and nothing else — two times on one line, one job each.                                                                                         |
| 4   | The response envelope and the positional row may be referenced only by the class implementing the API contract and by the snapshot client; the snapshot dies at the projection. | aircraft-source B-045, B-046                      | Satisfied rather than relaxed. Replay substitutes at the contract and brings no client of its own (B-022), so the envelope and the positional row stay referenced by exactly the two components B-045 names. **B-045 is not widened**, and a replay component that parses a recorded payload itself remains ruled out.                                 |
| 5   | A contract layer exists only where the provider is request/response shaped; a push provider's strategy has none.                                                        | aircraft-source B-049; § 4 row 4 of that spec      | Rules out one uniform substitution point across both feeds. Aircraft replay has a contract it could stand in for; vessel replay has none, so the two halves substitute at different depths — B-022 for aircraft, B-023 for vessels. Both reach the seam, which is the only symmetry a swap target needs.                                               |
| 6   | `Vessel`, the vessel client, its cache and its tracker source are unspecified — deferred by the aircraft-source specification.                                          | aircraft-source § 5 row 3                         | Rules out any claim here about a vessel record's members, keys or units. B-019 and B-021 claim replay behavior over whatever that Feature defines, and no more.                                                                                                       |
| 7   | A rehearsal recording is operational data — real callsigns, real positions — and credentials are never committed, logged, or placed in a fixture.                        | api-mock § "Never add"; aircraft-source § 4 row 12 | Rules out committing a recording, shipping one as a fixture, and any test that reads one. `recordings/` is git-ignored (ADR-0004); fixtures stay synthetic and committed beside their tests.                                                                          |
| 8   | No test may touch a network or the wall clock.                                                                                                                          | api-mock § "Tests"; test-from-scenarios            | Rules out `Thread.Sleep`, a real delay, and a test that replays in real time. Replay's cadence is driven by an injected scheduler, so a test advances time deliberately — in production as well as in tests.                                                          |
| 9   | There is no Gherkin runner in this repository — no Reqnroll, no bindings, no step definitions.                                                                           | AGENTS.md; spec-and-traceability                   | Rules out the `.feature` file being the executing artifact, and rules out an `@ignore` tag. A scenario existing never means a claim is covered; the § 9 row pointing at an xUnit test does.                                                                           |
| 10  | Credits are the budget — a ≤25 sq° box costs 1 credit per poll against 4,000 per day, polled every 15 seconds.                                                           | README.md § "Limits"; aircraft-source B-050         | Rules out a recorder that polls independently of the live source (B-003), which would double rehearsal spend. Recording is a tap on traffic already paid for.                                                                                                         |
| 11  | A vehicle past the staleness threshold is marked and kept, never removed, at a configurable five minutes.                                                               | aircraft-source B-051                             | Fixes the minimum useful recording length (B-005): a recording shorter than the threshold can never show staleness, so it cannot rehearse the part of the talk staleness is in.                                                                                       |
| 12  | Scrutor's `Decorate<>` wraps only what is already registered; a strategy registered after the decorate call resolves raw, silently.                                     | ADR-0003 § "Consequences"                          | Rules out registering replay after the decorator is applied. A replay strategy that is never wrapped is one that cannot be selected — and it fails silently, on stage, which is the one place this feature exists to protect.                                          |

## 5. Out of Scope

<!-- Owner: spec-author. The section nobody writes. Without it, a demo grows a
     feature nobody asked for. -->

| #   | Item                                                                                            | Exclusion reason                                                                                                                                                                                                                                      |
| --- | ----------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | The simulated source                                                                            | A separate Feature. It generates movement with no recording, no fixture and no cadence file, so ADR-0004 does not bind it and none of B-001 – B-014 applies. aircraft-source § 5 row 4 groups replay and simulated only because both were excluded there. |
| 2   | The vessel feed itself — `Vessel`, the AISStream client, its cache and its tracker source       | Deferred by aircraft-source § 5 row 3 and still unspecified. B-019 – B-021 claim replay's behavior over that feed; defining the feed is the vessel Feature's § 3, and writing it here would be authoring another Feature's claims.                      |
| 3   | Performing the recording                                                                        | An operational task at rehearsal, tracked in README.md § "Open items". B-005 claims what a stage recording must contain; capturing one is not code.                                                                                                     |
| 4   | A scrubbing tool                                                                                | B-006 forbids unscrubbed reuse; it does not commission an automated scrubber. Scrubbing a few minutes of NDJSON by hand is why the format is line-oriented and legible (ADR-0004 § "Consequences").                                                      |
| 5   | A transport control — seek, pause, scrub position, step a frame, change playback speed          | Playback is forward and looping (B-007, B-008). A transport is a media player nobody asked for, and ADR-0004 explicitly buys no index to seek with.                                                                                                      |
| 6   | Compression of recordings                                                                       | ADR-0004 § "Consequences" accepts uncompressed JSON at demo length and defers compression to a later record if it ever matters.                                                                                                                         |
| 7   | The swap control, the stage choreography, and what the audience sees during the gap             | `hot-swap-source` owns them, and the gap is already decided in aircraft-source decisions/0002. This spec claims replay's obligations as a swap target (B-015 – B-018) and stops there.                                                                   |
| 8   | An offline mode, an environment flag, or a "use replay" setting separate from source selection   | Forbidden by B-015 and by hot-swap-source § "Never add". Recorded as an exclusion because it is the obvious thing to add under pressure, and the whole point is that there is one switch.                                                                |
| 9   | Recording the domain side of the seam — changesets, vehicles, or the fleet's contents            | A recording is provider payloads (B-001). Recording after the projection would bake today's projection into the file and foreclose B-014, which is the reason raw was chosen over parsed.                                                               |
| 10  | A history store, a track per vehicle, or any persistence of domain state                        | Each cache holds current state; aircraft-source § 5 row 14 rules a history collection out. A recording is an input to the pipeline, not an archive of its output.                                                                                        |
| 11  | The pipeline operators, the grid, and every other UI concern                                    | Downstream of the seam and untouched by a swap (B-018). `build-maui-ui` and `mvvm` own them; no scenario here names a UI mechanic.                                                                                                                      |
| 12  | A Gherkin runner, Reqnroll, step definitions, or bindings                                       | § 4 row 9. Scenarios are documentation; a runner would move the coverage gate off § 9, which is where AGENTS.md puts it.                                                                                                                                |
| 13  | The `airplanes.live` backup source                                                              | Its access terms are unresolved (README.md § "Backup source"). A second live provider is a different contingency from a recording, and it gets its own contract and strategy if and when the terms clear.                                                |

## 6. Concern Separation

<!-- Owner: implementer. Classifies each item Business, Technical, or Both —
     the mechanism that stops business and technical judgment collapsing into
     one undifferentiated paragraph. -->

| Item     | Classification | Notes     |
| -------- | -------------- | --------- |
| {{item}} | Business       | {{notes}} |

## 7. Technical Design

<!-- Owner: implementer. One owner by design, and a compromise: a decision
     bigger than this item — a new seam, a changed boundary, a technology
     choice — is a question for the person, not something to settle here. -->

Unwritten. The substitution point it would otherwise have had to settle is
already answered — B-022, recorded repository-wide in [ADR-0002](../../../.spec/adr/0002-contract-client-strategy-tracker.md)
rather than here, because it binds any provider's contract and not this Feature
alone. What is
left for this section is the replay contract implementation's own shape: how a
recording is opened and read, how the recorded spacing reaches an injected
scheduler, and where § 11 row 3's configuration lands.

## 8. Testing Strategy

<!-- Owner: test-writer. -->

**Testability assessment**

| Dimension          | Verdict | Finding     | Recommendation |
| ------------------ | ------- | ----------- | -------------- |
| DI seams           | Pass    | {{finding}} | —              |
| Behavior isolation | Pass    | {{finding}} | —              |
| Coverage potential | Pass    | {{finding}} | —              |

**Scenarios**

<!-- Full Gherkin lives in <feature-slug>.feature beside this file, not
     inlined here. Each scenario carries the @B-00n tag of the claim it
     proves. Scenarios are documentation; the xUnit tests execute. -->

- Happy path → {{claim_ids}}
- Failure mode → {{claim_ids}}
- Validation failure → {{claim_ids}}
- Data-driven → {{claim_ids}}

## 9. Traceability Matrix

<!-- Owner: test-writer. The gate. A `Missing` row blocks ship — it is not a
     note, it is a stop sign. Every id in § 3 appears here exactly once,
     anchored to the scenario's @B-00n TAG rather than to the title in the
     Scenario column. -->

| Claim ID | Scenario     | Test     | Status   |
| -------- | ------------ | -------- | -------- |
| B-001    | {{scenario}} | {{test}} | Verified |

<!-- Status: Verified | Missing. -->

## 10. Lessons / Spec Deltas

<!-- Owner: whichever role closed the bug. Index only — one file per lesson in
     lessons/ beside this file, from .spec/templates/lesson.md. Append only. A
     delta that adds behavior also needs a § 3 row, which is spec-author's to
     write. "None yet." is a valid body. -->

None yet.

## 11. Open Questions

<!-- Owner: whoever is blocked. "None." is a valid body. -->

Rows 1 and 2 have been answered and left the table; row 3 keeps its number,
because § 7 references it. An answered question stays below rather than being
deleted — it is a record of what was asked, not clutter.

| #   | Question                                                                                                    | Owner       | Target date |
| --- | ----------------------------------------------------------------------------------------------------------- | ----------- | ----------- |
| 3   | How is the recording to replay chosen at launch, and where does it live relative to the running application? | implementer | before § 7  |

**Row 1 — answered: replay substitutes at the API contract, and selection stays
at the seam.** A replay implementation of the provider's contract hands back
recorded envelopes, and the live path's own snapshot client and cache are
constructed over it — the same client class, not a second one. That whole chain
registers as another `ITrackerSource`, so replay is a swap target like any live
strategy and B-015 – B-018 hold as written. The claim is B-022.

Two consequences worth keeping visible. **aircraft-source B-045 is not
widened**: replay introduces no client, so the envelope and the positional row
stay restricted to the two components that claim names (§ 4 row 4). And the
reason this reading won is that the recorded envelope carries the provider's own
reported time, so B-009's observed instant and B-010's staleness come free, with
no second implementation to re-derive them — where a replay client of its own
would have meant a second positional-row reader to keep correct and a second
place B-003 had to be honoured. A boundary rule honoured in two implementations
is one that drifts.

The decision binds any provider's contract rather than this Feature alone, so it
is recorded in [ADR-0002](../../../.spec/adr/0002-contract-client-strategy-tracker.md)
— amended there rather than given a record of its own, because that ADR's
subject already is the layering and where each thing substitutes in it.

**Row 2 — answered: vessel replay substitutes at the strategy.** It cannot
mirror row 1, because there is no vessel contract to stand in for (§ 4 row 5),
so the two halves substitute at different depths: aircraft replay below the
snapshot client, vessel replay at the strategy itself. B-023 claims it, and
ADR-0002 § "Consequences" accepts the asymmetry as following from its own
reading rather than from anything about the vessel feed. Both halves still reach
`ITrackerSource`, so the symmetry a swap target actually needs is untouched —
what makes something selectable is the seam it lands on, not the depth it
substitutes at.

**Row 3.** ADR-0004 fixes the format and the shape of a recording's name; it
deliberately says nothing about which recording a run selects or where the
directory sits relative to the application. Configuration, not format, and
answerable without reopening the ADR.

## 12. Sign-off

<!-- Owner: spec-reviewer. 🟡 Draft | 🟢 Approved | 🔴 Blocked — state the
     reason on a Blocked row. Overall goes 🟢 only when every row is 🟢 and
     § 9 has no Missing row. Overall 🟢 is what flips spec_status to
     approved. -->

| Sections                                                                  | Owner          | Status   |
| ------------------------------------------------------------------------- | -------------- | -------- |
| Business Goal, User Needs, Acceptance Criteria, Constraints, Out of Scope  | spec-author    | 🟡 Draft |
| Concern Separation, Technical Design                                      | implementer    | 🟡 Draft |
| Testing Strategy, Traceability Matrix                                     | test-writer    | 🟡 Draft |
| Overall                                                                   | spec-reviewer  | 🟡 Draft |

## Decisions

<!-- Owner: the role that made or reversed the call. Index only — one file per
     decision in decisions/ beside this file, from
     .spec/templates/decision.md. A product or scope call goes there; a
     durable technical choice goes to adr/ instead. "None yet." is valid. -->

None yet.

The recording format this specification is written against binds both replay
feeds rather than this Feature alone, so it is recorded in the
repository-wide [ADR-0004](../../../.spec/adr/0004-ndjson-recording-format.md),
as the layering and each half's
substitution point are in [ADR-0002](../../../.spec/adr/0002-contract-client-strategy-tracker.md) —
neither here nor in this Feature's `adr/`. The scope call that this Feature
covers both replay feeds while the simulated source becomes its own is recorded
as § 5 row 1 rather than as a `decisions/` file, because it is a boundary of
this specification rather than a call made and reversed inside it.

## Tasks

<!-- Owner: spec-author. The items cut from § 3 once the claims exist. Ids
     only — never restated titles, or the two records disagree. "None yet."
     is valid while the spec is still being agreed. -->

None yet. No item is cut while § 11 row 1 is open: the substitution point
decides how many components this Feature has, so items cut now would be
re-cut.

## Scoring

<!-- Owner: spec-author, recording the item's value and risk when they change.
     The authoritative values live in the .issues/ item. -->

| Date         | Field | From | To  | Rationale     |
| ------------ | ----- | ---- | --- | ------------- |

No scoring recorded — no `.issues/` item has been cut from § 3 yet, and
`value` and `risk` live in the item rather than here.
