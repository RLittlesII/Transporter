---
title: "Specification: Aircraft source"
description: "Poll OpenSky through a typed API contract, cache snapshots per client, project them to domain vehicles in a per-type strategy, and swap strategies behind a decorator the fleet tracker wraps."
type: spec
spec_status: draft
---

# Specification: Aircraft source

## 1. Business Goal

<!-- Rules: ../../../.spec/templates/feature.md § 1 -->

The demo exists to break a belief: that a reactive collection needs a push feed. Its audience polls REST endpoints and runs SQL queries, so the one step they need — a full snapshot becoming a changeset — is the step every DynamicData example skips. This feature builds that step for live aircraft, and it gives every thing the step passes through one responsibility and one name. Today a single interface carries the provider's payload, the domain set and the collection all at once, which is why the word "snapshot" means three things in conversation: `Snapshots` hands back domain objects even though a snapshot is the record you map *into* a domain object. Worse, the thing that actually varies between an aircraft feed and a vessel feed — how a provider's record becomes a `TransportVehicle` — has no owner at all, so it ends up smeared across whichever component happens to be holding both types. The outcome is a typed API contract that mirrors what OpenSky publishes, a client that caches what it fetches, a per-type projection that is the only place a domain object is built, and a swap that happens behind a decorator rather than in the pipeline. The failure state removed is a demo whose headline mechanism and whose extension point both have no address in its own source.

## 2. User Needs

<!-- Rules: ../../../.spec/templates/feature.md § 2 -->

| #   | Persona                                                                                                                              | Need                                                                                                      | Pain point today                                                                                                                                                 |
| --- | ------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Line-of-business .NET developer in the talk audience, whose data lives behind REST endpoints and SQL queries (README.md § "Audience") | To see the single point where a request/response feed becomes a reactive collection, and to be able to name it | Every DynamicData sample starts from a push feed or from a cache that is already full, so the step they actually need is the one nobody shows                      |
| 2   | The same developer, reading this repository after the talk — the repository is the takeaway                                           | A name per layer, so a signature tells them which one they are in                                         | One interface returns domain objects but is named for snapshots; the payload, the cached record and the domain object are all "the snapshot" in conversation      |
| 3   | The same developer, whose own app will grow a second data source                                                                     | One obvious place to add a source, and nothing else to touch                                              | With no owner for "provider record becomes domain object", a second source means editing whatever currently holds both types                                      |
| 4   | The same developer, whose own API is versioned and will change under them                                                             | A typed contract they can version and fake without HTTP                                                   | A loose DTO plus a converter leaves nothing to version and nothing a test can substitute except the HTTP transport                                               |
| 5   | The presenter running the demo on stage (README.md § "Demo resilience")                                                              | The aircraft feed to survive a talk on a venue network, inside one day's credit budget                     | A 30-minute token expires mid-sentence, a rehearsal can spend the day's credits before the talk, and a missing credential is discovered in front of people        |
| 6   | The presenter, at the closing act (README.md § "Closing act")                                                                        | To swap planes for ships without the grid, the filters or the bindings noticing                           | If the swap is wired through the pipeline, the pipeline knows which source is live, and the claim the talk is making is false on stage                            |

## 3. Acceptance Criteria

<!-- Rules: ../../../.spec/templates/feature.md § 3 -->

Fifty-one claims, in nine groups — one per component, plus the boundary rules:
**B-005 – B-010, B-048 and B-049** the API contract; **B-001 – B-004** the API
types and the envelope; **B-011 – B-014** the snapshot; **B-015 – B-029 and
B-050** the snapshot client; **B-030 – B-032** the cache; **B-033 – B-037** the
tracker source strategy; **B-038 – B-040** the swap decorator; **B-041 – B-044
and B-051** the fleet tracker; **B-045 – B-047** the layer boundaries.

B-048 – B-051 are out of numeric order because they were added after the rest
were written. Ids are permanent, so they keep the numbers they were given rather
than being slotted into the sequence.

