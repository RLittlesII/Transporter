---
title: "Specification: Aircraft source"
description: "Poll OpenSky for aircraft through four named layers — API types, an internal snapshot DTO, a cache that stores and diffs snapshots, and a tracker that projects the cache into domain vehicles."
type: spec
spec_status: draft
---

# Specification: Aircraft source

## 1. Business Goal

<!-- Owner: spec-author. One paragraph. The outcome, not the implementation.
     Name the failure state being removed. -->

The demo exists to break a belief: that a reactive collection needs a push feed. Its audience polls REST endpoints and runs SQL queries, so the one step they need — a full snapshot becoming a changeset — is the step every DynamicData example skips. This feature builds that step for live aircraft, and it gives each thing the step passes through a name of its own. Today one word does three jobs: the source seam's `Snapshots` hands back domain objects, while "snapshot" also means OpenSky's positional `states` row and, loosely, whatever the cache is holding. A developer who takes this repository home as the talk's artifact cannot see where the provider's payload ends, where the domain begins, or who owns the collection — so the lesson arrives as a slogan instead of a boundary they can copy. The outcome is four components with one responsibility each: API types that mirror OpenSky, an internal snapshot record that is the server's row with names on it, a cache that stores and diffs those records and does nothing else, and a tracker that projects the cache into domain vehicles. The failure state removed is a demo whose headline mechanism has no address in its own source.

## 2. User Needs

<!-- Owner: spec-author. The audience for this project is specific — see
     README.md § "Audience" — so do not write a generic persona. -->

| #   | Persona                                                                                                                  | Need                                                                                                        | Pain point today                                                                                                                                              |
| --- | ------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Line-of-business .NET developer in the talk audience, whose data lives behind REST endpoints and SQL queries (README.md § "Audience") | To see the single point where a request/response feed becomes a reactive collection, and to be able to name it | Every DynamicData sample they can find starts from a push feed or from a cache that is already full, so the step they actually need is the one nobody shows     |
| 2   | The same developer, reading this repository after the talk — the repository is the takeaway                               | A layer name that tells them which of the four things they are looking at                                   | `Snapshots` returns domain objects, which reads like a piece of a server record; the payload, the domain set and the cache's contents are all "the snapshot"    |
| 3   | The same developer, about to copy the pattern into an app whose feed also goes quiet                                      | To know how old a set of records is without asking the clock on the wall                                    | Nothing in the current seam carries "as of when", so a consumer deciding whether a row is stale invents the answer — and replay then ages items unlike live    |
| 4   | The presenter running the demo on stage (README.md § "Demo resilience")                                                   | The aircraft feed to survive a talk on a venue network, inside one day's credit budget                      | A 30-minute token expires mid-sentence, a rehearsal can spend the day's credits before the talk, and a missing credential is discovered in front of people      |
| 5   | The presenter, at the closing act (README.md § "Closing act")                                                             | The cache, the tracker and everything after them untouched when the source is swapped for ships             | If the collection lives inside whatever the current source built, swapping the source replaces the collection, and the claim the talk is making is false on stage |

## 3. Acceptance Criteria

<!-- Owner: spec-author. Numbered, falsifiable, SHALL / SHALL NOT. One claim
     per row. These ids are what § 9 and the .feature file are anchored to, so
     they are permanent: never renumbered, never reused. A withdrawn claim is
     marked Withdrawn, not deleted. -->

Thirty-six claims, in seven groups — one per layer, plus the boundary rules:
**B-001 – B-008** the API types and mapping layer 1; **B-009 – B-012** the
internal snapshot DTO; **B-013 – B-017** the source seam; **B-018 – B-024** the
polled OpenSky source; **B-025 – B-029** the cache; **B-030 – B-033**
`IFleetTracker` and mapping layer 2; **B-034 – B-036** the layer boundaries.

