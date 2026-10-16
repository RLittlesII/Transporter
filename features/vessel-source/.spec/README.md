---
title: "Specification: Vessel source"
description: "Subscribe to AISStream's push feed of vessel positions over the Houston box, hold what each message reports in a cache the client writes, project it into vessels on the same seam the aircraft use, and let a silent vessel go."
type: spec
spec_status: draft
---

# Specification: Vessel source

## 1. Business Goal

<!-- Rules: ../../../.spec/templates/feature.md § 1 -->

The talk ends on a promise: replace the polled source with a push source, and
everything downstream of the seam stays the same (README.md § "Closing act").
The person chose ships on 2026-10-15. Until a second source exists, the promise
is only something the presenter says, and the audience has to take it on trust.
This feature builds that source. AISStream's WebSocket feed of vessel positions
over the same Houston box the aircraft use is held in a cache that the push
client writes for itself, projected into vessels by a strategy of its own, and
selected with the control the presenter already has. The feature also gives the
audience the treatment the aircraft deliberately lack. A vessel that goes silent
is gone rather than quiet, so it is removed, and `ExpireAfter` is shown where it
belongs (README.md § "DynamicData operators"). The failure state removed is a
closing act with nothing behind it: "swap the source and nothing downstream
changes" stated, not shown.

## 2. User Needs

<!-- Rules: ../../../.spec/templates/feature.md § 2 -->

| #   | Persona                                                                                                                   | Need                                                                                              | Pain point today                                                                                                                                                           |
| --- | ------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Line-of-business .NET developer in the talk audience, whose data arrives by request and response (README.md § "Audience") | To watch a push feed drive the grid a poll drove a minute earlier, with nothing downstream edited | Every claim that the pipeline does not care about its source has so far been made with one source. A second source of the same shape would prove nothing                   |
| 2   | The presenter at the closing act (README.md § "Closing act")                                                              | One tap from aircraft to ships over the same water, and one tap back                              | There is nothing to swap to. A closing act that needs a restart, a second build or a different box is a different demo, not a swap                                         |
| 3   | The same developer, reading this repository afterwards (the repository is the takeaway)                                   | A push strategy beside a polled one, visibly different in shape and equal at the seam             | The aircraft path has a contract, a positional-row reader and a differ, and a reader can mistake all three for what makes a source swappable (ADR-0002 § "Consequences")   |
| 4   | The same developer, whose items do not depart but stop reporting                                                          | To see removal on silence as a decision a source makes, beside the stale marker aircraft get      | Only one staleness treatment has been shown. The other, `ExpireAfter`, is listed in README.md as an operator to demonstrate, and nothing in the repository demonstrates it |

## 3. Acceptance Criteria

<!-- Rules: ../../../.spec/templates/feature.md § 3 -->

Twenty-six claims in six groups. **B-001 – B-007** cover the connection,
**B-008 – B-013** the messages and the cache they are written to, **B-014 –
B-015** time, **B-016 – B-020** the vessel and its description, **B-021 – B-023**
silence, and **B-024 – B-026** the seam and the swap.

Throughout, the _vessel client_ is what holds the socket and writes the cache,
and the _vessel strategy_ is what projects that cache into vessels on
`ITrackerSource`. Both are names for responsibilities ADR-0002 items 4-6 already
assign. They are not types this section declares.

