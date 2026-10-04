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
| B-003 | The envelope's reported time SHALL be the observed instant for everything downstream, and no consumer SHALL read an ambient clock to supply one.                                                                             | api-mock § "Replay"; dynamic-data-pipeline § "Staleness"  |
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
| B-051 | A vehicle past the staleness threshold SHALL remain in the collection and SHALL be observably stale; it SHALL NOT be removed for staleness. The threshold SHALL be configurable and SHALL default to five minutes.                                                   | Decided call; dynamic-data-pipeline § "Staleness"          |
| B-011 | The snapshot SHALL have value equality over every member it carries, so two snapshots reporting identical values compare equal and the differ emits no change for them.                                                      | Decided call — the client diffs records                    |
| B-012 | The snapshot SHALL carry `icao24` as a non-optional member and SHALL be keyed on it.                                                                                                                                        | README.md index 0 ("the cache key")                        |
| B-013 | The snapshot SHALL carry the wire's values in the wire's units and SHALL perform no conversion, derivation or interpretation; it is the server's record with names on it.                                                     | Decided call; mapping § "Conversions are explicit"         |
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
| B-024 | The bounding box and the polling interval SHALL be supplied to the snapshot client as constructor or options input, and SHALL NOT appear on the API contract, on its own stream, or on anything downstream.                  | api-contract § "The seam is the whole design"              |
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
| B-035 | The projection SHALL store each value in the unit the wire reported it in — metres, metres per second, degrees clockwise from north — and SHALL NOT convert to feet, knots, or any display unit.                             | domain-model § "Units"                         |
| B-036 | Every absent snapshot value SHALL project to `Option<T>.None`: an absent altitude SHALL NOT become sea level and an absent position SHALL NOT become latitude 0, longitude 0; and barometric and geometric altitude SHALL both survive as separate optional members. | language-ext-usage; mapping § "Optional values"             |
| B-037 | `TransportVehicle.Key` SHALL be the snapshot's `icao24` in lowercase hex, non-optional and set on construction; and a per-type tracker source interface SHALL NOT widen the seam with source-describing members.            | domain-model; api-contract § "Never add"       |
| B-038 | A decorator registered as `ITrackerSource` SHALL select the live strategy at runtime, and there SHALL be no strategy-resolver type that callers ask which strategy to use.                                                    | Decided call — decorator, not resolver                     |
| B-039 | The decorator SHALL be substitutable for any strategy it wraps, and no consumer SHALL be able to observe from its stream that a swap occurred.                                                                                | hot-swap-source § "The mechanism"                          |
| B-040 | Swapping SHALL stop the outgoing source, so a swapped-out poller stops spending OpenSky credits and a swapped-out socket stops reading.                                                                                       | hot-swap-source § "Disposal discipline"                    |
| B-041 | `IFleetTracker` SHALL wrap `ITrackerSource` and SHALL be what view models depend on; no view model SHALL depend on a strategy, a client, a cache or the decorator.                                                            | Decided call; mvvm                                         |
| B-042 | `IFleetTracker` SHALL own the collection pipeline — filtering, sorting, grouping, aggregates, property-change refresh, expiry and binding — and that pipeline SHALL be constructed once and SHALL NOT be rebuilt because the live source changed. | Decided call; hot-swap-source § "What must not be rebuilt" |
| B-043 | `IFleetTracker` SHALL own the injected clock: staleness SHALL derive from a vehicle's last contact against it, and no staleness or expiry decision anywhere SHALL read `DateTime.UtcNow` inline.                              | dynamic-data-pipeline § "Staleness"; test-from-scenarios   |
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
| 11  | Unit conversion to feet, knots or local time, and display formatting of an `Option<T>`                           | A display concern (domain-model § "Units"). SI is canonical in the model (B-035); conversion happens at the view in a named method.                                                                                           |
| 12  | OpenSky's `sensors` field (index 12)                                                                             | Explicitly excluded by B-021. Recorded as a decision rather than left as an omission, so a later reader does not add it believing it was overlooked.                                                                                      |
| 13  | A second strategy seam, and any source-describing metadata — display name, which columns make sense, an icon      | B-037 forbids widening a per-type interface for this. A source that genuinely needs to describe itself gets a separate small type in a separate feature.                                                                                  |
| 14  | Persisting snapshots, snapshot history, or a track per aircraft                                                  | Each cache holds current state. A history store is a second collection, which dynamic-data-pipeline § "Never add" rules out, and nothing in § 3 or README.md asks for a trail.                                                            |
| 15  | Registering the OpenSky account, creating the API client, and provisioning the credentials                       | Operational tasks tracked in README.md § "Open items". B-029 claims the application's *behavior* when a credential is absent; obtaining one is not code.                                                                                  |
| 16  | The poller's hosting — actor shape, supervision, registration, and whether a view model uses `Tell` or `Ask`      | `akka-actor` and `mvvm`. This spec claims the client's observable behavior (B-015 – B-029), not where it runs. `Tell`-vs-`Ask` is a repository-wide undecided, open at README.md § "Open items".                                                  |
| 17  | A `Test` target in the Nuke build                                                                                | A repository-wide undecided, open at README.md § "Open items". Deciding it inside a feature specification would settle a build convention by side effect.                                                     |