| ID    | Claim                                                                                                                                                                                                                       | Source                                                    |
| ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| B-001 | The response envelope SHALL mirror what OpenSky sends — its reported time and its `states` rows — named as the provider names them.                                                                                          | Decided call — the contract mirrors the API                |
| B-002 | The envelope's `states` SHALL remain the provider's positional shape; no member of the envelope SHALL be a named per-aircraft type.                                                                                          | README.md § "Response shape"; decided call                 |
| B-003 | The envelope's reported time SHALL be the observed instant for everything downstream, and no consumer SHALL read an ambient clock to supply one.                                                                             | api-mock § "Replay"; dynamic-data-pipeline § "Staleness and expiry"  |
| B-004 | The positional row type SHALL NOT be visible outside the integration code; no domain type, cache, strategy or view SHALL be able to name it.                                                                                 | domain-model § "Never add"                     |
| B-005 | The API contract SHALL declare one method per external endpoint, returning `Task<T>`, with `CancellationToken` as the last parameter.                                                                                        | api-contract (global) § scrutiny                           |
| B-006 | The API contract SHALL NOT name an `IObservable`, a cache, a changeset, a bounding box, a polling interval or a credential.                                                                                                  | api-contract (global); decided call                        |
| B-007 | Exactly one production class SHALL implement the API contract **per transport** — one reaching the provider over HTTP, one reading a recording — and each SHALL be `internal sealed` with **explicit interface implementation** on every method and no `public` endpoint method. A second implementation for a transport that already has one SHALL NOT exist. | api-contract (global) § scrutiny; see § 4 row 20           |
| B-008 | No implementing class's type SHALL be resolvable from outside the integration code; dependency injection SHALL alias the contract to one implementation **per constructed chain**, so a consumer names the contract and never selects between implementations.                                                                | api-contract (global) § DI registration; see § 4 row 20    |
| B-009 | A new OpenSky API version SHALL produce a new contract interface; an existing contract interface SHALL NOT be edited once a class implements it.                                                                             | api-contract (global) § versioning                         |
| B-010 | The API contract's test double SHALL be a hand-written fake that throws naming the unset response, and SHALL NOT be produced by a mocking framework.                                                                        | api-contract (global) § testing; see § 4 row 18            |
| B-048 | The API contract SHALL carry no version suffix and SHALL have no marker interface above it for as long as OpenSky publishes no API version; the versioned layer SHALL be introduced only when the provider declares a version to be agnostic about. | Decided call; see § 4 row 3                                |
| B-049 | A contract layer SHALL exist only where the provider is request/response shaped; a push provider's strategy SHALL have none, and no contract SHALL be invented to give a socket one. The surface every strategy shares SHALL be `ITrackerSource` and nothing above it. | Decided call; see § 4 row 4                                |
| B-050 | The polling interval SHALL be configurable and SHALL default to 15 seconds; the bounding box SHALL be configurable with no default compiled in.                                                                                                                      | Decided call; decisions/0001                               |
| B-051 | A vehicle past the staleness threshold SHALL remain in the collection and SHALL be observably stale; it SHALL NOT be removed for staleness. The threshold SHALL be configurable and SHALL default to five minutes.                                                   | Decided call; dynamic-data-pipeline § "Staleness and expiry"          |
| B-011 | The snapshot SHALL have value equality over every member it carries, so two snapshots reporting identical values compare equal and the differ emits no change for them.                                                      | Decided call — the client diffs records                    |
| B-012 | The snapshot SHALL carry `icao24` as a non-optional member and SHALL be keyed on it.                                                                                                                                        | README.md index 0 ("the cache key")                        |
| B-013 | The snapshot SHALL carry the wire's values in the wire's units and SHALL perform no conversion, derivation or interpretation; it is the server's record with names on it.                                                     | Decided call; mapping § "Conversions are explicit, never implicit"         |
| B-014 | The snapshot SHALL NOT carry a staleness flag, a display label, a grouping key, or any other value derived rather than reported.                                                                                             | domain-model § "Never add"                     |
| B-015 | The snapshot client SHALL take the API contract and its cache by constructor, and SHALL construct neither.                                                                                                                   | Decided call — constructor injection                       |
| B-016 | The snapshot client SHALL read every field by its positional index per README.md § "Response shape", and an 18-element row SHALL populate each snapshot member from its own index.                                            | README.md § "Response shape"                               |
| B-017 | A `null` element SHALL be carried as absent on the snapshot, and SHALL NOT be substituted with `0`, `false`, an empty string, or any other default.                                                                          | mapping § "Never add"                                      |
| B-018 | A 17-element row SHALL yield a snapshot whose category is **absent**, and an 18-element row whose index 17 is `0` SHALL yield a snapshot whose category is **present with value 0**; the two SHALL be distinguishable.       | README.md index 17; domain-model               |
| B-019 | The snapshot's callsign SHALL have the wire's 8-character padding removed, and a callsign consisting only of padding SHALL be absent rather than empty or whitespace.                                                        | README.md index 1                                          |
| B-020 | The snapshot's squawk SHALL be a string and SHALL preserve leading zeros, so a wire value of `"0021"` is read as four characters and never as the number 21.                                                                  | README.md index 14                                         |
| B-021 | `sensors` (index 12) SHALL be read past deliberately and SHALL NOT appear on the snapshot or on any domain type; the exclusion SHALL be explicit rather than a side effect of nobody using it.                               | mapping § "Unmapped members are errors"                    |
| B-022 | A row the client cannot read — an element count outside 17–18, an element of the wrong type, or an absent `icao24` — SHALL be excluded and counted, and SHALL NOT abort the remaining rows.                                  | language-ext-usage                                          |
| B-023 | The snapshot client SHALL write every fetched set to its cache as a differential update over the whole set, and SHALL expose the resulting snapshot changeset stream.                                                        | Decided call — the client diffs; README.md § "Core idea"   |
| B-024 | The bounding box and the polling interval SHALL be supplied to the snapshot client as constructor or options input, and SHALL NOT appear on the API contract, on its own stream, or on anything downstream.                  | api-contract § "The traps"                                  |
| B-025 | When the application offers grouping by aircraft category, the request SHALL set `extended=1`; a request without it SHALL yield snapshots whose category is absent rather than defaulted.                                     | README.md § "Data source"; links B-018                     |
| B-026 | The snapshot client SHALL refresh its OAuth2 token both when the current token has expired and when a request returns `401`, retrying that request once after a successful refresh; it SHALL NOT rely on expiry alone.       | README.md § "Authentication"                               |
| B-027 | Every poll SHALL log the `X-Rate-Limit-Remaining` header value at debug level, and no log line, exception message, test fixture or diagnostic SHALL contain a token, `client_id` or `client_secret` value.                   | README.md § "Limits"; api-contract § "Credentials"         |
| B-028 | A `429` response SHALL be handled as data — inspected for `X-Rate-Limit-Retry-After-Seconds` and the next poll deferred by exactly that many seconds — and SHALL NOT surface as an exception or trigger an invented backoff; every other non-2xx status SHALL remain an exception. | README.md § "Limits"; flurl-http-client                          |
| B-029 | A missing OpenSky credential SHALL fail at application startup with a message naming which credential is absent; and a failed poll — timeout, `429`, `5xx`, or an unreadable body — SHALL NOT complete or error-terminate the client's stream. | api-contract § "Credentials"; hot-swap-source               |
| B-030 | The cache SHALL be a plain keyed store of snapshots: no diff policy of its own, no projection, and no clock.                                                                                                                 | Decided call — the cache is dumb                           |
| B-031 | There SHALL be one cache per client, typed to that client's snapshot, and its lifetime SHALL be the application's rather than the client's.                                                                                  | Decided call; hot-swap-source § "What must not be rebuilt" |
| B-032 | The cache SHALL NOT hold, construct, reference or return a `TransportVehicle` or any other domain type.                                                                                                                      | Decided call                                               |
| B-033 | `ITrackerSource` SHALL declare the `TransportVehicle` changeset stream, so every strategy is substitutable through it without a cast.                                                                                        | Decided call — the strategy seam                           |
| B-034 | The aircraft tracker source SHALL adhere to `ITrackerSource` and SHALL own the Mapperly projection from snapshot to `Aircraft`; this SHALL be the first place a domain object exists.                                        | Decided call; mapping § "One mapper per boundary"          |
| B-035 | The projection SHALL store each value in the unit the wire reported it in — metres, metres per second, degrees clockwise from north — and SHALL NOT convert to feet, knots, or any display unit.                             | ADR-0005 item 7; domain-model                   |
| B-036 | Every absent snapshot value SHALL project to `Option<T>.None`: an absent altitude SHALL NOT become sea level and an absent position SHALL NOT become latitude 0, longitude 0; and barometric and geometric altitude SHALL both survive as separate optional members. | language-ext-usage; mapping § "Optional values"             |
| B-037 | `TransportVehicle.Key` SHALL be the snapshot's `icao24` in lowercase hex, non-optional and set on construction; and a per-type tracker source interface SHALL NOT widen the seam with source-describing members.            | domain-model; api-contract § "Never add"       |
| B-038 | A decorator registered as `ITrackerSource` SHALL select the live strategy at runtime, and there SHALL be no strategy-resolver type that callers ask which strategy to use.                                                    | Decided call — decorator, not resolver                     |
| B-039 | The decorator SHALL be substitutable for any strategy it wraps, and no consumer SHALL be able to observe from its stream that a swap occurred.                                                                                | hot-swap-source § "The mechanism"                          |
| B-040 | Swapping SHALL stop the outgoing source, so a swapped-out poller stops spending OpenSky credits and a swapped-out socket stops reading.                                                                                       | hot-swap-source § "Disposal discipline"                    |
| B-041 | `IFleetTracker` SHALL wrap `ITrackerSource` and SHALL be what view models depend on; no view model SHALL depend on a strategy, a client, a cache or the decorator.                                                            | Decided call; mvvm                                         |
| B-042 | `IFleetTracker` SHALL own the collection pipeline — filtering, sorting, grouping, aggregates, property-change refresh, expiry and binding — and that pipeline SHALL be constructed once and SHALL NOT be rebuilt because the live source changed. | Decided call; hot-swap-source § "What must not be rebuilt" |
| B-043 | `IFleetTracker` SHALL own the injected clock: staleness SHALL derive from a vehicle's last contact against it, and no staleness or expiry decision anywhere SHALL read `DateTime.UtcNow` inline.                              | dynamic-data-pipeline § "Staleness and expiry"; test-from-scenarios   |
| B-044 | No code SHALL add to or remove from a bound collection imperatively; every change SHALL arrive through the pipeline `IFleetTracker` owns.                                                                                     | dynamic-data-pipeline § "Never add"                        |
| B-045 | The response envelope and the positional row SHALL be referenced only by the class implementing the API contract and by the snapshot client.                                                                                 | api-contract § "Never add"                                 |
| B-046 | The snapshot SHALL be referenced by the snapshot client, its cache, and its tracker source's projection, and by nothing downstream of that projection.                                                                        | Decided call — the snapshot dies at the projection         |
| B-047 | No consumer downstream of `IFleetTracker` SHALL reference an API contract, a client, a cache, a snapshot, or a concrete `ITrackerSource`.                                                                                     | Decided call; hot-swap-source                              |

## 4. Constraints

<!-- Rules: ../../../.spec/templates/feature.md § 4 -->