| ID    | Claim                                                                                                                                                                                                                                                                                        | Source                                                                                                      |
| ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| B-001 | The vessel client SHALL subscribe to exactly one bounding box: the one configured for the aircraft source, read from the same setting. No box SHALL be compiled in, and none SHALL be configured for vessels separately.                                                                     | `aircraft-source` decisions/0001; README.md § "The app"                                                     |
| B-002 | The vessel client SHALL send its subscription as the first frame on every connection it opens, before it reads anything, and SHALL send no subscription again on that connection.                                                                                                            | § 4 rows 2 and 4                                                                                            |
| B-003 | The AISStream API key SHALL reach the vessel client only from the developer's user-secrets store layered over the packaged settings, and SHALL NOT appear in the repository, a packaged setting, a log line, a recording or anything bound to a view.                                        | § 4 row 3; `aircraft-source` B-058; docs/runbook.md § "Never"                                               |
| B-004 | With no API key configured, selecting the vessel strategy SHALL open no connection and SHALL report the absent key as a failure a consumer can read. The application SHALL keep running, and selecting another strategy SHALL still work.                                                    | § 4 row 3                                                                                                   |
| B-005 | A connection SHALL be open only while the vessel strategy is the live one. Selecting another strategy SHALL close it, selecting the vessel strategy again SHALL open a new one, and any number of swaps in either direction SHALL leave at most one vessel connection open.                  | `aircraft-source` B-040; § 4 row 5; hot-swap-source § "Disposal discipline"                                 |
| B-006 | A connection the service closes or loses while the vessel strategy is live SHALL be reopened and resubscribed by the vessel client, without rebuilding the tracker, the cache or the strategy, and the vessels already held SHALL stay held. Silence alone decides whether they go (B-021).  | § 4 row 6; `aircraft-source` B-042                                                                          |
| B-007 | The vessel client SHALL request compressed transport on every connection it opens.                                                                                                                                                                                                           | § 4 row 7                                                                                                   |
| B-008 | A vessel SHALL be keyed by its MMSI, held as the nine-digit string the feed reports, and the key SHALL never change for the life of the vessel.                                                                                                                                              | ADR-0005 item 3; README.md § "Closing act"                                                                  |
| B-009 | A position report SHALL add that vessel to the vessel client's cache or update it there, one message at a time. The client SHALL NOT assemble a full set from the messages it holds, and SHALL NOT diff one set against another, to write the cache.                                         | ADR-0002 item 5 ("the writer owns the write"); § 5 row 2                                                    |
| B-010 | A static-data message SHALL set the ship name and ship type of the vessel it names. For a vessel not yet held, it SHALL add one with no position.                                                                                                                                            | § 4 row 2                                                                                                   |
| B-011 | The vessel client SHALL ask the feed for position reports and static data only, and SHALL ignore any other message type it receives.                                                                                                                                                         | § 5 row 4                                                                                                   |
| B-012 | A message that cannot be read SHALL be skipped. The vessel already held for it, if any, SHALL be unchanged, and the stream SHALL NOT end or fault because of it.                                                                                                                             | `aircraft-source` § 4 row 4                                                                                 |
| B-013 | The AIS "not available" values (latitude 91, longitude 181, true heading 511, speed over ground 102.3 knots, course over ground 360°) SHALL become an absent value, never a number. A position missing either coordinate SHALL be no position, never a position at `0,0` or at the sentinel. | § 4 row 8; ADR-0005 § "The members" (`Position`)                                                            |
| B-014 | A vessel's last contact SHALL be the instant the feed reports for the message that last updated it, and SHALL NOT be read from a clock by the client, the cache or the strategy.                                                                                                             | ADR-0007; dynamic-data-pipeline § "Staleness and expiry"; § 11 row 6                                        |
| B-015 | Each message carrying a reported instant SHALL advance the observed clock to that instant, as the aircraft envelope's time does. A message carrying none SHALL leave the clock where it is.                                                                                                  | ADR-0007 § "Consequences"; `fleet-pipeline` B-031; § 11 row 6                                               |
| B-016 | A vessel SHALL derive directly from `TransportVehicle`, one level below it, and the vessel strategy SHALL be the only place one is built.                                                                                                                                                    | ADR-0005 item 4; ADR-0002 item 6                                                                            |
| B-017 | A vessel's label SHALL be its ship name trimmed of surrounding whitespace, or its MMSI while that name is absent or blank.                                                                                                                                                                   | ADR-0005 item 2                                                                                             |
| B-018 | Speed over ground SHALL be held in knots, and course over ground and true heading in degrees from north, as the feed reports them. A converted value SHALL NOT replace a canonical one.                                                                                                      | ADR-0005 item 7; § 4 row 8                                                                                  |
| B-019 | The vessel description SHALL name its columns, comparers, filter choices and detail lines, and two grouping keys: ship type and flag state. Every one of them SHALL be a selector over `TransportVehicle`, and only that description SHALL name the vessel type.                             | `fleet-pipeline` B-020, B-022, B-043                                                                        |
| B-020 | Ship type SHALL group as `Unknown` until a static-data message has reported one. Flag state SHALL be the state the MMSI's leading three digits allocate (its MID), and SHALL group as `Unknown` for an MMSI whose MID the table does not hold.                                               | § 4 row 9; § 11 row 5                                                                                       |
| B-021 | A vessel silent for longer than the vessel silence threshold SHALL be removed from the fleet, and from every collection bound from it. A vessel that reports again after it was removed SHALL return as a new arrival under the same key.                                                    | README.md § "DynamicData operators"; dynamic-data-pipeline § "Staleness and expiry"; § 4 row 10; § 11 row 1 |
| B-022 | Silence SHALL be measured against the observed clock and re-evaluated as it advances, so a vessel is removed with no message arriving for it and no wall clock read.                                                                                                                         | `fleet-pipeline` B-018; ADR-0010                                                                            |
| B-023 | The vessel silence threshold SHALL be configurable, SHALL be distinct from the aircraft staleness threshold, and SHALL be applied to vessels only. An aircraft SHALL still never be removed for silence.                                                                                     | `fleet-pipeline` B-016, B-017, B-019; § 11 rows 1 and 3                                                     |
| B-024 | The vessel strategy SHALL reach the seam the way every strategy does, as a registered strategy behind the swap decorator, under the name `Vessels` for the swap control, with no contract layer beneath it and no member added to the seam.                                                  | ADR-0002 item 2; ADR-0011; `aircraft-source` B-037, B-049, B-057                                            |
| B-025 | The vessel strategy SHALL be registered with no poll status, so while vessels are live the tracker publishes none.                                                                                                                                                                           | ADR-0013; `fleet-pipeline` B-040; `aircraft-source` B-056                                                   |
| B-026 | Swapping between aircraft and vessels in either direction SHALL change the live description and the vehicles in the fleet, and SHALL NOT rebuild or edit the tracker's pipeline, a comparer, a predicate, a grouping key or a binding.                                                       | `fleet-pipeline` B-021; `aircraft-source` B-039, B-042                                                      |