## 6. Concern Separation

<!-- Rules: ../../../.spec/templates/feature.md § 6 -->

Unwritten. `implementer` owes it, and it is the classification that keeps a
business judgment from being settled as a technical one. The claims most likely
to need it are the ones § 4 rows 18 and 20 resolve as narrow departures from the
global contract pattern: each is a technical shape chosen for a business reason
that § 1 names, and the two readings should not collapse into one paragraph.

## 7. Technical Design

<!-- Rules: ../../../.spec/templates/feature.md § 7 -->

Unwritten. `implementer` owes it. The layering it would otherwise have had to
settle is already decided repository-wide in
[ADR-0002](../../../.spec/adr/0002-contract-client-strategy-tracker.md), the
decoration package in
[ADR-0003](../../../.spec/adr/0003-scrutor-for-decorator-registration.md), and
the tracked item's base in
[ADR-0005](../../../.spec/adr/0005-an-abstract-base-carries-the-tracked-item.md) —
none of them here, because each binds more than this Feature.

What is left for this section is this Feature's own shape: the snapshot's
members and the positional indices they read from, the domain model's fields,
the Mermaid diagram of the constructed chain, and the interface declarations
B-005 – B-010 constrain. No open decisions — § 11 records that every question
this specification opened has been answered.

Until it lands, every `risk` in `## Scoring` is provisional and every item sits
at `ready-for-architecture`.

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

None yet.

## 11. Open Questions

<!-- Rules: ../../../.spec/templates/feature.md § 11 -->

None. Every question this specification opened has been answered and recorded:
the bounding box and interval in [decisions/0001](decisions/0001-houston-bounding-box.md)
and B-050, what the audience sees on a swap in
[decisions/0002](decisions/0002-busy-indicator-on-swap.md), the staleness policy
in B-051, the version suffix in B-048, the contract layer's applicability in
B-049, the integration layout in § 4 row 5, and the decoration package in
[ADR-0003](../../../.spec/adr/0003-scrutor-for-decorator-registration.md).

A question arriving later is added here as a row — `#`, Question, Owner, Target
date — rather than settled in conversation.

## 12. Sign-off

<!-- Rules: ../../../.spec/templates/feature.md § 12 -->

| Sections                                                                  | Owner          | Status   |
| ------------------------------------------------------------------------- | -------------- | -------- |
| Business Goal, User Needs, Acceptance Criteria, Constraints, Out of Scope  | spec-author    | 🟡 Draft |
| Concern Separation, Technical Design                                      | implementer    | 🟡 Draft |
| Testing Strategy, Traceability Matrix                                     | test-writer    | 🟡 Draft |
| Overall                                                                   | spec-reviewer  | 🟡 Draft |

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

