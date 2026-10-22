---
title: "Stage-day runbook"
description: "What to do before the talk, on the day, and when something breaks — the operational half of the demo's resilience plan."
type: guide
---

# Stage-day runbook

The code is only half the deliverable; the other half is it working in a room
with bad wifi. The facts behind each step are in
[`README.md`](../README.md) §§ "Limits", "Gotchas", "Demo resilience" and
"Closing act", and in
[decision 0001](../src/Transporter/Integrations/OpenSky/.spec/decisions/0001-houston-bounding-box.md);
this file is the order to do them in.

## Before the talk

- **Register with the provider and create an API client.** Registered access is
  worth an order of magnitude in budget over anonymous access
  (`README.md` § "Limits"). Tracked in `README.md` § "Open items".
- **Verify every credential** — the polled provider's client id and secret, and
  the push provider's key for the closing act. An expired key discovered
  mid-sentence is the worst possible time.
- **Build the head on the machine that holds the secret store.** The polled
  provider's credentials are packaged into the bundle when it is built
  (`README.md` § "Authentication"), so a bundle built elsewhere, or before the
  store was set, has none — and one built here is never copied off the machine.
  Setting, checking and rotating the store is
  [`credentials.md`](credentials.md).
- **Record replay data in rehearsal**, aircraft _and_ vessels
  ([`features/replay-source`](../features/replay-source/.spec/README.md)).
  Record long enough that items go stale on playback, or the staleness part of
  the talk has nothing to show. The head records into its own container;
  `tools/copy-recordings.sh` copies what it wrote to `.recordings/` at the
  repository root, which is ignored.
- **Add the citation slide** the provider's terms ask for
  (`README.md` § "Gotchas").
- **Check the box has traffic at the talk's hour.** The vessel subscription uses
  the same box as the aircraft, which is why the box contains a port. An empty
  sea is a bad slide.

## Recordings on Mac Catalyst

The head's default input directory is
`~/Library/Containers/<application-id>/Data/Library/recordings/`.
The application id is declared in [`src/Gui/Gui.csproj`](../src/Gui/Gui.csproj)
(currently `com.companyname.gui`). The head obtains this location through
`FileSystem.AppDataDirectory`; it is not a path committed to settings.
Rehearsal recording and replay both use the effective `Recording:Root`.

1. For a synthetic aircraft sample on macOS, run this from the checkout:

    ```sh
    ./build.sh --target SampleRecordingForApp
    ```

    The target reads the application id from the MAUI project and writes
    `aircraft-sample.ndjson` directly into the directory above. Check the printed
    destination, payload count and span; the defaults produce 40 payloads
    spanning 9 minutes 45 seconds. It needs no provider credentials and is
    deliberately outside the normal build. Rerunning it replaces that sample;
    it does not copy or bundle a recording. To choose a different synthetic
    sequence, length or cadence:

    ```sh
    ./build.sh --target SampleRecordingForApp --seed 79 --polls 41 --poll-interval 10
    ```

    This spans 6 minutes 40 seconds. A span must exceed the five-minute staleness
    threshold or the generator refuses it. For a destination outside the default
    app container, use the existing `SampleRecording --recording <path>` target;
    `SampleRecordingForApp` rejects `--recording` and requires macOS. If you
    overrode `Recording:Root`, use that explicit destination instead.

2. Name the sample under `Replay:Aircraft` without a settings-file edit:

    ```sh
    dotnet user-secrets set "Replay:Aircraft" "aircraft-sample.ndjson" --id transporter-gui
    ```

    For rehearsal traffic, put the intended recording in the effective
    `Recording:Root` and name that file instead. The captured vessel recording
    must span at least `AisStream:SilentAfter` (ten minutes by default). Name it
    separately under `Replay:Vessels` and rebuild:

    ```sh
    dotnet user-secrets set "Replay:Vessels" "<vessel-file>.ndjson" --id transporter-gui
    ```

    The existing `Replay:Aircraft` setting remains independent. To undo the
    aircraft sample setup, delete only `aircraft-sample.ndjson` from the
    directory above and remove or replace its `Replay:Aircraft` setting, then
    rebuild.