## 4. Constraints

<!-- Rules: ../../../.spec/templates/feature.md § 4 -->

AISStream's facts below were read from <https://aisstream.io/documentation> on
2026-10-15. They are the provider's, not this project's to simplify, and the
page is the authority when the two disagree.

| #   | Constraint                                                                                                                                                                                                                                                                | Source                                                                          | Impact                                                                                                                                                                                                                                         |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | AISStream is push: one WebSocket at `wss://stream.aisstream.io/v0/stream`, a subscription frame sent by the client, then a stream of messages it never asked for one at a time.                                                                                           | aisstream.io documentation; README.md § "Closing act"                           | There is no request with a response, so there is no contract layer, no `Task<T>` per endpoint, and no fake one (`aircraft-source` B-049, § 4 row 4). Aircraft replay's way of substituting is closed to vessels, which is replay-source B-023. |
| 2   | The subscription is one JSON frame: `APIKey`, `BoundingBoxes` as `[[lat, lon], [lat, lon]]` corners with latitude first, and optionally `FiltersShipMMSI` and `FilterMessageTypes`. Every message arrives as `MessageType`, `MetaData` and a `Message` keyed by its type. | aisstream.io documentation                                                      | The configured box is converted to that corner order in one place. Latitude first is the reverse of a GeoJSON habit, and a swapped pair subscribes to empty ocean. B-001, B-010, B-011.                                                        |
| 3   | The API key is a credential. The service forbids direct browser use and keeps it server-side. The repository never holds a key.                                                                                                                                           | aisstream.io documentation; docs/runbook.md § "Never"; README.md § "Open items" | Rules out a key in `appsettings`, a test fixture, a log or a recording. It reaches the client the way the OpenSky secret does, and its absence is a reported failure rather than a crash. B-003, B-004.                                        |
| 4   | The subscription must arrive within three seconds of connecting or the service closes the socket. Resending replaces the subscription and is limited to one per second per connection. A rejected subscription gets no `SubscriptionConfirmation`.                        | aisstream.io documentation                                                      | Rules out connecting ahead of time and subscribing later, and changing the subscription on a live socket. One subscription per connection, sent first. B-002.                                                                                  |
| 5   | At most three subscribed connections per account and three open connections per originating IP. A fourth is rejected.                                                                                                                                                     | aisstream.io documentation                                                      | Rehearsing on one laptop while another runs the demo already spends two. A swapped-out or reconnecting client that leaks its old socket spends the rest. B-005.                                                                                |
| 6   | The service makes no uptime guarantee and replays nothing. It drops messages for a client that falls behind, and may close a slow connection.                                                                                                                             | aisstream.io documentation                                                      | A gap in the feed is lost, not recovered. A reconnect starts from now (B-006), and a vessel that goes unheard through a long gap is removed like any other silent vessel (B-021).                                                              |
| 7   | From September 2026, uncompressed connections are subject to per-user bandwidth limits, and messages over the limit are dropped. Compression is `permessage-deflate`, confirmed by `SubscriptionConfirmation.Message.CompressionEnabled`.                                 | aisstream.io documentation                                                      | Rules out a client that cannot negotiate `permessage-deflate`. This bears on § 11 row 2, the library choice. B-007.                                                                                                                            |
| 8   | AIS reports speed in knots and course and heading in degrees. It encodes "not available" as in-range-looking numbers (latitude 91, longitude 181, heading 511, speed 102.3, course 360).                                                                                  | ITU-R M.1371                                                                    | The values are canonical as reported (ADR-0005 item 7), and the sentinels must become absent before anything compares, sorts or plots them. A sentinel position plotted on the detail map draws a ship off the Arctic. B-013, B-018.           |
| 9   | An MMSI's first three digits are its Maritime Identification Digits, allocated to a flag state by the ITU.                                                                                                                                                                | ITU Maritime Identification Digits table                                        | Flag state is known from the first message, with no static data needed. It needs a table this repository does not have yet (§ 11 row 5). B-020.                                                                                                |
| 10  | `fleet-pipeline` B-019, amended 2026-10-15, treats silence per source: marking and keeping by default, and removal after a source's own threshold where that source's specification claims it.                                                                            | `fleet-pipeline` B-016, B-019, § 4 row 6, § 5 row 2, § 11 row 9                 | B-021 – B-023 are that claim's removal for vessels, and B-023's vessel threshold is the source's own threshold it names. Where the removal runs is `fleet-pipeline` § 11 row 9, still open. Nothing here edits another Feature's claims.       |
| 11  | The clock belongs to the tracker. No client, cache or strategy reads one.                                                                                                                                                                                                 | dynamic-data-pipeline § "Staleness and expiry"; ADR-0007; ADR-0010              | Rules out stamping a message with the arrival time read in the client. A vessel's time is what the feed reports (B-014), and silence is measured where staleness already is (B-022).                                                           |
| 12  | Package versions are central, and the WebSocket client is still undecided between `ClientWebSocket` and `AISStream.NET`.                                                                                                                                                  | `Directory.Packages.props`; README.md § "Open items"                            | No claim here names a library. A package, if one is chosen, joins `Directory.Packages.props`, and the choice is an ADR (§ 11 row 2).                                                                                                           |