| Date       | Field         | From | To | Rationale                                                                                                                                                 |
| ---------- | ------------- | ---- | -- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-10-04 | `0001` value  | —    | 5  | § 1 names four outcomes — the contract, the caching client, the per-type projection and the decorated swap — and the business goal fails without any of them. |
| 2026-10-04 | `0001` risk   | —    | 4  | The boundary claims B-045 – B-047 are the ones nothing verifies until both sides of each boundary exist, so they are the claims most likely to be "satisfied" by absence rather than by a test. Aim a test at each from the far side: name the envelope from a view model and fail. |
| 2026-10-04 | `0002` risk   | —    | 3  | B-010's fake is the hazard: a double that returns `default` instead of throwing on an unset response makes every test above it pass for the wrong reason, and nothing anywhere reports it. Second, B-007's explicit implementation compiles either way — an implicit `public` method satisfies the compiler and breaks the claim silently. |
| 2026-10-04 | `0003` risk   | —    | 2  | Lowest risk, one hazard, and it is the headline's foundation: a snapshot with a mutable or collection member makes two identical reports compare unequal (B-011), so the differ reports changes for aircraft that did not change. Nothing errors — the grid just churns. Aim at equality with a repeated report. |
| 2026-10-04 | `0004` risk   | —    | 4  | Four hazards, each a silent wrong answer rather than a failure. Absent versus present-zero category is the distinction a converter collapses (B-018). Squawk `"0021"` becomes the number 21 (B-020). A `null` element becomes `0`, `false` or `""` (B-017). One malformed row takes the whole batch down (B-022). A fifth if the transport is sloppy: a `429` thrown instead of deferred (B-028). |
| 2026-10-04 | `0005` risk   | —    | 3  | Mapperly will do the wrong thing here without complaining: an absent altitude mapped to `0` is an aircraft at sea level, and an absent position mapped to `0,0` is one in the Gulf of Guinea (B-036). Both render as plausible data. Also aim at B-037 — an uppercase hex key splits one aircraft into two cache entries. |
| 2026-10-04 | `0006` risk   | —    | 4  | Both hazards are invisible at runtime. Scrutor's `Decorate<>` wraps only what is **already** registered, so a strategy registered after the call resolves raw and the swap silently does nothing (ADR-0003). And an outgoing source that is not stopped keeps spending OpenSky credits with nothing on screen to show it (B-040). |
| 2026-10-04 | `0007` risk   | —    | 3  | One inline `DateTime.UtcNow` in a staleness check (B-043) makes the behaviour untestable *and* wrong under replay, where time comes from the recording. Then B-042: a pipeline rebuilt on swap leaks, and a leak in a demo reads as a memory bug on a projector. Aim at a swap followed by a second swap. |

`value` is inherited: no child carries one, so each resolves to `0001`'s 5 at
read time. `risk` is never inherited and is set per item above.

`priority` and `rank` on each item are **derived** from those two —
`rank = (value × 20) − (risk × 3) + (unblocks × 5)`, bucketed `high` at 60 and
above — so they are recomputed, never hand-edited. Every item lands in `high`,
which means the bucket orders nothing here: `rank` does. `0003` at 99 is the
highest-ranked item with no prerequisite, so it is the queue head; `0005` ranks
highest overall at 106 because three items wait on it, and still runs after
`0004`.

Three of those ranks moved when the replay Feature's items were cut, because
`unblocks` counts dependents from **any** Feature: replay's recording tap waits
on `0004` (93 → 98), its aircraft substitution on `0005` (101 → 106), and its
selection on `0006` (88 → 93). No `value` or `risk` changed, so the table above
has no row for it — `rank` is derived, and the moves are the formula working on
a dependency that did not exist when the items were cut. `0003` is still the
queue head.

**Every `risk` above is provisional.** §§ 6-7 are unwritten, and the design
assessment they carry is what each number is supposed to come from — which is
why every item sits at `ready-for-architecture` rather than
`ready-for-implementation`. Re-score as a row here when § 7 lands.