| ID    | Claim                                                                                                                                                                                                                                | Source                                                        | Status |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------- | ------ |
| B-001 | The converter SHALL read every field by its positional index per README.md § "Response shape", and an 18-element row SHALL populate each snapshot member from its own index.                                                          | README.md § "Response shape"                                  | Draft  |
| B-002 | A `null` element SHALL be carried as absent on the snapshot, and the converter SHALL NOT substitute `0`, `false`, an empty string, or any other default in its place.                                                                 | api-contract; mapping § "Never add"                           | Draft  |
| B-003 | A 17-element row SHALL yield a snapshot whose category is **absent**, and an 18-element row whose index 17 is `0` SHALL yield a snapshot whose category is **present with value 0**; the two SHALL be distinguishable by a consumer.  | README.md index 17; transponder-domain-model § "Aircraft"     | Draft  |
| B-004 | The snapshot's callsign SHALL have the wire's 8-character padding removed, and a callsign consisting only of padding SHALL be absent rather than an empty or whitespace string.                                                       | README.md index 1; transponder-domain-model § "Aircraft"      | Draft  |
| B-005 | The snapshot's squawk SHALL be a string and SHALL preserve leading zeros, so a wire value of `"0021"` is read as four characters and never as the number 21.                                                                          | README.md index 14; transponder-domain-model § "Aircraft"     | Draft  |
| B-006 | `sensors` (index 12) SHALL be read past deliberately and SHALL NOT appear on the snapshot, on the response envelope, or on any domain type; the exclusion SHALL be explicit in the converter rather than a side effect of nobody using it. | mapping § "Unmapped members are errors"                       | Draft  |
| B-007 | A `states` row the converter cannot read — an element count outside 17–18, an element of the wrong JSON type, or an absent `icao24` — SHALL be excluded and counted, and SHALL NOT abort deserialization of the remaining rows.        | language-ext-usage; api-contract                              | Draft  |
| B-008 | The converter SHALL be the only code that reads a `states` element by index, and SHALL produce the snapshot directly, with no intermediate positional type between the wire and the snapshot.                                         | Decided call — mapping layer 1; mapping § "Two steps"         | Draft  |
| B-009 | The snapshot SHALL have value equality over every member it carries, so two snapshots reporting identical values compare equal and the cache's differ emits no change for them.                                                       | Decided call — the cache diffs records                        | Draft  |
| B-010 | The snapshot SHALL carry `icao24` as a non-optional member and SHALL be keyed on it.                                                                                                                                                  | README.md index 0 ("the cache key")                           | Draft  |
| B-011 | The snapshot SHALL carry the wire's values in the wire's units and SHALL perform no conversion, derivation or interpretation; it is the server's record with names on it.                                                              | Decided call; mapping § "Conversions are explicit"            | Draft  |
| B-012 | The snapshot SHALL NOT carry a staleness flag, a display label, a grouping key, or any other value derived rather than reported.                                                                                                      | transponder-domain-model § "Never add"                        | Draft  |
| B-013 | `ITrackingSource` SHALL expose exactly one member yielding snapshot sets, and the repository SHALL declare exactly one such source interface.                                                                                         | api-contract § "Never add" (a second seam)                    | Draft  |
| B-014 | A snapshot set SHALL carry the complete set of snapshots the emitting source currently knows about **and** the instant that set was observed, and both SHALL be readable without consulting the source that produced it.              | Decided call — the seam makes "as of when" explicit           | Draft  |
| B-015 | A snapshot set's observed instant SHALL be supplied by the emitting source — the poll's own observation for a live source, the recorded instant for a replay — and SHALL NOT be read from an ambient clock by a consumer.             | api-mock § "Replay"; dynamic-data-pipeline § "Staleness"      | Draft  |
| B-016 | A snapshot set carrying no snapshots SHALL be a valid emission meaning "this source currently knows of no vehicles", SHALL be distinguishable from the source not having emitted, and SHALL NOT be discarded by a consumer as a non-event. | README.md § "Data source" (a box can legitimately be empty)   | Draft  |
| B-017 | `ITrackingSource` and the snapshot set SHALL NOT name a bounding box, a polling interval, a credential, a credit, an HTTP status, or any DynamicData type; a source that makes no network call SHALL satisfy the seam without stubbing any of those. | api-contract § "The seam is the whole design"                 | Draft  |
| B-018 | The bounding box and the polling interval SHALL be supplied to the OpenSky source as constructor or options input, and SHALL NOT appear on `ITrackingSource`, on the snapshot set, or on any type the cache or the tracker references. | api-contract § "The seam is the whole design"                 | Draft  |
| B-019 | When the application offers grouping by aircraft category, the OpenSky request SHALL set `extended=1`; a request without it SHALL yield snapshots whose category is absent rather than defaulted.                                     | README.md § "Data source"; api-contract                       | Draft  |
| B-020 | The OpenSky source SHALL refresh its OAuth2 token both when the current token has expired and when a request returns `401`, retrying that request once after a successful refresh; it SHALL NOT rely on expiry alone.                 | README.md § "Authentication"; api-contract                    | Draft  |
| B-021 | Every poll SHALL log the `X-Rate-Limit-Remaining` header value at debug level, and no log line, exception message, test fixture or diagnostic SHALL contain a token, `client_id` or `client_secret` value.                            | README.md § "Limits"; api-contract § "Credentials"            | Draft  |
| B-022 | A `429` response SHALL be handled as data — inspected for `X-Rate-Limit-Retry-After-Seconds` and the next poll deferred by exactly that many seconds — and SHALL NOT surface as an exception or trigger an invented backoff; every other non-2xx status SHALL remain an exception. | README.md § "Limits"; http-client § "Read the response"       | Draft  |
| B-023 | A missing OpenSky credential SHALL fail at application startup with a message naming which credential is absent, and SHALL NOT be discovered on the first poll.                                                                       | api-contract § "Credentials"                                  | Draft  |
| B-024 | A failed poll — timeout, `429`, `5xx`, or an unreadable body — SHALL NOT complete or error-terminate the seam; the source SHALL emit the next successful poll's snapshot set on the same subscription.                                | akka-actor; hot-swap-source § "Failure modes to rehearse"     | Draft  |
| B-025 | Given one snapshot set applied to the cache and then a second differing by one snapshot added, one changed and one gone, the cache's changeset stream SHALL emit exactly one add, one update and one remove, and no change for a snapshot identical across the two sets. | dynamic-data-pipeline § "Testing"; README.md § "Core idea"    | Draft  |
| B-026 | The cache SHALL store snapshots keyed on `icao24`, and SHALL NOT hold, construct, reference or return a `TransportVehicle` or any other domain type.                                                                                  | Decided call — the cache stores and diffs, nothing else       | Draft  |
| B-027 | The cache SHALL run its differ on every applied set regardless of how the emitting source obtained it, so a push source and a polled source are indistinguishable to the cache.                                                       | Decided call — push or pull does not matter to the cache      | Draft  |
| B-028 | There SHALL be exactly one cache of snapshots and exactly one snapshot changeset stream out of it; no second collection of snapshots SHALL be held by a source, an actor, the tracker, or a view model.                               | dynamic-data-pipeline § "Never add"; akka-actor               | Draft  |
| B-029 | The cache SHALL NOT read a clock, derive staleness, expire an item, or make any decision requiring the current time.                                                                                                                 | Decided call; dynamic-data-pipeline § "Staleness and expiry"  | Draft  |
| B-030 | `IFleetTracker` SHALL project the cache's snapshot changeset stream into a changeset stream of `TransportVehicle` keyed on the vehicle's key, and SHALL be the first place a domain object exists.                                    | Decided call — the tracker wraps the cache                    | Draft  |
| B-031 | The projection SHALL store each value in the unit the wire reported it in — metres, metres per second, degrees clockwise from north — and SHALL NOT convert to feet, knots, or any other display unit.                                | transponder-domain-model § "Units"; mapping § "Conversions"   | Draft  |
| B-032 | Every absent snapshot value SHALL project to `Option<T>.None`: an absent altitude SHALL NOT become sea level and an absent position SHALL NOT become latitude 0, longitude 0; and barometric and geometric altitude SHALL both survive as separate optional members. | language-ext-usage; mapping § "Optional values"                | Draft  |
| B-033 | `TransportVehicle.Key` SHALL be the snapshot's `icao24` in lowercase hex, non-optional and set on construction; and staleness SHALL derive from the snapshot's last contact against an **injected** clock, with no staleness decision reading `DateTime.UtcNow` inline. | transponder-domain-model § "TransportVehicle"; test-from-scenarios | Draft  |
| B-034 | The response envelope and the positional row SHALL be referenced only by the converter; no snapshot, seam, cache, tracker, domain type or downstream consumer SHALL reference them.                                                   | api-contract § "Never add"; transponder-domain-model          | Draft  |
| B-035 | The snapshot SHALL be referenced by the converter, the seam, the cache and the tracker's projection, and by nothing downstream of the tracker.                                                                                        | Decided call — the snapshot dies at the projection            | Draft  |
| B-036 | No consumer downstream of the tracker SHALL reference `ITrackingSource`, a snapshot, or the cache; the tracker's stream SHALL be the only route tracked state reaches the pipeline.                                                   | Decided call; hot-swap-source § "What must not be rebuilt"     | Draft  |

