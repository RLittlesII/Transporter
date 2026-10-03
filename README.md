# Transponder

## Audience

Line-of-business and enterprise .NET developers. Most of them work with data behind REST endpoints, SQL queries, and other request/response services, not push-based feeds. The demo should show a pattern they can apply to their own apps right away.

## Core idea

**Polled data can still be reactive.** Poll a REST API for a full snapshot, use `EditDiff` to turn successive snapshots into changesets, and from that point on everything is a normal DynamicData pipeline. Many developers assume reactive collections require a push source; this demo shows they don't.

## The app: "Fleet Tracking Dashboard"

Live aircraft over a metro area, framed as a fleet dashboard so the audience maps it onto their own domains (trucks, technicians, shipments, tickets).

UI features:

- Live grid of aircraft (one row per aircraft, updating in place)
- Search box and dropdown filters
- Column sorting chosen by the user
- Grouped view (by origin country or aircraft category)
- Master-detail: selected aircraft in a detail pane
- Summary counts and aggregates per group
- "Stale" indicator or removal for aircraft that stop reporting
- Optional: map view

## DynamicData operators to demonstrate

In roughly the order the audience meets them:

| Operator | Used for |
|---|---|
| `EditDiff` | Snapshot to changeset (the headline feature) |
| `Filter` with an observable predicate | Search box and dropdowns |
| `Sort` with a user-selected comparer | Column sorting |
| `Group` | Grouped grid by country or category |
| `Bind` | Pushing changes into the UI collection |
| Aggregates (`Count`, etc.) | Summary counts per group |
| `AutoRefresh` | Re-evaluating filters and sorts when properties change |
| `ExpireAfter` / staleness | Aircraft that stop reporting |

## Data source: OpenSky Network

- **Docs:** https://openskynetwork.github.io/opensky-api/rest.html
- **Base URL:** `https://opensky-network.org/api`
- **Endpoint:** `GET /states/all`
    - Bounding box: `lamin`, `lomin`, `lamax`, `lomax`
    - `extended=1` adds aircraft category (useful for grouping)
- **No OpenAPI spec.** Write the client by hand with `HttpClient` and `System.Text.Json`.

### Response shape

`states` is an array of arrays; fields are positional, not named. Needs a custom `JsonConverter`.

| Index | Field | Notes |
|---|---|---|
| 0 | `icao24` | **The cache key** |
| 1 | `callsign` | May be null; padded to 8 chars |
| 2 | `origin_country` | Good for grouping |
| 3 | `time_position` | Unix seconds, nullable |
| 4 | `last_contact` | Unix seconds |
| 5 | `longitude` | Nullable |
| 6 | `latitude` | Nullable |
| 7 | `baro_altitude` | Meters, nullable |
| 8 | `on_ground` | bool |
| 9 | `velocity` | m/s, nullable |
| 10 | `true_track` | Degrees from north, nullable |
| 11 | `vertical_rate` | m/s, nullable |
| 12 | `sensors` | Usually null |
| 13 | `geo_altitude` | Meters, nullable |
| 14 | `squawk` | Nullable |
| 15 | `spi` | bool |
| 16 | `position_source` | 0 ADS-B, 1 ASTERIX, 2 MLAT, 3 FLARM |
| 17 | `category` | Only with `extended=1` |

### Authentication

- OAuth2 client credentials only (no basic auth).
- Create an API client on the OpenSky account page to get `client_id` and `client_secret`.
- Token URL: `https://auth.opensky-network.org/auth/realms/opensky-network/protocol/openid-connect/token`
- Tokens expire after 30 minutes; refresh on expiry or on a 401.

### Limits

| Tier | Daily credits | Resolution |
|---|---|---|
| Anonymous | 400 | 10 seconds |
| Registered (free) | 4,000 | 5 seconds |

Credit cost per `/states/all` call by bounding box area: ≤25 sq° = 1, 25–100 = 2, 100–400 = 3, >400 or global = 4.

- A metro-sized box polled every 10 seconds costs about 360 credits per hour. **Register for a free account.**
- Remaining credits appear in the `X-Rate-Limit-Remaining` header; a `429` response includes `X-Rate-Limit-Retry-After-Seconds`.

### Gotchas

- OpenSky may block AWS and other hyperscaler IPs. **Run the demo from a laptop, not a cloud VM.**
- Terms ask that public presentations cite the OpenSky paper. **Add a credit slide:** Schäfer, Strohmeier, Lenders, Martinovic, Wilhelm, "Bringing Up OpenSky: A Large-scale ADS-B Sensor Network for Research."

## Backup source: airplanes.live

- Docs: https://airplanes.live/api-guide/
- Endpoint: `/v2/point/[lat]/[lon]/[radius]` (radius up to 250 nm)
- Named JSON fields (easier than OpenSky), 1 request per second, non-commercial, no SLA.
- **Access terms are unclear:** email them before relying on it.

## Demo resilience

- Put the data source behind a single interface that produces snapshots (or an `IObservable` of snapshots).
- Implementations: live OpenSky, and **replay from recorded snapshots**.
- Record several minutes of snapshots during rehearsal. Be able to switch to replay on stage if the venue network fails.
- This also makes a teaching point: the pipeline doesn't care where the data comes from.

## Closing act (optional): swap in a push source

Replace the polled source with a push source and show that everything downstream of the cache is unchanged. Options:

- **Ships via AISStream.io** (my favorite, and it fits the "fleet" theme). Free WebSocket feed of live vessel positions, keyed by MMSI, filterable by bounding box. Requires a free API key (sign up with GitHub). Community .NET package: `AISStream.NET`; official C# example uses `ClientWebSocket`. Ships go silent rather than being removed, so `ExpireAfter` matters here too.
- **Coinbase** via `JKorf.Coinbase.Net` (Advanced Trade WebSocket). Order book updates map almost directly onto `AddOrUpdate` / `Remove`. Crypto may distract an enterprise audience.
- **Simulated push source** with no network dependency at all.

## Alternatives considered

| Option | Why not the main demo |
|---|---|
| Coinbase order book | Great fit technically, but push-only lesson, fast/bursty data, crypto domain may distract |
| Ships (AISStream) | Very cool, but push-based so it skips the `EditDiff` lesson; kept as the closing act |
| Wikimedia EventStreams | Append-only events; needs derived state |
| Bluesky Jetstream | Event-shaped; main .NET library (FishyFlip) deprecated in favor of CarpaNet |
| Local processes / FileSystemWatcher | Great offline fallback, less compelling story |

## Open items

- [ ] Register an OpenSky account and create an API client
- [ ] Pick the bounding box (metro area) and polling interval
- [X] Decide UI framework (WPF, Avalonia, Blazor, MAUI)
- [ ] Record replay snapshots during rehearsal
- [ ] Decide on the closing act (ships, Coinbase, or simulated)
- [ ] If using ships: get an AISStream API key
  - [X] Intend to show ships
- [ ] Add OpenSky citation slide

## Technology Decisions

- [Maui.Markup](https://github.com/CommunityToolkit/Maui.Markup?WT.mc_id=dotnet-29192-cxa)
- [C# Language Extensions](https://github.com/louthy/language-ext)
- [Akka](https://github.com/akkadotnet/akka.net)
- [Mapperly](https://github.com/riok/mapperly)
