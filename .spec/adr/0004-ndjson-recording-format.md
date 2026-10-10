---
title: "ADR-0004: NDJSON append log for rehearsal recordings"
description: "Record rehearsal snapshots as an NDJSON append log — one line per snapshot, the provider's payload verbatim beside the instant it arrived — so replay needs no persistence package and survives a converter fix."
type: adr
---

# ADR-0004: NDJSON append log for rehearsal recordings

**Status:** proposed

## Context

Replay is not a testing afterthought. `README.md` § "Demo resilience" commits
to "replay from recorded snapshots", and
[the stage-day runbook](../../docs/runbook.md) makes it a control one
switch away during the talk — the thing that goes on stage when the venue
network dies. [`features/replay-source`](../../features/replay-source/.spec/README.md) says what to
record: **raw** snapshots as received, with the instant they arrived, long
enough that items go stale on playback.

What it did not say is where they go. Its § "Recording" offered "one file or
one line per snapshot" — two entirely different on-disk shapes, never picked —
and nothing else in the repository picked either. No persistence package is in
`Directory.Packages.props`, no recording path exists, and `.gitignore` has no
entry for recorded data.
[`src/Transporter/Integrations/OpenSky`](../../src/Transporter/Integrations/OpenSky/.spec/README.md)
§ 5 defers the replay strategy out of its own scope, so no Feature spec owns
the call. It is a decision both the aircraft replay and the vessel replay need
before either is built, which makes it this record's and not a Feature's.

One existing claim constrains any answer. B-003 requires the observed instant
to come from the provider's own envelope, with no consumer reading an ambient
clock. A recording format has to leave that envelope intact rather than
replace it with a timestamp of the recorder's own.

## Decision drivers

- **Raw and re-parsable.** A converter fix should be testable against
  recordings already taken. Parsed records bake the bug in.
- **Appendable while polling.** The recorder runs for minutes during rehearsal
  and may be stopped with a keystroke. Nothing should need a clean close for
  what was already written to be usable.
- **Playback only ever reads forward.** Snapshots are fed in order and the file
  rewinds to the start to loop. No query, no index, no seek.
- **The demo's subject is DynamicData.** A persistence dependency is a cost
  paid in audience attention, and so is any format that needs explaining.
- **Recordings are operational data.** Real callsigns, real positions. The
  format must be readable and scrubbable by eye before any of it is reused.

## Considered options

| Option                     | Summary                                                                                                                                  | Why not                                                                                                                                                                                                                                                                        |
| -------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| One JSON file per snapshot | A directory of timestamped files, one payload each. Trivially inspectable one at a time, and deleting a bad snapshot is deleting a file. | Hundreds of files for a few minutes of polling, and the playback order lives in the filenames rather than in the data. A snapshot interrupted mid-write is a silent gap in a directory listing, where in a line-oriented file it is a torn last line that reads as what it is. |
| SQLite                     | A `snapshot(seq, received_at, raw_body)` table. Durable, ordered, indexed.                                                               | A persistence package and a schema to serve playback that only reads forward. The indexing is bought and never used, and the recording stops being something `head` and `wc -l` can answer questions about — which matters because these files get scrubbed by hand.           |
| Parquet or a binary log    | Columnar or framed binary. Compact.                                                                                                      | Compression and column layout for a few minutes of JSON that is never aggregated. Not inspectable, not scrubbable without tooling, and a second serialization concept in a demo that has one.                                                                                  |
| **NDJSON append log**      | **Chosen.** One line per snapshot: the provider's payload verbatim, beside the instant it arrived.                                       | —                                                                                                                                                                                                                                                                              |

## Decision

Recordings are an **NDJSON append log** — one JSON object per line, one line
per snapshot:

```
recordings/aircraft-2026-10-04T14-22-01Z.ndjson
{"receivedAt":"2026-10-04T14:22:01.113Z","body":{"time":1759587721,"states":[[...]]}}
{"receivedAt":"2026-10-04T14:22:16.207Z","body":{"time":1759587736,"states":[[...]]}}
```

- **`body` is the provider's payload verbatim**, unparsed and unreshaped. That
  is what makes a recording re-playable after a converter fix.
- **`receivedAt` paces playback and nothing else.** It is the recorder's
  arrival instant, used to space snapshots on replay at their original
  cadence. It is **not** the observed instant: that stays on the provider's own
  envelope inside `body`, per B-003, and this record does not change who
  supplies it.
- **One file per rehearsal run per source**, named
  `recordings/<source>-<utc-instant>.ndjson`. Looping is a rewind to line one.
- **A torn final line is discarded on read.** A recorder stopped mid-write
  costs the snapshot it was writing and nothing before it.
- **AISStream is push, not polled**, so a recording there is one line per
  received message. Same shape, same fields — the closing act gets its
  fallback from the same format as the main demo.
- **No package.** `System.Text.Json` writes a line; `ReadLineAsync` reads one.

`recordings/` is git-ignored. A rehearsal recording is operational data, not a
fixture, and it is scrubbed before any of it is reused as one. Synthetic test
fixtures are unaffected by this record: they stay plain JSON committed beside
the tests that use them, per `test-from-scenarios` § "Fixtures are synthetic".

## Consequences

- **No dependency, no schema, no migration.** The format is a file the standard
  library already writes. It is also reversible: nothing downstream of the
  replay source knows how the recording is stored.
- **The recording stays legible.** `wc -l` counts snapshots, `head -1` shows
  one, and scrubbing before reuse is an edit a person can actually perform.
  That was the practical argument against SQLite and against binary.
- **No index, and no seek.** Jumping to the middle of a recording means reading
  to it. Acceptable for forward playback and looping; it would not be if
  anything ever needed to query a recording.
- **Uncompressed raw JSON on disk.** Minutes of OpenSky snapshots, so size is
  not a concern at demo length. A long recording would be, and compression is a
  later record if it ever matters.
- **`receivedAt` is a second time on every line**, next to the provider's own
  reported time inside `body`. Two timestamps invites exactly the confusion
  B-003 exists to prevent, so the field's one job — pacing — is stated here and
  must be stated wherever the replay source reads it.
- **Nothing is added to `Directory.Packages.props`**, so `README.md`
  § "Technology Decisions" gains no line. Like
  [ADR-0011](0011-the-swap-decorator-selects-among-registered-strategies.md) as it now stands,
  this record costs the talk no explaining.