<!-- Status: Draft | Built | Withdrawn. "Built" means a test cites it and § 9
     says Verified. -->

## 4. Constraints

<!-- Owner: spec-author. Impact states what the constraint rules out, so § 7
     has something concrete to satisfy. Many of this project's constraints are
     the data provider's and are not ours to simplify — credit budgets, token
     expiry, blocked hyperscaler IPs (README.md). -->

| #   | Constraint                                                                                                                                                      | Source                                              | Impact                                                                                                                                                                                                                                      |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | OpenSky publishes no OpenAPI document.                                                                                                                          | README.md § "Data source"; ADR-0001                 | Rules out a generated client and any attribute-driven typed interface. The index table in README.md § "Response shape" **is** the contract; it is read, not inferred from a sample payload.                                                   |
| 2   | `states` is an array of arrays — fields are positional, not named.                                                                                              | README.md § "Response shape"                        | Rules out deserializing to a named-field type directly, and rules out Mapperly reading the payload. Forces a hand-written converter and makes the API types a separate layer rather than a convenience.                                       |
| 3   | `category` exists only when the request sets `extended=1`.                                                                                                      | README.md index 17; api-contract                    | Rules out offering category grouping without the extended request, and rules out a single "unknown category" value — absent and unknown must be two states (B-003, B-019).                                                                    |
| 4   | OAuth2 client credentials only; no basic auth; tokens last 30 minutes and can die early.                                                                        | README.md § "Authentication"                        | Rules out a token fetched once at startup, a clock-only refresh, and any call site attaching its own credential. Forces one token cache and refresh on both expiry and `401`.                                                                 |
| 5   | Credits are the budget, and cost scales with bounding-box area: ≤25 sq° = 1, up to 4 for global. A metro box at 10 s ≈ 360 credits/hour against 4,000/day.       | README.md § "Limits"                                | Rules out treating the interval as a tuning knob and rules out an unbounded poll loop. Bounding box and interval are **one** decision, and it is a budget decision — § 11 Q1. Rehearsal spends the same budget the talk does.                 |
| 6   | A `429` carries `X-Rate-Limit-Retry-After-Seconds`, and the remaining budget is in `X-Rate-Limit-Remaining`.                                                     | README.md § "Limits"                                | Rules out an invented backoff and rules out any call style that discards the response headers — so `GetJsonAsync<T>()` on a URL is unusable here. A `429` must be inspectable rather than thrown.                                              |
| 7   | OpenSky blocks AWS and other hyperscaler IPs.                                                                                                                   | README.md § "Gotchas"                               | Rules out a CI job or integration test that reaches the live API, and rules out running the demo from a cloud VM. The replay source is the network contingency, not a nice-to-have.                                                            |
| 8   | Public presentation of this data owes the OpenSky citation.                                                                                                     | README.md § "Gotchas"                               | Rules out presenting without a credit slide. Not a code constraint and not ours to simplify; tracked as a README.md open item.                                                                                                                |
| 9   | Credentials come from user secrets or environment variables, and are never committed, logged, or placed in a fixture.                                            | api-contract § "Credentials"                        | Rules out a committed fixture containing a real key, a token value in debug output, and any test that passes only when a credential is present.                                                                                               |
| 10  | There is no Gherkin runner in this repository — no Reqnroll, no bindings, no step definitions.                                                                   | AGENTS.md; spec-and-traceability                    | Rules out the `.feature` file being the executing artifact, and rules out an `@ignore` tag. "A scenario exists" never means a claim is covered; the § 9 row pointing at an xUnit test does.                                                    |
| 11  | No test may touch a network or the wall clock.                                                                                                                  | api-mock; test-from-scenarios                       | Rules out `Thread.Sleep`, a real delay, a retry-until-true, and a hand-rolled `HttpMessageHandler`. Every time-based element — poll interval, staleness, expiry — takes an injected scheduler or clock in production, not only in tests.        |
| 12  | Flurl's `HttpTest` intercepts through the logical asynchronous call context and does not follow a message into an actor on its own dispatcher.                   | ADR-0001 § "Consequences"; http-client              | Rules out testing the poller actor with `HttpTest`. Forces the HTTP mechanics into a plain class the actor calls, and the actor to be tested against a stubbed `ITrackingSource`.                                                              |
| 13  | The domain model references only the framework and LanguageExt — no DynamicData, Flurl, `HttpClient` or `System.Text.Json` types.                                | transponder-domain-model § "Never add"              | Rules out the cache or the tracker being part of the model or living inside it, which is why both are separate named components rather than members on `TransportVehicle`.                                                                     |
| 14  | The hot swap must leave the cache, the tracker, filters, comparers, groups, aggregates and bindings untouched.                                                   | hot-swap-source § "What must not be rebuilt"        | Rules out a cache owned by or constructed per source, and rules out a pipeline rebuilt when the source changes. The swap is a cache *clear* at most, never a rebuild.                                                                          |
| 15  | `DynamicData` is not in `Directory.Packages.props`; neither are AwesomeAssertions or NSubstitute. The Nuke build has no `Test` target.                           | dynamic-data-pipeline; test-from-scenarios; AGENTS.md | Rules out assuming the packages are available. The first item that builds the cache adds `DynamicData` centrally; the first item that writes a test adds the assertion and mocking packages. A green build does not mean tests ran.             |

