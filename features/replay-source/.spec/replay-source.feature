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
    Given a live aircraft source that has just received a newline-free JSON payload reporting a time of 14:32:10
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

  @B-005
  Scenario: A vessel stage recording covers its own removal threshold
    Given vessels are removed after ten minutes of silence
     When a vessel recording is prepared for the closing act
     Then its span exceeds ten minutes
      And replaying it shows a vessel appearing, updating, and being removed on silence

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

  # ─────────────────────────── The substitution point ────────────────────────

  @B-022
  Scenario: Replay stands in for the provider's contract and brings nothing else
    Given a recording of aircraft payloads
     When replay is built
     Then it is an implementation of the provider's own API contract, handing back recorded envelopes
      And the snapshot client above it is the same class the live path uses
      And replay declares no snapshot client, cache, converter or projection of its own

  @B-023
  Scenario: Vessel replay stands in at the strategy, because there is no contract to stand in for
    Given a recording of vessel messages
      And the vessel provider is a socket with no contract layer
     When vessel replay is built
     Then it is an `ITrackerSourceStrategy` fed by the recording
      And it reaches the seam the same way the live vessel source does
      And no contract is invented for the socket so that replay can substitute below it

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
      And no replay marker or source identity is added to the seam

  @B-017
  Scenario: Swapping to replay stops the outgoing live source
    Given the live aircraft source is selected and polling
     When the presenter swaps to replay
     Then the outgoing poller stops
      And no further credit is spent on the outgoing source
      And an outgoing socket, if there was one, stops reading

  @B-017
  Scenario: Swapping away from vessel replay cancels its pending frame
    Given recorded vessels are selected and playback is waiting for the next frame
     When the presenter selects another source
     Then the vessel replay subscription is disposed
      And no later recorded frame enters its cache through that subscription

  @B-018
  Scenario: The pipeline survives the swap to replay untouched
    Given a fleet with a search filter, a chosen sort column and a grouped view
     When the presenter swaps from the live source to replay
     Then the tracker, its collection, the filter, the sort, the groups and the bindings are the same instances
      And the fleet refills from the recording

  # ──────────────────────── Which recording a run loads ──────────────────────

  @B-024
  Scenario: Configuration names the recording, and names nothing about what is live
    Given configuration naming one recording for the aircraft replay source
      And no recording named for the vessel replay source
     When the application starts
     Then the aircraft replay source is registered and selectable
      And the vessel replay source is neither registered nor selectable
      And the live aircraft source is still the one selected, because configuration decided only which recording

  @B-025
  Scenario: A named recording resolves against the configured recordings root
    Given a recordings root configured as "recordings"
      And a recording named "aircraft-2026-10-04T14-22-01Z.ndjson"
     When the recording is opened
     Then it is read from "recordings/aircraft-2026-10-04T14-22-01Z.ndjson" relative to the running application
      And neither the configuration nor the recording's name carried an absolute path

  @B-026
  Scenario: A recording that cannot serve is reported at startup, not at the swap
    Given a staleness threshold of five minutes
      And configuration naming a recording that spans ninety seconds
      And configuration naming a second recording that does not exist
     When the application starts
     Then both are reported — one as too short to show staleness, one as absent
      And neither was first discovered at the moment replay was selected

  @B-026
  Scenario: A vessel recording is checked against the configured vessel threshold
    Given vessels are removed after ten minutes by default
      And a named vessel recording spans six minutes
     When the application starts
     Then the recording is reported too short and vessel replay is not selectable
      And changing AisStream:SilentAfter to twenty minutes makes a twelve-minute recording too short as well

  @B-027
  Scenario: The pacer is handed a stream and resolves nothing
    Given a recording that has already been opened
     When the pacer is constructed
     Then it is handed the opened payload stream
      And it reads no configuration and resolves no path
      And a test can drive it with synthetic lines and no file system

  # ───────────────────────────────── Vessels ─────────────────────────────────

  @B-019
  Scenario: A vessel recording is one line per message
    Given a vessel feed that pushes three messages for MMSI "366123456" in eight seconds
     When the messages are recorded
     Then the recording holds three lines, one per message
      And their recorded arrival instants reproduce the eight seconds between first and last
      And the messages were not batched into poll-shaped snapshots

  @B-008 @B-009 @B-010
  Scenario: Vessel replay restarts its provider time without emptying the fleet
    Given a recording with timed vessel messages and an untimed message
      And an older provider message within the first pass
     When replay reaches the end and starts a second pass
     Then the first timed message of that pass resets the observed clock to its provider time
      And each vessel's first timed message rebases its held last contact
      And untimed messages do not erase a held last contact or advance the clock
      And later older messages in that pass do not move the clock or a last contact backwards
      And the retained cache emits no change that removes every vessel at the boundary
      And a vessel silent beyond its threshold is removed again in the second pass

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

  @B-028
  Scenario: A run starts on a live source even when a recording is named
    Given a live aircraft source is registered
      And an aircraft recording is named in configuration
     When the application starts
     Then the live source is the one selected
      And the recorded source is registered after it, reachable only by a swap
      And no configuration, environment or launch value changes which is selected

  @B-025
  Scenario: The sandboxed head uses its application-data directory when no root is configured
    Given the sandboxed head has an application-data directory
      And no recordings root is supplied by packaged settings or user secrets
     When rehearsal records a payload and replay resolves its recording name
     Then both use the recordings directory beneath that application-data directory
      And no absolute machine path was needed in committed configuration

  @B-025
  Scenario: An explicit root overrides the platform default
    Given a platform default recordings root
      And packaged settings name a different recordings root
     When application configuration is composed
     Then recording and replay both use the packaged root
      And a root in user secrets overrides that packaged root when supplied

  @B-026
  Scenario: The head reports recording defects without starting a generic host
    Given a staleness threshold of five minutes
      And named recordings that are absent, cannot be opened, contain no readable recorded line, or span ninety seconds
      And the application is built without starting generic hosted services
     When the head invokes its startup report before returning the built application
     Then every named recording is reported with its source, resolved path and distinct defect
      And no unusable replay source is registered
      And the application still starts
      And no provider request or source selection was needed to produce the report

  @B-026
  Scenario: A recording that fails while being inspected is reported and released
    Given a named recording whose opened stream fails with an I/O error during inspection
     When the application starts
     Then the recording is reported as unreadable
      And its stream is released
      And its replay source is not registered
      And the application still starts

  @B-026
  Scenario: A usable recording is reported without being selected
    Given a named recording spanning at least the staleness threshold
     When the application starts
     Then the report names its source, resolved path and measured span as usable
      And the report does not select that recording or start its source

  @B-001
  Scenario: A multiline payload is preserved with an unsupported-format warning
    Given a provider payload containing a raw line break
     When the payload is recorded
     Then the payload is preserved verbatim
      And a warning says the one-line recording format is broken
      And replay of that payload is outside the supported recording format

  @B-009 @B-010
  Scenario: Returning to a retained source restores its provider time before its vehicles
    Given an aircraft recording has published fresh aircraft and its reported instant
      And vessel replay then advances the observed instant to a later day
      And the next aircraft payload is still waiting for its recorded spacing
     When the aircraft source is selected again
     Then its retained aircraft are measured against its own last reported instant immediately
      And no aircraft is marked stale because of the outgoing vessel instant
      And the next payload remains paced by the recording

  @B-009 @B-010
  Scenario: Returning to vessel replay restores its provider time before its vehicles
    Given vessel replay has published vessels and its reported instant
      And another source then reports an instant on a different day
      And the next vessel frame is still waiting for its recorded spacing
     When vessel replay is selected again
     Then its retained vessels are measured against its own last reported instant immediately
      And no vessel is removed because of the outgoing source instant
      And a source that has never reported an instant invents no time