| #   | Constraint                                                                                                                                                | Source                                   | Impact                                                                                                                                                                                                                      |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | OpenSky publishes no OpenAPI document.                                                                                                                    | README.md § "Data source"; ADR-0001      | Rules out a generated client and an attribute-driven typed interface. The index table in README.md § "Response shape" **is** the contract; it is read, not inferred from a sample payload.                                     |
| 2   | `states` is an array of arrays — fields are positional, not named.                                                                                        | README.md § "Response shape"             | Rules out Mapperly at the wire boundary and rules out deserializing straight to a named per-aircraft type. The rows-to-snapshot mapping is hand-written; only snapshot-to-domain is Mapperly's.                                |
| 3   | OpenSky publishes no API version — there is no `/v1/` in the path.                                                                                        | README.md § "Data source"                | Rules out a version suffix, because a suffix matches the *provider's* major version and there is none to match; an invented `V1` would be the internal version number the pattern forbids. The contract therefore has no suffix and no marker above it until OpenSky declares a version (B-048). |
| 4   | A WebSocket does not fit one `Task<T>` per endpoint. Subscribe-then-receive has no request to return a response.                                           | ais-stream; api-contract (global)        | Rules out a contract layer for a push provider, and rules out inventing a fake one to make the strategies look alike (B-049). The surface they share is `ITrackerSource`, which is why the seam sits at the domain boundary.     |
| 5   | Integration code does not live under `Features/`. `AGENTS.md` § "Code Conventions" governs feature code and is silent on integrations.                     | Decided call; AGENTS.md                  | Rules out a contract, a client or a cache inside `src/Transponder/Features/<FeatureName>/`. Integrations get their own root, so a provider's code is not mistaken for one feature's — see `coding-conventions`.                 |
| 6   | `category` exists only when the request sets `extended=1`.                                                                                                | README.md index 17                       | Rules out offering category grouping without the extended request, and rules out a single "unknown category" value — absent and unknown are two states (B-018, B-025).                                                        |
| 7   | OAuth2 client credentials only; no basic auth; tokens last 30 minutes and can die early.                                                                   | README.md § "Authentication"             | Rules out a token fetched once at startup, a clock-only refresh, and any call site attaching its own credential. Forces one token cache and refresh on both expiry and `401`.                                                  |
| 8   | Credits are the budget, and cost scales with bounding-box area: ≤25 sq° = 1, up to 4 for global. The registered free tier is 4,000 per day.                 | README.md § "Limits"                     | Rules out an unbounded poll loop, and rules out a box large enough to leave the 1-credit tier. At the chosen 15-second interval (B-050) a ≤25 sq° box costs 240 credits/hour, so a talk spends ~180 of 4,000 and rehearsal is unconstrained — credits bound the *box*, not the interval. |
| 9   | A `429` carries `X-Rate-Limit-Retry-After-Seconds`; remaining budget is in `X-Rate-Limit-Remaining`.                                                       | README.md § "Limits"                     | Rules out an invented backoff and any call style that discards response headers — so `GetJsonAsync<T>()` on a URL is unusable. A `429` must be inspectable rather than thrown.                                                 |
| 10  | OpenSky blocks AWS and other hyperscaler IPs.                                                                                                             | README.md § "Gotchas"                    | Rules out a CI job or test that reaches the live API, and rules out running the demo from a cloud VM. The replay strategy is the network contingency, not a nice-to-have.                                                      |
| 11  | Public presentation of this data owes the OpenSky citation.                                                                                                | README.md § "Gotchas"                    | Rules out presenting without a credit slide. Not a code constraint and not ours to simplify; tracked as a README.md open item.                                                                                                |
| 12  | Credentials come from user secrets or environment variables, and are never committed, logged, or placed in a fixture.                                      | api-contract § "Credentials"             | Rules out a committed fixture with a real key, a token in debug output, and any test that passes only when a credential is present.                                                                                            |
| 13  | There is no Gherkin runner in this repository — no Reqnroll, no bindings, no step definitions.                                                              | AGENTS.md; spec-and-traceability         | Rules out the `.feature` file being the executing artifact, and rules out an `@ignore` tag. "A scenario exists" never means a claim is covered; the § 9 row pointing at an xUnit test does.                                    |
| 14  | No test may touch a network or the wall clock.                                                                                                             | api-mock; test-from-scenarios            | Rules out `Thread.Sleep`, a real delay, a retry-until-true, and a hand-rolled `HttpMessageHandler`. Every time-based element takes an injected scheduler or clock in production, not only in tests.                             |
| 15  | Flurl's `HttpTest` intercepts through the logical asynchronous call context and does not follow a message into an actor on its own dispatcher.             | ADR-0001 § "Consequences"                | Rules out testing a poller actor with `HttpTest`. The contract's implementation stays a plain class tested directly — and the hand-written contract fake (B-010) is what lets everything above it be tested with no HTTP at all. |
| 16  | The domain model references only the framework and LanguageExt — no DynamicData, Flurl, `HttpClient` or `System.Text.Json` types.                           | domain-model § "Never add"   | Rules out the cache, the strategies or the tracker living inside the model. It is why each is a separate component rather than a member on `TransportVehicle`.                                                                 |
| 17  | A package is available only once it is in `Directory.Packages.props`, and the build runs only the targets it declares.                                      | dynamic-data-pipeline; ADR-0003; `nuke-build` | Rules out assuming a package is referenced: the first item that needs one adds it centrally, never pinned in a `.csproj`. Read the file and the build rather than this row — which packages and targets exist today is repository state, not a constraint. A green build proves only that the declared targets ran. |
| 18  | The versioned-contract pattern bans a mocking framework for the contract's test double; this repository's conventions mandate NSubstitute (`transponder-conventions`).                                | api-contract (global) § scrutiny; `transponder-conventions` | A narrow conflict, resolved narrowly: the API contract's double is hand-written (B-010); NSubstitute remains correct for everything else. Neither document is wrong; the scope of each differs.                                 |
| 19  | The versioned-contract pattern is silent on caching, observables and streaming, and defines no layer above the contract.                                    | api-contract (global)                    | Rules out claiming conformance for the client, the cache, the strategies, the decorator or the tracker. Only B-005 – B-010 are governed by it; everything above is this repository's own design and says so.                   |
| 20  | Replay must be selectable at the `ITrackerSource` seam, and a recording is the provider's own envelope written verbatim.                                     | api-mock § "Replay"; hot-swap-source; ADR-0004 | Rules out the pattern's "exactly one production class" for the contract: replay substitutes *at the contract*, so a second implementation reads the recording and the same client, cache and projection sit above it unchanged. B-007 is therefore one implementation **per transport** and B-008 aliases per constructed chain. A narrow, deliberate departure from the global pattern, recorded here rather than discovered — the same treatment row 18 gives the NSubstitute conflict. The alternative, a replay client of its own, would have required widening B-045 and keeping a second positional row reader correct. |

## 5. Out of Scope

<!-- Rules: ../../../.spec/templates/feature.md § 5 -->

| #   | Item                                                                                                             | Exclusion reason                                                                                                                                                                                                                       |
| --- | ---------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | The pipeline operators themselves — filtering, sorting, grouping, aggregates, property-change refresh, expiry, binding | B-042 claims only that `IFleetTracker` **owns** them and that they are built once. Building them is the next feature. The line matters: ownership is claimed here, behavior is not.                                                      |
| 2   | The `airplanes.live` backup source                                                                               | Its access terms are unresolved and README.md § "Backup source" says to email them first. A legal open question, not a technical one. It gets its own contract and strategy if and when that clears.                                     |
| 3   | The AISStream vessel feed, `Vessel`, and the vessel client, cache and tracker source                             | The closing act and a stretch goal. This feature claims the shape a second strategy plugs into (B-033, B-038, B-049) and nothing about the vessel feed itself. Note it has **no** contract layer: a socket does not fit one `Task<T>` per endpoint (§ 4 row 4). |
| 4   | The replay and simulated strategies                                                                              | Separate implementations of the seam this feature defines. This spec gives them a seam and an observed instant to carry (B-003); it does not build them or record rehearsal fixtures. Replay now has its own specification — [`features/replay-source`](../../replay-source/.spec/README.md) — and it substitutes at the API contract, which is why B-007 is one implementation per transport (§ 4 row 20). The simulated strategy stays a forward reference with no spec yet. |
| 5   | The swap control, the stage choreography, and in-flight-result cancellation on swap                              | `hot-swap-source` owns them. This feature claims the decorator's obligations (B-038 – B-040) and stops there; no selector UI, no disposal choreography.                                                                                  |
| 6   | `ExpireAfter` and any removal-for-staleness behavior                                                             | B-051 decides it the other way: a stale aircraft is **marked and kept**, never removed. `ExpireAfter` therefore has no role in the aircraft demo and is not built here. Vessels may still want it — they go silent rather than departing — and that belongs to the closing act. |
| 7   | The grid, the detail pane, the search box, the dropdowns, the grouped view, the stale indicator's rendering, and the summary tiles | UI (`maui-ui`, `mvvm`). A view's obligations are not written into a scenario here, and no scenario names a UI mechanic.                                                                                                            |
| 8   | The optional map view                                                                                            | README.md § "The app" lists it as optional. Nothing in § 3 needs it, and a map is the most expensive way to prove a changeset arrived.                                                                                                   |
| 9   | A generated OpenSky client, or an OpenAPI document written by us to generate from                                 | Rejected in ADR-0001 and foreclosed by Constraint 1. Writing a specification for someone else's undocumented API to feed a generator is a project, not a step.                                                                           |
| 10  | A Gherkin runner, Reqnroll, step definitions, or bindings                                                        | § 4 row 13. Scenarios are documentation. A runner would make the `.feature` file executable and move the coverage gate off § 9, which is where AGENTS.md puts it.                                                                     |
| 11  | Unit conversion to feet, knots or local time, and display formatting of an `Option<T>`                           | A display concern (ADR-0005 item 7). SI is canonical in the model (B-035); conversion happens at the view in a named method.                                                                                           |
| 12  | OpenSky's `sensors` field (index 12)                                                                             | Explicitly excluded by B-021. Recorded as a decision rather than left as an omission, so a later reader does not add it believing it was overlooked.                                                                                      |
| 13  | A second strategy seam, and any source-describing metadata — display name, which columns make sense, an icon      | B-037 forbids widening a per-type interface for this. A source that genuinely needs to describe itself gets a separate small type in a separate feature.                                                                                  |
| 14  | Persisting snapshots, snapshot history, or a track per aircraft                                                  | Each cache holds current state. A history store is a second collection, which dynamic-data-pipeline § "Never add" rules out, and nothing in § 3 or README.md asks for a trail.                                                            |
| 15  | Registering the OpenSky account, creating the API client, and provisioning the credentials                       | Operational tasks tracked in README.md § "Open items". B-029 claims the application's *behavior* when a credential is absent; obtaining one is not code.                                                                                  |
| 16  | The poller's hosting — actor shape, supervision, registration, and whether a view model uses `Tell` or `Ask`      | `akka-actor` and `mvvm`. This spec claims the client's observable behavior (B-015 – B-029), not where it runs. `Tell`-vs-`Ask` is a repository-wide undecided, open at README.md § "Open items".                                                  |
| 17  | A `Test` target in the Nuke build                                                                                | A repository-wide undecided, open at README.md § "Open items". Deciding it inside a feature specification would settle a build convention by side effect.                                                     |

