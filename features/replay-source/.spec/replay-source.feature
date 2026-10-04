@replay-source
Feature: Replay source — recording, playback, and selection

  As the presenter running this demo on a venue network I do not control
  I want the payloads a live provider actually sent, recorded verbatim and fed back at the
    spacing they arrived with, through the same seam and the same switch the live sources use
  So that a dead network costs the audience nothing, the closing act has a fallback, and the
    pipeline's indifference to its source is shown rather than asserted

  # Scenarios are documentation. There is no Gherkin runner in this repository;
  # the xUnit tests execute, each citing the claim it proves. Every value below
  # is synthetic: invented callsigns, icao24 values, MMSIs and positions. A real
  # rehearsal recording is operational data and never a fixture.

  # ──────────────────────────────── Recording ────────────────────────────────

  @B-001
  Scenario: A recorded line holds the payload verbatim beside the instant it arrived
    Given a live aircraft source that has just received a payload reporting a time of 14:32:10
      And that payload arrived at 14:32:11.113
     When the payload is recorded
     Then the recording gains exactly one line
      And that line carries the payload exactly as the provider sent it
      And it carries 14:32:11.113 as the instant the payload arrived
      And the payload on the line is not parsed, renamed, or reshaped

  @B-002
  Scenario: A payload with nothing in it is recorded as received
    Given a live aircraft source that receives a payload reporting no aircraft at all
     When the payload is recorded
     Then the recording gains one line for it
      And the line carries the empty payload rather than being skipped

  @B-003
  Scenario: Recording spends no additional credit and opens no additional connection
    Given a live aircraft source polling every 15 seconds
     When recording is turned on and four polls elapse
     Then four payloads are recorded
      And the provider has been asked for data four times in total
      And no second poller, socket or subscription to the provider was created

  @B-004
  Scenario: Turning recording on changes nothing the fleet sees
    Given a recorded sequence of payloads fed through a live source with recording off
      And the changesets the fleet received were observed
     When the same sequence is fed through the same source with recording on
     Then the fleet receives the same changesets, in the same order, with the same contents

  @B-005
  Scenario: A recording meant for the stage is long enough to show a vehicle going stale
    Given a staleness threshold of five minutes
     When a recording is captured for use on stage
     Then it spans at least five minutes
      And replaying it shows an aircraft appearing, updating, and becoming stale

  @B-006
  Scenario: A rehearsal recording is operational data, not a fixture
    Given a recording captured during rehearsal from the live provider
     When the repository is inspected
     Then the recording is not committed
      And no test reads it
      And every fixture a test does read is synthetic and committed beside that test

  # ──────────────────────────────── Playback ─────────────────────────────────

  @B-007
  Scenario: Payloads are replayed in order, at the spacing they arrived with
    Given a recording of three payloads that arrived at 14:32:11, 14:32:26 and 14:32:41
     When the recording is replayed
     Then the payloads are emitted in that order
      And 15 seconds of the replay's time base separate each one from the next
      And no fixed interval of the replay's own is used in place of the recorded spacing

  @B-008
  Scenario: Replay loops without emptying the fleet at the seam
    Given a recording of three payloads, the last reporting two aircraft
     When the recording is replayed past its end
     Then the first payload follows the last
      And the stream neither completes nor faults
      And no changeset at the loop boundary removes every aircraft at once

  @B-009
  Scenario: The observed instant is the one the provider reported, not the one we recorded
    Given a recorded line whose payload reports a time of 14:32:10
      And whose recorded arrival instant is 14:32:11.113
     When the line is replayed
     Then everything downstream treats 14:32:10 as the observed instant
      And 14:32:11.113 is used only to space this payload from the next
      And no consumer reads an ambient clock to supply an instant

  @B-010
  Scenario: A vehicle goes stale at the same point on replay as it did live
    Given a staleness threshold of five minutes
      And a recording in which aircraft "a1b2c3" stops being reported six minutes before the end
     When the recording is replayed
     Then "a1b2c3" becomes observably stale at the same point in the recording at which it did live
      And it remains in the fleet rather than being removed
      And the clock that decided it is the injected one, not the wall clock

  @B-011
  Scenario: Replay runs with the provider unreachable and no credential configured
    Given no credentials are configured
      And the provider cannot be reached from this machine
     When a recording is replayed
     Then the fleet fills from the recording
      And no network request is attempted

  @B-012
  Scenario: A recording truncated mid-write replays without faulting
    Given a recording whose final line was cut off part-way through being written
     When the recording is replayed
     Then every complete line before it is replayed
      And the truncated line is discarded
      And the stream does not fault

  @B-013
  Scenario: A recorded payload travels the same projection as a live one
    Given a payload that produces aircraft "a1b2c3" when it arrives live
     When the same payload is replayed from a recording
     Then the aircraft produced is identical to the one the live path produced
      And it was built by the same projection, with no second converter for recorded payloads

  @B-014
  Scenario: A converter fix reaches an existing recording with no re-recording
    Given a recording captured before a fix to the payload converter
      And the converter is then corrected
     When the same recording is replayed
     Then the corrected behavior is observed
      And the recording did not have to be captured again

  # ───────────────────────────── Selection and swap ──────────────────────────

  @B-015
  Scenario: Replay is reached through the same switch as any live source
    Given the live aircraft source is selected
     When the presenter selects replay
     Then replay becomes the live source through the same selection mechanism
      And no offline-mode flag, environment switch or replay-only selector was involved

  @B-016
  Scenario: Nothing downstream can tell replay from a live provider
    Given replay is the selected source
     When a consumer observes the seam
     Then it cannot determine which source produced any change
      And it cannot determine that a swap occurred at all

  @B-017
  Scenario: Swapping to replay stops the outgoing live source
    Given the live aircraft source is selected and polling
     When the presenter swaps to replay
     Then the outgoing poller stops
      And no further credit is spent on the outgoing source
      And an outgoing socket, if there was one, stops reading

  @B-018
  Scenario: The pipeline survives the swap to replay untouched
    Given a fleet with a search filter, a chosen sort column and a grouped view
     When the presenter swaps from the live source to replay
     Then the tracker, its collection, the filter, the sort, the groups and the bindings are the same instances
      And the fleet refills from the recording

  # ───────────────────────────────── Vessels ─────────────────────────────────

  @B-019
  Scenario: A vessel recording is one line per message
    Given a vessel feed that pushes three messages for MMSI "366123456" in eight seconds
     When the messages are recorded
     Then the recording holds three lines, one per message
      And their recorded arrival instants reproduce the eight seconds between first and last
      And the messages were not batched into poll-shaped snapshots

  @B-020
  Scenario: The closing act has a recorded fallback before it is presented
    Given the closing act swaps the aircraft feed for a vessel feed
     When the demo is declared ready to present
     Then a vessel recording exists
      And selecting it drives the same fleet the live vessel feed drives

  @B-021
  Scenario: Replay introduces no vehicle or record type of its own
    Given recordings exist for both the aircraft feed and the vessel feed
     When the replay source is inspected
     Then it declares no vehicle, record or snapshot type of its own
      And each recording produces the types that recorded provider's own feature defines
