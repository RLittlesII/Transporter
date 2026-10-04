---
name: transponder-domain-model
description: Model tracked fleet items as subclasses of the abstract TransportVehicle (Aircraft by icao24, Vessel by MMSI) so the DynamicData pipeline and UI never learn which source is live. Use when adding or changing a domain type, a unit, or a grouping key.
---

# The Transponder domain model

`src/Transponder` holds the model. It is the one layer the rest of the demo
leans on, and the ships stretch goal is what stresses it: the audience must
see the *same* grid, filters, sorts and groups refill with vessels after a
live source swap, which only works if nothing downstream names `Aircraft`.

**This is a demo and nothing here is adopted yet.** The field tables below are
facts, taken from the OpenSky response table in [`README.md`](../../README.md).
The shapes — which value object, which base member, which folder — are a first
reading of the documentation. Change them freely; just change them in one
place and say so in the specification.

## `TransportVehicle`: one abstract base

`TransportVehicle` is an **abstract class**, not an interface — the
polymorphism is the point. It carries the five members every tracked thing
has, and `Aircraft` and `Vessel` derive from it:

| Member | Why it exists |
|---|---|
| Key | The cache key. `icao24` for aircraft, MMSI for vessels. Stable for the life of the item, never null, never reused. |
| Position | Latitude and longitude, both optional — OpenSky reports aircraft it has no fix for. |
| Last contact | The instant the source last heard from the item. **Staleness is derived from this**, never stored as a flag. |
| Label | What the grid shows in its identity column: callsign, vessel name, or the key when nothing better exists. |
| Grouping key | What `Group` groups by. Origin country or category for aircraft; flag or ship type for vessels. |

Why a base class rather than an interface:

- **Shared behavior lives once.** Staleness derivation against an injected
  clock is identical for a silent aircraft and a silent ship, so it is a
  non-abstract member on the base, implemented once, tested once.
- **`abstract` makes the per-source parts explicit.** `Label` and
  `GroupingKey` are abstract — a new source cannot forget to answer them, and
  the compiler says so, which an interface default would have hidden.
- **Key and last contact are set on construction** through a `protected`
  constructor, so no subclass can produce a keyless item.
- Keep the base free of `virtual` members nobody overrides; that is
  speculative inheritance and this is a demo.

A derived type adds its own detail on top, and **the detail pane is the only
place a derived type appears**. Everything else — cache, filters, comparers,
aggregates, bindings — is written against `TransportVehicle`.

The tradeoff is deliberate and worth stating out loud when the design is
reviewed: a thin base buys the swap and costs expressiveness in the grid. If a
column genuinely needs aircraft-only data, it comes from a source-supplied
column description, not from a downcast.

## `Aircraft : TransportVehicle`

From OpenSky `/states/all`. Fields are positional on the wire; the index
table in [`README.md`](../../README.md) is the contract and lives there, not
duplicated here.

- **`Icao24`** — the key. Lowercase hex.
- **`Callsign`** — optional, and the wire pads it to 8 characters. Trim it
  before it reaches the model; a padded label sorts and searches badly.
- **`OriginCountry`** — a grouping key.
- **`Position`** — latitude and longitude, each optional.
- **`BaroAltitude`** and **`GeoAltitude`** — two different altitudes, both
  optional. Keep both; do not collapse them into one and lose which was
  reported.
- **`OnGround`**, **`Spi`** — booleans.
- **`Velocity`**, **`TrueTrack`**, **`VerticalRate`** — optional. `TrueTrack`
  is degrees clockwise from north.
- **`Squawk`** — optional, a 4-digit transponder code and a string, not a
  number: leading zeros are meaningful.
- **`PositionSource`** — `0` ADS-B, `1` ASTERIX, `2` MLAT, `3` FLARM.
- **`Category`** — **only present when the request sets `extended=1`**. An
  aircraft fetched without it has no category, which is different from having
  an unknown one. Model the difference.
- `Sensors` is usually null and the demo has no use for it. Leave it off.

## `Vessel : TransportVehicle` (the stretch goal)

From AISStream.io, keyed by MMSI, with named JSON fields — see
[`ais-stream`](../ais-stream/SKILL.md). The model side is small: another
subclass, overriding `Label` with the vessel name and `GroupingKey` with flag
or ship type, inheriting position, last contact and staleness unchanged.

The one behavioral difference that matters: **vessels go silent rather than
being removed.** That is the same rule as a stale aircraft, so the staleness
derivation serves both and the expiry policy lives in the pipeline, not here.

## Units

- **SI is canonical in the model.** Metres, metres per second, degrees,
  instants. That is what both wires report, so the model stores what it was
  told.
- Feet, knots and local times are a *display* concern. Convert at the view or
  in an explicit named conversion, never silently inside a mapper — see
  [`mapping`](../mapping/SKILL.md).
- A converted value never replaces the canonical one on the model.

## Optional values

Both wires are full of nullable fields. Use LanguageExt `Option<T>`
(`LanguageExt.Core` is already referenced in
[`Directory.Packages.props`](../../Directory.Packages.props)) at the boundary
so "no fix reported" is a value rather than a null to be remembered. See
[`language-ext-usage`](../language-ext-usage/SKILL.md) for how far that goes
and where it stops.

A missing position and a position of `0,0` are not the same thing. The Gulf
of Guinea is a real place.

## Never add

- MAUI, Akka, DynamicData, `HttpClient` or `System.Text.Json` types to the
  model. It is referenced by everything; it references nothing but the
  framework and LanguageExt.
- Mutable collection state. Collections are the cache's job.
- Wire-shaped positional arrays past the client boundary. The positional
  array is a transport detail and dies at the `JsonConverter`.
- A `IsStale` field. Derive it on the base; a stored flag goes wrong the
  moment the clock moves.
- A downcast, `is` check or `switch` on the subclass outside the detail pane.
  That is the swap breaking, one line at a time.
- A third level of inheritance. One abstract base, one concrete type per
  source, and that is the whole hierarchy.
- A subclass-specific member hoisted onto the base so a view can reach it.
  Squawk codes and MMSIs are not shared.