## 6. Concern Separation

<!-- Rules: ../../../.spec/templates/feature.md § 6 -->

One row per concern, not per claim. Fifty-one rows would be § 3 with a column
bolted on, and a second copy of a claim drifts from the first; instead each row
names in its Notes the claim ids it answers for, so coverage is checked by
reading the ids down the column rather than by counting rows. The rows follow
§ 3's nine groups.

How the three classifications are applied here: **Business** where the shape
could have gone either way technically and a product judgment picked it;
**Technical** where no business party expressed a preference and a constraint or
a skill decided it; **Both** where a technical shape was chosen for a business
reason § 1 or § 2 names. The last is the column that earns the table — § 4 rows
18 and 20 are each a departure from the global contract pattern made for a
reason the business goal states, and reading either as purely technical loses
why it was allowed.

| Item | Classification | Notes |
| ---- | -------------- | ----- |
| One name per layer — envelope, snapshot, domain vehicle | Both | § 2 need 2: a signature must tell the reader which layer they are in, and today "snapshot" means three things. Three distinct types is the technical answer to a comprehension problem, not to a correctness one. B-001, B-011, B-034. |
| The provider's positional shape is a transport detail | Technical | § 4 row 2 and `domain-model` § "Never add". Nobody outside the code has a stake in where the array dies. B-002, B-004. |
| The observed instant comes from the provider, never a clock | Both | § 2 need 5: on stage the fallback must age aircraft the way the live feed does, or the recording is visibly not live. ADR-0004 rules out the arrival time as the answer. B-003. **Unresolved — see § 7 "Decision required" and § 11.** |
| A typed contract that can be versioned and faked without HTTP | Both | § 2 need 4: the audience's own APIs are versioned and will change under them. § 4 row 15 makes the contract the only seam a test can use at all, since `HttpTest` cannot follow a message into an actor. B-005, B-006, B-009. |
| The contract carries no version the provider never published | Technical | § 4 row 3 is the whole argument, and it is a reading of someone else's rule against this provider rather than a judgment anyone outside the code has a stake in. B-048. |
| A contract layer only where the provider is request/response shaped | Technical | § 4 row 4. The temptation it resists — making the two strategies look alike — is a presentation concern, and it is the last row of this table rather than this one. B-049. |
| The contract's double is hand-written, not generated | Both | § 4 row 18. Technically a narrow conflict between two documents whose scopes differ. The business reason it resolves toward the hand-written fake: § 2 need 2 makes the repository the takeaway, and a double that returns `default` teaches the reader that a passing test means something it does not. B-010. |
| One contract implementation per transport | Both | § 4 row 20. The business reason is § 2 need 5 — replay exists so a talk survives a venue network, and for no technical reason at all. The technical consequence is that substitution happens at the deepest layer that exists, which keeps B-045 from widening and keeps one positional-row reader in the repository. B-007, B-008. |
| The snapshot is the server's record, not an interpretation of it | Technical | `mapping` § "Conversions are explicit"; `domain-model` § "Never add". B-012, B-013, B-014. |
| A full snapshot becoming a changeset has an address in the source | Both | § 1: this is the step the audience needs and every DynamicData sample skips, so the feature exists to give it a name. The technical answer — the writer owns the write, `dynamic-data-pipeline` — would be the same even if nobody were watching. B-015, B-023. |
| The cache stores and does nothing else | Technical | Three negative claims and no product stake. `dynamic-data-pipeline`. B-030, B-031, B-032. |
| Absent is not zero, and absent is not present-zero | Both | Technically `Option<T>` at the wire boundary. Why it matters more here than in most code: a wrong answer renders as plausible data — an aircraft at sea level, or one in the Gulf of Guinea — in front of a room, so the failure is invisible exactly when it is most expensive. B-017, B-018, B-036. |
| Grouping by category is what makes the request extended | Both | § 2 need 1 and README.md § "The app" ask for the grouped view; § 4 row 6 says `extended=1` is how the field arrives and that its absence is not a zero. B-025. |
| The wire's encodings are undone at the read, and visibly | Technical | Each field comes from its own positional index, and callsign padding and squawk's leading zeros are the provider's encoding; `mapping` § "Conversions are explicit" requires the undoing to be a named step rather than a side effect. B-016, B-019, B-020. |
| Canonical units are canonical in the model | Technical | ADR-0005 item 7. The model stores what the wire reported — metres, metres per second, degrees from north — and conversion to anything a reader prefers is a view concern (§ 5 row 11). B-035. |
| `sensors` is excluded on the record, not by disuse | Technical | `mapping` § "Unmapped members are errors", and § 5 row 12 records it so a later reader does not restore it believing it was overlooked. B-021. |
| One unreadable row costs one row | Both | Technically a per-row result and a count. The business reason: the grid keeps its other aircraft on stage, which is the difference between a blemish and a dead demo. B-022. |
| The box, the interval, and the credit budget | Both | § 2 need 5 — one day's credits, and a rehearsal must not spend the talk's. § 4 row 8 is the technical half: credits bound the *box*, not the interval, which is the opposite of the intuition. B-024, B-050. |
| Houston specifically | Business | `decisions/0001`: chosen for the variety the grouped view needs, and so the vessel closing act shares one geography. Any box works technically. |
| Token lifecycle, the credit header, and the throttle | Technical | § 4 rows 7 and 9 are the provider's terms, not ours to simplify. B-026, B-028. **See § 11 — these claims name a component that cannot hold a credential.** |
| A credential never reaches a log, a fixture or a screenshot | Both | Technically § 4 row 12. The business reason is that this runs in front of a room and is recorded, so "it is only a debug log" does not apply. B-027. |
| A missing credential stops the application at startup | Business | Chosen so the presenter learns before the stage rather than during it (§ 2 need 5). Failing lazily on the first poll compiles equally well and is the natural implementation. B-029, first half. |
| A failed poll does not end the stream | Both | Technically an `IObservable` that errors is finished for good. Why it is claimed at all: the grid must not go dead mid-sentence on a venue network. B-029, second half. |
| The swap is unobservable from the stream | Both | § 2 need 6: if the pipeline can tell which source is live, the claim the talk is making is false while it is being made. The decorator is the technical shape that buys it. B-038, B-039. |
| A swapped-out source stops spending | Both | Credits, and a leak reads as a library bug on a projector. `hot-swap-source` § "Disposal discipline" is the technical half. B-040. |
| What a viewer sees during the swap gap | Business | `decisions/0002`. `hot-swap-source` leaves it to the specification deliberately; no technical reading picks between an empty view and a busy indicator. |
| Stale is marked and kept, never removed | Business | The row most at risk of being settled technically: `ExpireAfter` sits in the README's own operator table as the obvious reach, and reaching for it answers a product question with an operator. A row vanishing reads as a bug; a flagged row reads as information. B-051. |
| One clock, and the tracker owns it | Technical | § 4 row 14 and `dynamic-data-pipeline` § "Staleness and expiry". B-043. |
| The pipeline is built once and survives the swap | Both | § 2 need 6, and the closing act's whole proof. `hot-swap-source` § "What must not be rebuilt" is the technical half. B-041, B-042, B-044. |
| One obvious place to add a source | Both | § 2 need 3. One seam carries every strategy, and it stays one member wide — a per-type interface that described its source would make the seam a place to look things up rather than a place to substitute. The three boundary claims are what make the promise true rather than aspirational, and each is falsifiable only from the far side, which is why they are assigned to the items that build the far side. B-033, B-037, B-045, B-046, B-047. |
| Integration code is the provider's, not one feature's | Technical | § 4 row 5. `AGENTS.md` governed feature layout and was silent on integrations; it gained the line. |
| Seven boxes is more than a slide wants | Business | ADR-0002 § Consequences names the cost this layering carries on stage. How much of it the talk draws is a presentation call, and this Feature does not make it — which is why the row is here and the answer is not. |