## 5. Out of Scope

<!-- Owner: spec-author. The section nobody writes. Without it, a demo grows a
     feature nobody asked for. -->

| #   | Item                                                                                                       | Exclusion reason                                                                                                                                                                                                                 |
| --- | ---------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | The `airplanes.live` backup source                                                                         | Its access terms are unresolved and README.md § "Backup source" says to email them first. A legal open question, not a technical one. It satisfies the same seam if and when that clears; it is not wired in, stubbed, or claimed. |
| 2   | The AISStream vessel feed, `Vessel`, the WebSocket reader, and the `ClientWebSocket`-vs-`AISStream.NET` choice | The closing act and a stretch goal (README.md § "Closing act"). This feature claims only that the cache diffs every set, whatever the source (B-027) — proven with a synthetic source, not a socket. See § 11 Q5.                 |
| 3   | The replay source and the simulated source                                                                 | Separate implementations of the seam this feature defines (`api-mock`). This spec gives them a seam to satisfy and an observed instant to carry (B-015); it does not build them, and it does not record rehearsal fixtures.        |
| 4   | The source selector, the `Switch`, and the on-stage swap                                                   | `hot-swap-source` owns it. This feature claims the properties that *make* a swap possible — one cache, one stream, no provider concepts on the seam (B-017, B-028, B-036) — and stops there.                                       |
| 5   | `Filter`, `Sort`, `Group`, aggregates, `AutoRefresh` and `Bind`                                            | Everything after the tracker's stream. That stream is this feature's output and the next feature's input.                                                                                                                          |
| 6   | `ExpireAfter` and the removal policy                                                                       | B-033 claims the staleness *derivation* only. Whether a stale item is marked and kept or removed, and at what threshold, is undecided per source — § 11 Q3. Building one of the two now would silently decide it.                   |
| 7   | The grid, the detail pane, the search box, the dropdowns, the grouped view, the stale indicator's rendering, and the summary tiles | UI (`build-maui-ui`, `mvvm`). A view's obligations are not written into a scenario here, and no scenario names a UI mechanic.                                                                                                     |
| 8   | The optional map view                                                                                      | README.md § "The app" lists it as optional. Nothing in § 3 needs it, and a map is the most expensive way to prove a changeset arrived.                                                                                             |
| 9   | A generated OpenSky client, or an OpenAPI document written by us to generate from                          | Rejected in ADR-0001 and foreclosed by Constraint 1. Writing a specification for someone else's undocumented API to feed a generator is a project, not a step.                                                                     |
| 10  | A Gherkin runner, Reqnroll, step definitions, or bindings                                                  | Constraint 10. Scenarios are documentation. A runner would make the `.feature` file executable and move the coverage gate off § 9, which is where AGENTS.md puts it.                                                               |
| 11  | Unit conversion to feet, knots or local time, and any display formatting of an `Option<T>`                 | A display concern (transponder-domain-model § "Units"). SI is canonical in the model (B-031); conversion happens at the view in a named method.                                                                                    |
| 12  | OpenSky's `sensors` field (index 12)                                                                       | Explicitly excluded by B-006. Recorded as a decision rather than left as an omission, so a later reader does not add it believing it was overlooked.                                                                               |
| 13  | A second source seam, and any source-describing metadata — display name, which columns make sense, an icon | `api-contract` forbids widening the interface for this. A source that genuinely needs to describe itself gets a separate small type in a separate feature, not a member added here.                                                 |
| 14  | Persisting snapshots, snapshot history, or a track per aircraft                                             | The cache holds current state. A history store is a second collection, which dynamic-data-pipeline § "Never add" rules out, and nothing in § 3 or README.md asks for a trail.                                                      |
| 15  | Registering the OpenSky account, creating the API client, and provisioning the credentials                 | Operational tasks tracked in README.md § "Open items". B-023 claims the application's *behavior* when a credential is absent; obtaining one is not code.                                                                           |
| 16  | The poller actor's supervision strategy, its registration wiring, and whether a view model uses `Tell` or `Ask` | `akka-actor` and `mvvm`. This spec claims the source's observable behavior (B-018 – B-024), not its hosting. `Tell`-vs-`Ask` is a repo-wide undecided listed in spec-and-traceability.                                             |
| 17  | A `Test` target in the Nuke build                                                                          | A repo-wide undecided (spec-and-traceability § "Decisions this demo still owes a record"). Deciding it inside a feature specification would settle a build convention by side effect.                                               |

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

