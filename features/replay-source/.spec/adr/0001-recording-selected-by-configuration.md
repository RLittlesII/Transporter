---
title: "ADR-0001: A run's recording is named in configuration, not discovered"
description: "Configuration names the recording each replay source reads, with no default compiled in and no newest-wins discovery, and the pacer is handed an opened stream rather than a path."
type: adr
---

# ADR-0001: A run's recording is named in configuration, not discovered

**Status:** proposed

## Context

[ADR-0004](../../../../.spec/adr/0004-ndjson-recording-format.md) fixes what a
recording _is_ — NDJSON, one line per payload, named
`recordings/<source>-<utc-instant>.ndjson` — and deliberately says nothing
about which recording a run loads or where that directory sits relative to the
running application. The specification carried that gap as § 11 row 3.

It is not a format question and it cannot be deferred past design. Two items
need it answered: selection is where a run's recording surfaces, and the pacer
needs to know whether it is handed a path or a stream.

Three facts constrain it. **B-015** says replay is selected through the same
mechanism as any live source, with no second selection path — so whatever
names a recording must not become a second way to turn replay on. **B-011**
says replay needs no network and no credential, so nothing about this may
reintroduce an external dependency. And a rehearsal recording is operational
data that is never committed (B-006, ADR-0004), so a recording cannot be
embedded in the application and shipped with it.

The precedent is aircraft-source B-050: the polling interval is configurable
with a default, and the bounding box is configurable **with no default compiled
in**. A value that is wrong-but-plausible when unset is worse than one that is
absent.

## Decision drivers

- A wrong recording on stage must be impossible to select silently. This runs
  in front of people once.
- Rehearsal writes recordings and the stage reads them; neither should have to
  name an absolute path.
- Configuration answers _which_ recording. The selector answers _whether_
  replay is live. Neither may grow into the other.
- A test must reach the pacer without a file system.
- The failure a presenter cares about — the recording is missing — should be
  visible before the talk, not at the moment of the swap.

## Considered options

| Option                                             | Summary                                                                                                                                                                        | Why not                                                                                                                                                                                                                                                                 |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Newest recording wins                              | Configuration names the directory; the app picks the most recent recording per source, ordering by the UTC instant already in the filename. Nothing to edit after a rehearsal. | A thirty-second test capture taken after the real rehearsal silently becomes the stage recording, and B-005 — a recording long enough to show staleness — fails invisibly at the one moment it matters. Convenience bought with a silent wrong answer.                  |
| The presenter picks one in the UI                  | A list of available recordings, chosen at or before the swap.                                                                                                                  | A second selection surface for replay, which B-015 forbids and § 5 row 8 excludes; it is also a transport control by another name (§ 5 row 5). The presenter already has one switch, and adding a second thing to get right under pressure is the opposite of the goal. |
| A recording embedded in the application            | Ship a known-good recording inside the bundle, so there is always one.                                                                                                         | A recording is operational data that is never committed (B-006); embedding it commits it to the build artifact instead, with real callsigns and positions in a redistributable.                                                                                         |
| **Named in configuration, per source, no default** | **Chosen.** One key per replay source names the recording; paths resolve against one configured recordings root.                                                               | —                                                                                                                                                                                                                                                                       |

## Decision

1. **Configuration names the recording**, one key per replay source, through the
   same configuration mechanism the interval and the bounding box use — not an
   environment-only flag and not a second file. **No default is compiled in**:
   with no recording named, that replay source is not registered and therefore
   is not selectable, which is a visible absence rather than a wrong recording.
2. **Paths resolve against one configured recordings root**, defaulting to
   `recordings/` relative to the running application, so rehearsal writes and
   replay reads the same place without either naming an absolute path. The root
   is overridable for the platforms where a working directory is not where a
   person would look.
3. **Configuration never decides whether replay is live.** It names which
   recording a replay source would read; the source selector (B-015) decides
   whether that source is the live one. A recording named in configuration and
   never selected is normal.
4. **A configured recording that is absent, unreadable, or shorter than the
   staleness threshold is reported when the application starts**, not when the
   presenter swaps. This is the same shape as aircraft-source B-029: a missing
   credential is the application's behaviour at startup, not a surprise at a
   call site.
5. **The pacer is handed an opened payload stream, not a path.** Resolving
   configuration to a stream happens once, where the source is registered. The
   pacer performs no file resolution, so a test feeds it synthetic lines with no
   file system at all.

Claims B-024 – B-027 carry these in § 3.

## Consequences

- **A rehearsal costs one configuration edit** before the talk — the recording's
  name is the new timestamped file. That edit is the deliberate step that
  newest-wins removed, and it is the one that makes B-005 checkable.
- **Replay can be silently unavailable if nobody edits the configuration**, and
  the mitigation is item 4: startup reports it. A presenter who never looks at
  startup output still has the swap fail in the way an unregistered source fails
  — the control is absent rather than offering a wrong recording.
- **Two recordings are named, not one.** Aircraft and vessels each get a key, so
  the closing act's fallback (B-020) is configured rather than assumed.
- **Startup reads each configured recording far enough to check its span**,
  which at demo length is cheap and at an hour's recording is not. If a longer
  recording ever matters, the span check is what to revisit — not the naming.
- **The stream-not-path decision moves file access out of the pacer**, so the
  component with the time-base logic has no I/O to stub. It also means a
  recording is opened once per selection rather than per loop, and looping
  rewinds the same stream.
- **This is reversible per item.** Newest-wins could be added later as a
  resolution strategy behind the same configuration key without touching the
  pacer, the contract substitution, or the selector — which is why this record
  is scoped to this Feature rather than the repository.