3. Build the head after changing its user-secrets store. The platform root is
   the lowest-priority default; a `Recording:Root` in packaged settings wins
   over it, and one in user secrets wins over both. An override must still be
   accessible inside the sandbox. Existing files under the earlier
   `Data/Documents/recordings/` workaround are not moved automatically.

4. Capture startup output before rehearsing or presenting. Set `demo_bundle`
   to the built `Gui.app` (the example is Debug on Apple Silicon; use the
   corresponding Release or `maccatalyst-x64` output when appropriate):

    ```sh
    demo_bundle="$(pwd)/src/Gui/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Gui.app"
    replay_log="$(mktemp -t transporter-replay)"
    open -n --stdout "$replay_log" --stderr "$replay_log" "$demo_bundle"
    ```

5. Once the window opens, inspect `cat "$replay_log"`. For every named
   recording, confirm the source, resolved path and span. A usable recording
   is reported as available without selecting it. An absent file, a file
   that cannot be opened or read, a file with no readable recorded line, and
   a recording below the staleness threshold have distinct diagnostics.
   Those sources are unavailable; the application still starts. Console
   reporting is enabled in Debug and Release, so a debugger is not required.

6. Export rehearsal captures with `tools/copy-recordings.sh`. It reads the
   default `Data/Library/recordings/` directory and copies captures to ignored
   `.recordings/` at the repository root. That export is not replay's input
   directory. If you overrode `Recording:Root`, copy from that location
   yourself; the helper does not discover overrides. The export deliberately
   excludes `aircraft-sample.ndjson`; regenerate that sample with step 1.

Configuration names the recording; the source selector determines which feed
is live. The startup report neither selects replay nor starts its source.
Do not treat a usable-recording message as proof that replay is selected.

## On the day

- **Run from a laptop, not a cloud VM.** The provider blocks hyperscaler IPs
  (`README.md` § "Gotchas"). This is not a performance preference; the demo
  simply will not get data.
- **Check the remaining budget before starting.** If it is nearly spent, open on
  replay and say so — it costs one sentence and removes the risk entirely.
- **Have replay one control away, not one rebuild away.** It is the same source
  selector as the live swap
  ([`hot-swap-source`](../.skills/hot-swap-source/SKILL.md)).
- **Keep credential values off screen.** Secrets come from the user-secrets
  store, and a shared screen is a screenshot.

## The closing act

Swap the polled feed for the push feed **in the running app** and let the
audience watch the same grid, filters, sorts and groups refill.

What they should be watching is _the code that did not change_. Have the
pipeline on screen, or ready to show immediately after: same cache, same
`Filter`, same `Sort`, same `Group`, same `Bind`. That is the whole argument.

Rehearse:

- the swap, more than once in a row, watching memory and budget burn stay flat;
- the swap with the recorded vessel replay as the target, so a dead network does
  not take the ending with it. **A stretch goal that only works on a good
  network is a gamble, not a demo**;
- swapping while a poll is in flight — the late response must not land;
- swapping twice quickly, because someone will ask.

The failure modes and the gap-on-swap decision live in
[`hot-swap-source`](../.skills/hot-swap-source/SKILL.md). Settle what the
audience sees during the gap in rehearsal and stick to it; deciding on stage
looks like a bug.

## If something breaks

| Symptom                                       | First move                                                                                                   |
| --------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| No data at all, live source                   | Check the IP — a cloud VM, or a VPN exiting through one. Switch to replay.                                   |
| Throttle responses                            | The budget is spent, or the retry is ignoring the provider's retry-after header. Switch to replay.           |
| Grid empty after the swap                     | The push feed never authenticated or never emitted. Swap to recorded vessel replay.                          |
| Grid filling with items that should have gone | Expiry or staleness is not running — the clock is wrong. Mention it, move on; it is one slide, not the talk. |

In every row the answer is the same shape: **swap the source, keep talking.**
That is the resilience plan the architecture exists to support, and saying so
out loud makes the failure part of the lesson.

## Never

- A cloud-hosted run of the live demo.
- A credential on screen, in a log, or in a screenshot.
- A closing act with no recorded fallback.
- A rehearsal that skips the swap, or skips letting items go stale.