## 5. Out of Scope

<!-- Rules: ../../../.spec/templates/feature.md § 5 -->

| #   | Item                                                                                                                                                                      | Exclusion reason                                                                                                                                                              |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | A contract layer for AISStream, or one invented so the two strategies diagram alike                                                                                       | `aircraft-source` B-049 and ADR-0002 § "Consequences" forbid it. It would buy symmetry on a slide and a lie in the code.                                                      |
| 2   | `EditDiff`, or any full-set assembly, on the vessel path                                                                                                                  | A push client already knows its deltas (ADR-0002 item 5). Assembling a set so a differ can run is the shape this Feature exists to contrast with. B-009.                      |
| 3   | Vessel replay, the vessel recording format, and the recording tap on the vessel client                                                                                    | `replay-source` B-019 – B-021 and B-023, delivered by `features/replay-source/.issue/0013-vessel-replay-strategy.yml`. This Feature defines the vessel; replay reproduces it. |
| 4   | AIS message types other than position reports and static data: class B reports, aids to navigation, base stations, search-and-rescue aircraft, safety and binary messages | A closing act needs ships moving and named. Every other type is a second projection with no audience. B-011.                                                                  |
| 5   | Performing the rehearsal recording of the feed                                                                                                                            | README.md § "Open items" and docs/runbook.md: a rehearsal task, not a build. B-020 in `replay-source` is the obligation.                                                      |
| 6   | Coinbase, or a simulated push source                                                                                                                                      | The closing act is ships (2026-10-15). README.md § "Closing act" lists the others as the options not taken.                                                                   |
| 7   | More than one bounding box, a box for vessels alone, or a global subscription                                                                                             | One geography serves both fleets (`aircraft-source` decisions/0001). B-001.                                                                                                   |
| 8   | Holding vessels across runs, or a vessel's history beyond what the pipeline already keeps                                                                                 | Nothing in the talk needs it, and the service replays nothing to fill one (§ 4 row 6).                                                                                        |
| 9   | How the grid, the detail pane, the map or the swap control show a vessel                                                                                                  | `fleet-dashboard`'s. This Feature supplies the description (B-019) and claims nothing about a view.                                                                           |
| 10  | Choosing between `ClientWebSocket` and `AISStream.NET`                                                                                                                    | A technology choice is an ADR (§ 11 row 2), not a claim.                                                                                                                      |
| 11  | A stale marker on vessels before removal                                                                                                                                  | Each source gets one treatment (dynamic-data-pipeline § "Staleness and expiry"), and the person chose removal for vessels on 2026-10-15.                                      |

## 6. Concern Separation

<!-- Rules: ../../../.spec/templates/feature.md § 6 -->

Not written. Owed by `implementer` once §§ 1-5 are agreed.