## 7. Technical Design

<!-- Rules: ../../../.spec/templates/feature.md § 7 -->

The layering this section would otherwise have had to settle is decided
repository-wide in
[ADR-0002](../../../.spec/adr/0002-contract-client-strategy-tracker.md), the
decoration package in
[ADR-0003](../../../.spec/adr/0003-scrutor-for-decorator-registration.md), and
the tracked item's base in
[ADR-0005](../../../.spec/adr/0005-an-abstract-base-carries-the-tracked-item.md) —
none of them here, because each binds more than this Feature. What follows is
this Feature's own shape, and it cites those records rather than restating them.

**Domain model**

`Aircraft` is the one concrete subclass of `TransportVehicle`. The base's
members and the rules behind them are ADR-0005's; the four it contributes are
marked *(base)* below so the ownership line is visible without leaving the
table.

| Field | Type | Notes |
| ----- | ---- | ----- |
| `Key` | `string` | *(base)* The snapshot's `icao24` lowercased, by a named `ToKey` in the mapper rather than inside a generated member mapping (B-037, `mapping` § "Conversions are explicit"). Plain `string`: a cache key is where `Option` stops (`language-ext-usage`). |
| `LastContact` | `DateTimeOffset` | *(base)* Index 4's Unix seconds, converted by a named method. |
| `Position` | `Option<GeoPosition>` | *(base — ADR-0005 defines it and why it is one optional rather than two)* What this Feature adds: it is fed from indices 5 and 6, combined by the mapper's `ToPosition`. |
| `IsStale(asOf, threshold)` | `bool` | *(base)* Derived, never stored. Takes the instant rather than holding a clock, because B-043 puts the clock in `IFleetTracker`. |
| `Label` | `string` | *(base, abstract — overridden here)* `Callsign` when present, else `Key`. Derived, which is why B-014 keeps it off the snapshot. |
| `Callsign` | `Option<string>` | Index 1, padding removed; all-padding is `None`, never `""` (B-019). |
| `OriginCountry` | `string` | Index 2. The index table declares no nullability, so it is non-optional and a null makes the row unreadable under B-022. |
| `TimePosition` | `Option<DateTimeOffset>` | Index 3, converted by the same named method as `LastContact`. |
| `BarometricAltitude` | `Option<double>` | Index 7, metres. |
| `GeometricAltitude` | `Option<double>` | Index 13, metres. Separate from the barometric one — B-036 requires both survive. |
| `OnGround` | `bool` | Index 8. |
| `Velocity` | `Option<double>` | Index 9, metres per second (B-035). |
| `TrueTrack` | `Option<double>` | Index 10, degrees clockwise from north (B-035). |
| `VerticalRate` | `Option<double>` | Index 11, metres per second. |
| `Squawk` | `Option<string>` | Index 14, a string so `"0021"` stays four characters (B-020). |
| `Spi` | `bool` | Index 15. |
| `PositionSource` | `Option<PositionSource>` | Index 16, a four-value enum named from the README's index table. `None` means a code this build does not name, which is a different absence from an unreported field — B-018's warning applied to an enum. No `Unknown` member: that would be a value the source never sent. |
| `Category` | `Option<int>` | Index 17, left as the wire's integer. § 4 row 1 makes the README index table the contract, and it publishes no value list, so naming the codes would invent a contract OpenSky did not. Display naming is a view concern, the same treatment § 5 row 11 gives units. |

The grouping member ADR-0005 item 2 names is deliberately not implemented here.
ADR-0005 § "The members" says why it has no single answer for this source; § 5
rows 1 and 7 are what make leaving it unanswered affordable, since both the
grouped view and `Group` belong to the next Feature.

**The snapshot, and the index each member reads from**

`AircraftSnapshot` is an `internal sealed record` keyed on `Icao24`, read from
README.md § "Response shape" index by index (B-016). The table runs in index
order **including the excluded index**, because B-021 requires the exclusion to
be visible rather than inferred from a gap.

| Index | Wire field | Member | Type | Note |
| ----- | ---------- | ------ | ---- | ---- |
| 0 | `icao24` | `Icao24` | `string` | The key (B-012). Absent ⇒ the row is excluded and counted (B-022). |
| 1 | `callsign` | `Callsign` | `Option<string>` | Padding trimmed; all-padding ⇒ `None`, not `""` and not whitespace (B-019). |
| 2 | `origin_country` | `OriginCountry` | `string` | Non-optional. |
| 3 | `time_position` | `TimePosition` | `Option<long>` | Unix seconds, **unconverted** — B-013. The conversion is the mapper's. |
| 4 | `last_contact` | `LastContact` | `long` | Unix seconds, unconverted, non-optional. |
| 5 | `longitude` | `Longitude` | `Option<double>` | Its own member here; combined into `Position` only at the domain. |
| 6 | `latitude` | `Latitude` | `Option<double>` | As above. |
| 7 | `baro_altitude` | `BarometricAltitude` | `Option<double>` | Metres. |
| 8 | `on_ground` | `OnGround` | `bool` | Non-optional. |
| 9 | `velocity` | `Velocity` | `Option<double>` | m/s. `null` ⇒ `None`, never `0` (B-017). |
| 10 | `true_track` | `TrueTrack` | `Option<double>` | Degrees. Same hazard. |
| 11 | `vertical_rate` | `VerticalRate` | `Option<double>` | m/s. Same hazard. |
| **12** | **`sensors`** | **— none —** | — | **Read past deliberately (B-021.)** The reader names index 12 and skips it in a statement a human can see. It is also the only index whose value is a collection, which is part of why value equality holds below. |
| 13 | `geo_altitude` | `GeometricAltitude` | `Option<double>` | Metres. |
| 14 | `squawk` | `Squawk` | `Option<string>` | Read with `GetString()`, never `GetInt32()`: `"0021"` is four characters (B-020). |
| 15 | `spi` | `Spi` | `bool` | Non-optional. |
| 16 | `position_source` | `PositionSource` | `int` | The wire integer, uninterpreted (B-013). Named at the domain. |
| 17 | `category` | `Category` | `Option<int>` | **The hazard a converter collapses (B-018).** `Option<int>` is what makes the claim's two cases two values rather than two spellings of one. Presence is decided by element count and element count alone — never by whether `extended=1` was requested, because the two can disagree. |

Three things follow from the table:

- **Value equality (B-011) is structural, not configured.** Every member is a
  scalar, a `string`, or an `Option<T>` of one; `record` supplies the rest. No
  member is a collection, and the only collection-valued wire field is index 12,
  which B-021 removed. That is the whole reason the hazard `## Scoring` recorded
  against `0003` does not arise.