**Domain model**

| Field     | Type     | Notes     |
| --------- | -------- | --------- |
| {{field}} | {{type}} | {{notes}} |

**Diagrams**

<!-- Mermaid (AGENTS.md). Declare every type; one that does not apply says
     `Not applicable — <reason>` rather than being silently dropped. Never
     summarize a diagram into prose and delete the diagram. -->

```mermaid
{{diagram}}
```

**Interface changes**

{{interface_changes}}

**Decision required**

> | Option | Summary     | Tradeoff     |
> | ------ | ----------- | ------------ |
> | A.     | {{summary}} | {{tradeoff}} |
>
> **Recommendation:** {{recommendation}}
> **Awaiting:** {{decision_owner}}

<!-- Omit the block when nothing is open, but say so: "No open decisions." -->

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

| #   | Question                                                                                                                                                                                                                                                                                                                                                                 | Owner                   | Target date                      |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------- | -------------------------------- |
| 1   | What is the bounding box, and what is the polling interval? One credit-budget decision, not two tuning knobs: cost scales with box area and a metro box at 10 s burns ≈ 360 credits/hour against 4,000/day, so the pair decides how long the demo runs and how much rehearsal the budget affords. Blocks a concrete value behind B-018 and the Constraint 5 figures.       | Project owner           | Before the first rehearsal recording |
| 2   | What does the cache do on a source swap — clear and refill, or let expiry drain the outgoing set? `hot-swap-source` recommends clear-and-refill and calls it a stage effect, not a correctness call. It changes what the audience sees and nothing about B-025 – B-029, so it is deliberately unclaimed here.                                                             | Project owner           | Rehearsal                        |
| 3   | Is a stale item **marked and kept** or **expired and removed**, and per source? B-033 claims only the derivation; the policy and its threshold are why `ExpireAfter` is § 5 row 6. Note that B-029 puts this decision downstream of the cache, which narrows where it can live.                                                                                            | Project owner           | Before the pipeline feature is specified |
| 4   | Where does a live poll's observed instant come from — the `/states/all` response's own reported time, or the client's observation taken from the injected clock? B-015 claims the instant is *supplied by the source*, which is the decided part. Which of the two OpenSky supplies is not, and README.md documents only the `states` array, so claiming a `time` field would be inventing a requirement. | spec-author             | Before an item is cut from § 3   |
| 5   | How does a push source produce a full set without holding a second collection? B-027 makes the cache diff every applied set, so a push feed must accumulate its own known set — which brushes dynamic-data-pipeline § "Never add". Deferred deliberately: the AISStream feed is § 5 row 2 and this specification makes no claim either way.                                | Project owner           | With the closing-act feature     |

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

None yet. The four-layer call this specification is written against is a
cross-cutting technical decision, so it is recorded in the repository-wide
[ADR-0002](../../../.spec/adr/0002-four-layers-wire-to-fleet.md) rather than
here or in this Feature's `adr/`.

## Tasks

<!-- Owner: spec-author. The items cut from § 3 once the claims exist. Ids
     only — never restated titles, or the two records disagree. "None yet."
     is valid while the spec is still being agreed. -->

None yet.

## Scoring

<!-- Owner: spec-author, recording the item's value and risk when they change.
     The authoritative values live in the .issues/ item. -->

| Date         | Field | From | To  | Rationale     |
| ------------ | ----- | ---- | --- | ------------- |

No scoring recorded — no `.issues/` item has been cut from § 3 yet.