## 7. Technical Design

<!-- Rules: ../../../.spec/templates/feature.md § 7 -->

Not written. Owed by `implementer` once §§ 1-5 are agreed. § 11 rows 2 and 6
are design questions it will meet first.

## 8. Testing Strategy

<!-- Rules: ../../../.spec/templates/feature.md § 8 -->

Not written. Owed by `test-writer` once § 7 exists.

## 9. Traceability Matrix

<!-- Rules: ../../../.spec/templates/feature.md § 9 -->

Not written. Owed by `test-writer`. Every claim B-001 – B-026 will need a row,
and the scenarios in [`vessel-source.feature`](vessel-source.feature) are
already tagged for them.

## 10. Lessons / Spec Deltas

<!-- Rules: ../../../.spec/templates/feature.md § 10 -->

None yet.

## 11. Open Questions

<!-- Rules: ../../../.spec/templates/feature.md § 11 -->

| #   | Question                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                | Owner                                                                      | Target date                                                                      |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | -------------------------------------------------------------------------------- |
| 1   | **Answered 2026-10-15 — `fleet-pipeline` B-019 amended.** Silence is treated per source there: aircraft stay marked and kept (`fleet-pipeline` B-016, B-017), and a source whose specification claims removal, as B-021 – B-023 do, has its vehicles removed after its own threshold on the observed clock; `fleet-pipeline` B-016 now yields to that removal. Where the removal runs — a pipeline stage driven by the description, or this strategy emitting the removes below the seam — is not decided there either: `fleet-pipeline` § 11 row 9, open.              | the person                                                                 | Closed; `fleet-pipeline` § 11 row 9 before any item cut for B-021 – B-023 starts |
| 2   | `ClientWebSocket` or `AISStream.NET`? Either must negotiate `permessage-deflate` (§ 4 row 7) and expose the subscription as the first frame (B-002). Recorded as an ADR.                                                                                                                                                                                                                                                                                                                                                                                                | the person                                                                 | Before the vessel client's item starts                                           |
| 3   | The vessel silence threshold's default. Class A ships report positions every few seconds under way and every three minutes at anchor, and static data every six minutes. A default under three minutes would remove anchored ships that are still reporting.                                                                                                                                                                                                                                                                                                            | the person                                                                 | Before any item cut for B-021 – B-023 starts                                     |
| 4   | What does the refresh control do while vessels are live? It `Tell`s `DemandPoll`, and a push source has nothing to poll. Disabled, hidden, or a no-op?                                                                                                                                                                                                                                                                                                                                                                                                                  | `fleet-dashboard`                                                          | Before the closing act is rehearsed                                              |
| 5   | Where does the MID → flag-state table come from, and how much of it does the Houston box need? The ITU publishes the whole allocation, and a box over Galveston Bay sees a small part of it.                                                                                                                                                                                                                                                                                                                                                                            | the person, or `implementer` with § 7                                      | Before the description's item starts                                             |
| 6   | Which reported instant is a message's time? The documentation shows no full timestamp in `MetaData`, and `PositionReport.Timestamp` is a UTC second (0-59), not an instant. If the feed carries a full UTC time in an undocumented `MetaData` field, B-014 and B-015 use it. If it does not, a static-data or position message reports no instant, B-015 leaves the clock alone, and B-022 never fires. Then the arrival instant needs a home that is not the client (§ 4 row 11), which would amend ADR-0007. Answered by reading a captured message, not by assuming. | `implementer`, with a captured message; the person if ADR-0007 must change | Before the vessel client's item starts                                           |

## 12. Sign-off

<!-- Rules: ../../../.spec/templates/feature.md § 12 -->

| Sections | Owner       | Status   |
| -------- | ----------- | -------- |
| §§ 1-5   | spec-author | 🟡 Draft |
| §§ 6-7   | implementer | 🟡 Draft |
| §§ 8-9   | test-writer | 🟡 Draft |

## Decisions

<!-- Rules: ../../../.spec/templates/feature.md § Decisions -->

None yet. Three calls the person made on 2026-10-15 shaped §§ 1-5. Ships are the
closing act (README.md § "Open items"). A silent vessel is removed, not marked
(B-021, § 5 row 11). Vessels group by ship type and by flag state (B-019, B-020).
None of the three needed a record of its own beyond the claim it produced.

## Tasks

<!-- Rules: ../../../.spec/templates/feature.md § Tasks -->

None yet. Items are cut from § 3 once these claims are agreed.

## Scoring

<!-- Rules: ../../../.spec/templates/feature.md § Scoring -->

None yet.