- **The envelope's reported time is not a snapshot member.** It would differ on
  every poll, so the differ would emit a change for every aircraft every
  interval and the headline mechanism would produce nothing but churn. Where it
  goes instead is the open decision below.
- **Element count is read first.** B-022 lists three ways a row can be
  unreadable and two of them are answerable before any member is parsed, so the
  count is the first thing the reader looks at and the row is excluded there
  rather than part-way through being built.

**Diagrams**

The constructed chain. Dashed nodes are outside this Feature (§ 5 rows 1, 4, 7);
the recording transport is drawn because B-007's "per transport" is only legible
once the second one is visible.

```mermaid
graph LR
  sky(["OpenSky /states/all"])

  subgraph contracts["Integrations/OpenSky/Contracts"]
    api["IOpenSkyApi"]
    env["OpenSkyStatesResponse<br/>OpenSkyStateRow<br/>OpenSkyThrottled"]
  end

  subgraph http["Integrations/OpenSky/Http"]
    httpApi["OpenSkyHttpApi<br/>internal sealed, explicit impl"]
  end

  subgraph provider["Integrations/OpenSky"]
    client["AircraftSnapshotClient"]
    cache[("SourceCache of AircraftSnapshot")]
  end

  subgraph tracking["Tracking"]
    strategy["AircraftTrackerSource"]
    mapper["AircraftSnapshotMapper"]
    decorator["SwappingTrackerSource"]
    tracker["FleetTracker"]
  end

  replay["recording transport<br/>features/replay-source"]:::out
  vms["view models, next Feature"]:::out

  sky -->|JSON| httpApi
  httpApi -.->|implements| api
  env --- api
  replay -.->|implements, second transport| api
  api -->|"Either(throttled, response)"| client
  client -->|"EditDiff over the whole set"| cache
  cache -->|"changeset of AircraftSnapshot"| strategy
  mapper --- strategy
  strategy -->|"changeset of TransportVehicle"| decorator
  decorator --> tracker
  tracker --> vms

  classDef out stroke-dasharray: 4 3
```

One poll. This earns its place because it is where the split between what the
transport sees and what the client decides becomes visible — the distinction
§ 11's open question turns on.

```mermaid
sequenceDiagram
  autonumber
  participant S as poll schedule
  participant C as AircraftSnapshotClient
  participant H as OpenSkyHttpApi
  participant P as OpenSky
  participant K as SourceCache

  S->>C: tick, default 15s (B-050)
  C->>H: GetStates(lamin, lomin, lamax, lomax, extended, ct)
  H->>P: GET /states/all

  alt 200
    P-->>H: time and positional states rows
    H->>H: log X-Rate-Limit-Remaining at debug, never the token (B-027)
    H-->>C: Right(OpenSkyStatesResponse)
    C->>C: read rows by index, skipping 12 (B-016, B-021)
    C->>C: exclude and count unreadable rows (B-022)
    C->>K: EditDiff over the whole set (B-023)
  else 401
    P-->>H: 401
    H->>H: refresh the token, retry this request once (B-026)
  else 429
    P-->>H: 429 and X-Rate-Limit-Retry-After-Seconds
    H-->>C: Left(OpenSkyThrottled)
    C->>S: defer the next poll by exactly that many seconds (B-028)
  else 5xx, timeout, unreadable body
    P-->>H: failure
    H-->>C: throws
    C->>C: observed; the stream stays open (B-029)
  end
```

A class diagram is `Not applicable` — the declarations below are the
authoritative form, with accessibility, explicit implementation and generic
arguments a diagram would have to approximate. Drawing them as well would be a
second copy of the same thing to keep in step.

A state diagram is `Not applicable` — no component here holds more than one
state. The only state in the chain is the decorator's selected strategy, and the
control that changes it is § 5 row 5.

An entity-relationship diagram is `Not applicable` — nothing is persisted. § 5
row 14 excludes a snapshot store, and the recording format belongs to ADR-0004
and the replay Feature.

**Interface changes**

No name for any of these exists in the repository today, so this section chooses
them. The contract first:

```csharp
internal interface IOpenSkyApi
{
    Task<Either<OpenSkyThrottled, OpenSkyStatesResponse>> GetStates(
        double lamin,
        double lomin,
        double lamax,
        double lomax,
        bool extended,
        CancellationToken cancellationToken);
}
```

`IOpenSkyApi` carries no suffix, which is B-048 visible in the identifier. Not
`IOpenSkyApiContract`: "contract" is the pattern's word for the role, not part
of the thing's name. Not `IOpenSkyStatesApi`: B-005 is one method *per endpoint*
on one interface, and a name narrowed to one endpoint would make `/flights` look
like it needs a second interface, which B-009 reserves for a version change.
`GetStates` takes no `Async` suffix and puts `CancellationToken` last.

**Four loose coordinates rather than a bounding box.** B-006 and B-024 both
forbid a bounding box on the contract. These are OpenSky's own query-string
keys, so the signature matches the provider's documentation line for line while
the configured box stays on `OpenSkyOptions`. A request record holding the four
would be a bounding box under another name.

**`Either` because B-028 makes a `429` data.** The envelope cannot carry the
throttle — B-001 says it mirrors what OpenSky sends — and an exception is what
B-028 forbids. `language-ext-usage` names a throttle response as its case, and
B-005's `Task<T>` is satisfied, since `Either` is a `T`. Every other non-2xx
remains an exception, per B-028's second half.

```csharp
internal sealed record OpenSkyStatesResponse
{
    [JsonPropertyName("time")]
    public required long Time { get; init; }

    [JsonPropertyName("states")]
    public required IReadOnlyList<OpenSkyStateRow> States { get; init; }
}

[JsonConverter(typeof(OpenSkyStateRowConverter))]
internal sealed class OpenSkyStateRow
{
    public OpenSkyStateRow(IReadOnlyList<JsonElement> elements)
    {
        _elements = elements;
    }

    public int Count => _elements.Count;

    public JsonElement this[int index] => _elements[index];

    private readonly IReadOnlyList<JsonElement> _elements;
}

internal sealed record OpenSkyThrottled(TimeSpan RetryAfter);
```

Two members on the envelope, named as OpenSky names them (B-001), and no member
of it is a named per-aircraft type (B-002) — which is why there is no
`AircraftState` here. `OpenSkyStateRow` exposes **a count and an indexer and
nothing else**: it names no aircraft field, so it is not the named per-aircraft
type B-002 forbids, while still being something B-004 and B-045 can point at and
something the converter can attach to. `Count` is what B-016's element count and
B-022's 17–18 test read. It is a `class` rather than a `record` because a
`record` over a list advertises value equality it cannot honour — the type that
needs equality is the snapshot, one table above. `OpenSkyThrottled` carries
exactly the header's seconds and no invented backoff.

```csharp
internal sealed class OpenSkyHttpApi : IOpenSkyApi
{
    public OpenSkyHttpApi(IFlurlClientCache clients, IOpenSkyTokenSource tokens, ILogger<OpenSkyHttpApi> logger)
    {
        _clients = clients;
        _tokens = tokens;
        _logger = logger;
    }

    Task<Either<OpenSkyThrottled, OpenSkyStatesResponse>> IOpenSkyApi.GetStates(
        double lamin,
        double lomin,
        double lamax,
        double lomax,
        bool extended,
        CancellationToken cancellationToken) => /* … */;

    private readonly IFlurlClientCache _clients;
    private readonly IOpenSkyTokenSource _tokens;
    private readonly ILogger<OpenSkyHttpApi> _logger;
}
```

`internal sealed`, explicit interface implementation, no `public` endpoint
method: all three of B-007's clauses are readable in the declaration. The name
says **the transport**, which is the axis B-007 counts along, so the replay
sibling gets a name that pairs with it. Not `OpenSkyApiClient` — "client" is
already the layer above, and that exact collision is the one-word-three-things
problem ADR-0002 opens with.

```csharp
internal interface IAircraftTrackerSource : ITrackerSource;
```

Empty, and the emptiness is the claim: B-037's ban on widening the seam with
source-describing members is only visible if the interface exists and declares
nothing. `ITrackerSource` itself is unchanged — ADR-0002 item 6 declares it and
`api-contract` § "The two declarations" writes it out, so this Feature adds
nothing to it.

Options and credentials are **two types, not one**, because B-027 bans a
credential reaching a log line and a single options object invites being logged
whole. The polling interval defaults to fifteen seconds and the bounding box has
no default compiled in (B-050); `ValidateOnStart` on the credentials is what
makes B-029's startup failure name the absent one.

The projection is a Mapperly mapper with `RequiredMappingStrategy.Both`, so a
forgotten member is a build error rather than a silent default, and with
`ToKey`, `ToInstant` and `ToPosition` as named methods a test can call directly.
Indices 5 and 6 are marked ignored at the source because `ToPosition` consumes
them, rather than being dropped silently. There is no unit conversion anywhere
in it (B-035).

Everything under `Integrations/OpenSky/` is `internal`, the contract included: a
`public` interface cannot return an `internal` envelope, and `internal` is as
close as the compiler gets to B-004 inside one assembly. The only `public`
surface is the registration extension in `Container/`, which is what lets
`src/Gui` wire the chain without naming an implementation (B-008). The cost,
stated rather than discovered later: `Transponder.csproj` gains
`InternalsVisibleTo("Transponder.UnitTests")`, and `transponder-conventions`
§ `test-from-scenarios` now carries that as the convention.

Layout follows `transponder-conventions` § "Project structure":

```
src/Transponder/Model/                           TransportVehicle, Aircraft, GeoPosition, PositionSource
src/Transponder/Tracking/                        ITrackerSource, IFleetTracker, FleetTracker, SwappingTrackerSource
src/Transponder/Tracking/Sources/                IAircraftTrackerSource, AircraftTrackerSource, AircraftSnapshotMapper
src/Transponder/Integrations/OpenSky/Contracts/  IOpenSkyApi, OpenSkyStatesResponse, OpenSkyStateRow, OpenSkyThrottled
src/Transponder/Integrations/OpenSky/Http/       OpenSkyHttpApi, OpenSkyStateRowConverter, IOpenSkyTokenSource, OpenSkyTokenSource
src/Transponder/Integrations/OpenSky/            AircraftSnapshot, AircraftSnapshotClient, OpenSkyOptions, OpenSkyCredentials, BoundingBox
src/Transponder/Integrations/OpenSky/Container/  OpenSkyRegistration
```

The cache gets no type of its own. A `SourceCache` of `AircraftSnapshot` keyed
by `string`, registered with the application's lifetime, **is** B-030's plain
store, and the absence of a wrapper is what makes "no diff policy of its own"
true by construction rather than by assertion.

**Decision required**

B-003 makes the envelope's reported time the observed instant for everything
downstream and bans any consumer from reading an ambient clock to supply one.
B-043 puts the clock in `IFleetTracker`. Nothing says how the first reaches the
second, and the obvious answer is ruled out one table above: carried on the
snapshot, it would differ on every poll and the differ would emit churn.

> | Option | Summary | Tradeoff |
> | ------ | ------- | -------- |
> | A. | The clock `IFleetTracker` is injected with reads the last observed instant the live chain reported; the integration advances it as each envelope arrives. | Nothing widens: `ITrackerSource` keeps one member (B-033, B-037), no snapshot carries a per-poll value, and replay needs no second mechanism — which is § 4 row 20's whole purpose. Cost: the clock becomes stateful and source-coupled, and two live sources feeding one clock is undefined. |
> | B. | `ITrackerSource` gains a second member carrying the observed instant beside the changeset. | Explicit and per-source, so two sources cannot fight over one clock. Cost: it widens the one seam the whole design rests on, and B-037 sits immediately beside it. |
> | C. | Each projected `TransportVehicle` carries the instant it was observed at. | No seam change and no clock. Cost: B-011 means a vehicle whose snapshot did not change is never re-emitted, so the value would go stale on precisely the aircraft staleness is about. Listed because it is the first thing anyone proposes, and rejected on inspection. |
>
> **Recommendation:** A, scoped to one live source, with the two-source case
> left to the closing act.
> **Awaiting:** the person. It binds
> [`features/replay-source`](../../replay-source/.spec/README.md) and the
> pipeline Feature, not only this one.

## 8. Testing Strategy

<!-- Rules: ../../../.spec/templates/feature.md § 8 -->

Unwritten. `test-writer` owes both the testability assessment and the scenario
grouping. The constraints it has to satisfy are already stated: no test touches
a network or the wall clock (§ 4 row 14), `HttpTest` cannot follow a message
into an actor (§ 4 row 15), and the contract's double is hand-written rather
than mocked (B-010, § 4 row 18).

The scenarios themselves exist, in
[`aircraft-source.feature`](aircraft-source.feature) beside this file, each
tagged with the `@B-00n` it proves. Scenarios are documentation; the xUnit
tests execute, and none has been written yet.

## 9. Traceability Matrix

<!-- Rules: ../../../.spec/templates/feature.md § 9 -->

Unwritten, and **this is the gate**. `test-writer` owes one row per § 3 claim,
anchored to the scenario's `@B-00n` tag and naming the xUnit test that proves
it. Until those rows exist no claim is covered, so the matrix stands `Missing`
in its entirety and implementation does not start — which is why every item
sits at `ready-for-architecture` rather than `ready-for-implementation`.

A scenario existing is not coverage. This section is the only place a claim's
build state is written, and a row here naming no test is what blocks ship.

## 10. Lessons / Spec Deltas

<!-- Rules: ../../../.spec/templates/feature.md § 10 -->

No Feature-scoped lesson yet. One repository-wide lesson bears on this
document: [lesson 0002](../../../.spec/lessons/0002-metadata-about-a-rule-drifts-too.md),
which is why §§ 6-9 above say in words that they are unwritten rather than
carrying a template row, and why § 3 no longer keeps a build state § 9 owns.

## 11. Open Questions

<!-- Rules: ../../../.spec/templates/feature.md § 11 -->

Two, both opened by § 7. Writing the design is what surfaced them; neither was
visible while the section was a stub.

| #   | Question | Owner | Target date |
| --- | -------- | ----- | ----------- |
| 1   | How does the envelope's reported time reach the clock `IFleetTracker` owns? B-003 makes it the observed instant for everything downstream and bans a consumer from reading an ambient clock; B-043 puts the clock in the tracker; nothing connects them, and § 7 rules out the obvious answer of carrying it on the snapshot. § 7 "Decision required" states three options and recommends the first. Blocks `0007`, and binds [`features/replay-source`](../../replay-source/.spec/README.md). | `implementer` → the person | Before `0007` starts |
| 2   | Which component do B-026, B-027 and B-028 actually name? Each attributes to "the snapshot client" behaviour only the thing holding the HTTP response can perform — a `401`, the `X-Rate-Limit-Remaining` header, the `X-Rate-Limit-Retry-After-Seconds` header — while B-006 forbids a credential on the contract, so the client cannot hold the token, and a recording transport has no token to refresh at all. § 7 could not place the behaviour without contradicting one claim or the other, and `implementer` does not edit § 3. Expect B-026 and B-027 to be re-subjected and B-028 split: inspected in the transport, deferred in the client. Blocks `0004`. | `spec-author` | Before `0004` starts |

Everything else this specification opened has been answered and recorded: the
bounding box and interval in [decisions/0001](decisions/0001-houston-bounding-box.md)
and B-050, what the audience sees on a swap in
[decisions/0002](decisions/0002-busy-indicator-on-swap.md), the staleness policy
in B-051, the version suffix in B-048, the contract layer's applicability in
B-049, the integration layout in § 4 row 5, and the decoration package in
[ADR-0003](../../../.spec/adr/0003-scrutor-for-decorator-registration.md).

## 12. Sign-off

<!-- Rules: ../../../.spec/templates/feature.md § 12 -->

| Sections   | Owner          | Status   |
| ---------- | -------------- | -------- |
| §§ 1-5     | spec-author    | 🟡 Draft |
| §§ 6-7     | implementer    | 🟡 Draft |
| §§ 8-9     | test-writer    | 🟡 Draft |

Overall is the frontmatter's `spec_status`, not a row here. `spec-reviewer`
flips it to `approved` when every row above is 🟢 and § 9 has no `Missing`
row.

## Decisions

<!-- Rules: ../../../.spec/templates/feature.md § Decisions -->

- [0001 — Houston is the bounding box, and the interval starts at 15 seconds](decisions/0001-houston-bounding-box.md) — decided
- [0002 — A busy indicator covers the swap, then the new fleet arrives](decisions/0002-busy-indicator-on-swap.md) — decided

The layering this specification is written against is a cross-cutting technical
decision rather than a product call, so it is recorded in the repository-wide
[ADR-0002](../../../.spec/adr/0002-contract-client-strategy-tracker.md), and the
decoration package in
[ADR-0003](../../../.spec/adr/0003-scrutor-for-decorator-registration.md) —
neither here nor in this Feature's `adr/`.

## Tasks

<!-- Rules: ../../../.spec/templates/feature.md § Tasks -->

| Item                                                     | Claims                                          |
| -------------------------------------------------------- | ----------------------------------------------- |
| [`0001`](../.issue/0001-aircraft-source.yml)             | all 51 — the parent; its children hold the work |
| [`0002`](../.issue/0002-opensky-api-contract.yml)        | B-001 – B-010, B-048, B-049                     |
| [`0003`](../.issue/0003-aircraft-snapshot-and-cache.yml) | B-011 – B-014, B-030 – B-032                    |
| [`0004`](../.issue/0004-aircraft-snapshot-client.yml)    | B-015 – B-029, B-045, B-050                     |
| [`0005`](../.issue/0005-aircraft-tracker-source.yml)     | B-033 – B-037, B-046                            |
| [`0006`](../.issue/0006-source-swap-decorator.yml)       | B-038 – B-040                                   |
| [`0007`](../.issue/0007-fleet-tracker-wrapper.yml)       | B-041 – B-044, B-047, B-051                     |

Every claim is carried by exactly one child, and `0001` carries all of them
because the children are slices of it rather than work beside it. The three
layer-boundary claims are assigned to the child that makes each falsifiable —
B-045 to `0004`, B-046 to `0005`, B-047 to `0007` — because a boundary cannot be
asserted before both sides of it exist.

Each item's `depends_on` sequences the work: `0002` and `0003` have no
prerequisite, `0004` waits on both, `0005` waits on `0004`, and `0006` and
`0007` wait on `0005`.

## Scoring

<!-- Rules: ../../../.spec/templates/feature.md § Scoring -->

| Date       | Item   | Field | Rationale |
| ---------- | ------ | ----- | --------- |
| 2026-10-04 | `0001` | value | § 1 names four outcomes — the contract, the caching client, the per-type projection and the decorated swap — and the business goal fails without any of them. |
| 2026-10-04 | `0001` | risk | The boundary claims B-045 – B-047 are the ones nothing verifies until both sides of each boundary exist, so they are the claims most likely to be "satisfied" by absence rather than by a test. Aim a test at each from the far side: name the envelope from a view model and fail. |
| 2026-10-04 | `0002` | risk | B-010's fake is the hazard: a double that returns `default` instead of throwing on an unset response makes every test above it pass for the wrong reason, and nothing anywhere reports it. Second, B-007's explicit implementation compiles either way — an implicit `public` method satisfies the compiler and breaks the claim silently. |
| 2026-10-04 | `0003` | risk | Lowest risk, one hazard, and it is the headline's foundation: a snapshot with a mutable or collection member makes two identical reports compare unequal (B-011), so the differ reports changes for aircraft that did not change. Nothing errors — the grid just churns. Aim at equality with a repeated report. |
| 2026-10-04 | `0004` | risk | Four hazards, each a silent wrong answer rather than a failure. Absent versus present-zero category is the distinction a converter collapses (B-018). Squawk `"0021"` becomes the number 21 (B-020). A `null` element becomes `0`, `false` or `""` (B-017). One malformed row takes the whole batch down (B-022). A fifth if the transport is sloppy: a `429` thrown instead of deferred (B-028). |
| 2026-10-04 | `0005` | risk | Mapperly will do the wrong thing here without complaining: an absent altitude mapped to `0` is an aircraft at sea level, and an absent position mapped to `0,0` is one in the Gulf of Guinea (B-036). Both render as plausible data. Also aim at B-037 — an uppercase hex key splits one aircraft into two cache entries. |
| 2026-10-04 | `0006` | risk | Both hazards are invisible at runtime. Scrutor's `Decorate<>` wraps only what is **already** registered, so a strategy registered after the call resolves raw and the swap silently does nothing (ADR-0003). And an outgoing source that is not stopped keeps spending OpenSky credits with nothing on screen to show it (B-040). |
| 2026-10-04 | `0007` | risk | One inline `DateTime.UtcNow` in a staleness check (B-043) makes the behaviour untestable *and* wrong under replay, where time comes from the recording. Then B-042: a pipeline rebuilt on swap leaks, and a leak in a demo reads as a memory bug on a projector. Aim at a swap followed by a second swap. |
| 2026-10-04 | `0001` | risk | 4 → 3 against § 7. The boundary claims are no longer only test-enforced: the whole integration is `internal`, so most of "satisfied by absence" is now a compile error rather than a missing assertion. What remains is the reference-counting the @B-004 @B-045 scenario describes, which no compiler answers. |
| 2026-10-04 | `0002` | risk | 3 → 4. § 7 **adds** hazards rather than retiring them. The contract returns `Either`, which is a shape the pattern's reviewers will not have seen on a governed interface. The positional row needs a hand-written `JsonConverter`, and a sloppy one defaults silently — the same failure as the fake that returns `default`. `InternalsVisibleTo` is plumbing no project here has yet. The two hazards recorded above stand unchanged, and the thrown-`429` hazard moves here from `0004`, since the status code is seen in this item's transport. |
| 2026-10-04 | `0003` | risk | 2 → 1. The hazard recorded above is now structurally impossible rather than merely tested for: § 7 gives the snapshot only scalars, strings and `Option<T>` of one, and the single collection-valued wire field is index 12, which B-021 removed. A mutable or collection member cannot be added without contradicting the table. The residual is that someone adds one anyway. |
| 2026-10-04 | `0004` | risk | 4 → 3. The same four hazards in kind, but each is now a named member at a named index whose type encodes the distinction: `Option<int>` for B-018, `Option<T>` throughout for B-017, `string` for B-020. The fifth moves to `0002`. Against that, open question 2 means this item's claim list may still change. |
| 2026-10-04 | `0005` | risk | 3 → 2. B-036 moves from test-enforced to compile-enforced: one `Option<GeoPosition>` makes a half-position unrepresentable, the two altitudes are separate members, and `RequiredMappingStrategy.Both` makes a forgotten field a build error. What is left is `ToKey`'s lowercasing, which no type system catches. |
| 2026-10-04 | `0006` | risk | 4, unchanged. § 7 adds nothing it was waiting for — ADR-0003 already carried both hazards. The row exists because the blanket provisional sentence it previously sat under is gone, and an unrestated number would read as an oversight. |
| 2026-10-04 | `0007` | risk | 3 → 4. § 7 raises it: open question 1 lands on this item, and B-043's clock cannot be specified until the observed instant has a route to it. The two hazards recorded above stand. |

Why `0001` carries a `value` and no child does: a child omits it to inherit the
parent's, and `risk` is never inherited. The derivation of `priority` and `rank`
from the two, and the fact that a dependency cut in another Feature moves a rank
here with no row in this table, are the item schema's —
[`item.yml`](../../../.spec/templates/item.yml).

**§ 7 has landed, and the numbers above are no longer provisional.** Each item
carries a second row re-scoring it against the design rather than against a
guess at the design. Four moved down, because § 7 turned a hazard a test had to
catch into one the compiler catches; two moved up, because writing the design
found hazards that were not visible from the claims alone.

**No item's `status` changes on this.** Two things held every item at
`ready-for-architecture` and § 7 clears only the first: § 9 is still empty, and
a claim with no row there is not covered. Moving an item now would be recording
a gate as passed that nothing has verified, which is the failure
[lesson 0002](../../../.spec/lessons/0002-metadata-about-a-rule-drifts-too.md)
is about. What each item is waiting on, so the next change need not re-derive
it: `0002`, `0003`, `0005` and `0006` wait on § 9 alone; `0004` waits on § 9 and
§ 11 question 2; `0007` waits on § 9 and § 11 question 1; `0001` moves when its
children do.
