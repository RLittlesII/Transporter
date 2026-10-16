@vessel-source
Feature: Vessel source — a push feed on the same seam

  As a line-of-business .NET developer watching the closing act
  I want the polled aircraft swapped for a push feed of ships over the same water,
    held in a cache the push client writes itself and projected onto the same seam
  So that "nothing downstream changes" is shown with a source of a different shape,
    and a vehicle that goes silent can be seen to go

  # Scenarios are documentation. There is no Gherkin runner in this repository;
  # the xUnit tests execute, each citing the claim it proves. Every value below
  # is synthetic: invented MMSIs, ship names and positions inside the Houston box.
  # No API key, real or invented, appears in any scenario.

  Background:
    Given the configured bounding box runs from latitude 28.8 to 30.4 and longitude -95.9 to -94.4

  # ──────────────────────────────── Connection ───────────────────────────────

  @B-001
  Scenario: The vessel feed subscribes to the box the aircraft use
    Given an API key is configured
     When the vessel strategy becomes the live one
     Then the subscription names exactly one bounding box
      And that box is the configured box, with latitude before longitude in each corner
      And no box was configured for vessels separately

  @B-002
  Scenario: The subscription is the first frame on every connection
    Given an API key is configured
     When the vessel client opens a connection
     Then the first frame it sends is the subscription
      And it reads nothing before sending it
      And it sends no second subscription on that connection

  @B-003
  Scenario: The key reaches the client from the user-secrets store and nowhere else
    Given the developer's user-secrets store holds an AISStream API key
      And the packaged settings hold none
     When the vessel client subscribes
     Then the subscription carries the key from the store
      And no log line, recording or bound value contains the key

  @B-004
  Scenario: With no key, selecting vessels opens nothing and says why
    Given no AISStream API key is configured
     When the vessel strategy is selected
     Then no connection is opened
      And a failure naming the absent key can be read
      And selecting the aircraft strategy afterwards still works

  @B-005
  Scenario: A connection is open only while vessels are live
    Given an API key is configured
     When the presenter swaps from aircraft to vessels, back to aircraft, and to vessels again
     Then a vessel connection was opened on each swap to vessels
      And it was closed on the swap back to aircraft
      And at no point was more than one vessel connection open

  @B-006
  Scenario: A dropped connection is reopened and nothing held is lost
    Given vessels are live and "366999001" and "366999002" are held
     When the service closes the connection
     Then the vessel client opens a new connection and subscribes on it
      And "366999001" and "366999002" are still held
      And the tracker, the cache and the strategy were not rebuilt

  @B-007
  Scenario: Every connection asks for compression
    Given an API key is configured
     When the vessel client opens a connection
     Then the connection requests permessage-deflate

  # ───────────────────────────────── Messages ────────────────────────────────

  @B-008
  Scenario: A vessel is keyed by its nine-digit MMSI
    Given vessels are live
     When a position report arrives for MMSI 366999001
     Then the fleet holds a vessel keyed "366999001"
      And later reports for the same MMSI update that vessel without changing its key

  @B-009
  Scenario: Each position report is written on its own
    Given vessels are live and "366999001" is held at 29.70, -95.00
     When a position report puts "366999001" at 29.71, -95.01
      And a position report for "366999002" arrives
     Then "366999001" is updated in place and "366999002" is added
      And no set of all held vessels was assembled or diffed to write either change

  @B-010
  Scenario Outline: Static data names a vessel, whether it is held yet or not
    Given vessels are live and <held>
     When a static-data message reports "366999003" as "GULF TRADER" of ship type "Cargo"
     Then "366999003" is held with ship name "GULF TRADER" and ship type "Cargo"
      And its position is <position>

    Examples:
      | held                                         | position     |
      | "366999003" is held at 29.40, -94.90         | 29.40, -94.90 |
      | no vessel "366999003" is held                | absent       |

  @B-011
  Scenario: Only position reports and static data are asked for or used
    Given vessels are live
     When the subscription is sent
     Then it asks for position reports and static data only
     When a base-station report arrives anyway
     Then the fleet is unchanged

  @B-012
  Scenario: A message that cannot be read is skipped
    Given vessels are live and "366999001" is held at 29.70, -95.00
     When a message arrives that is not valid JSON
      And a position report puts "366999001" at 29.72, -95.02
     Then "366999001" is at 29.72, -95.02
      And the vessel stream has neither ended nor faulted

  @B-013
  Scenario Outline: AIS "not available" values become absent
    Given vessels are live
     When a position report for "366999004" reports <field> as <sentinel>
     Then the vessel's <member> is absent

    Examples:
      | field              | sentinel | member           |
      | latitude           | 91       | position         |
      | longitude          | 181      | position         |
      | true heading       | 511      | true heading     |
      | speed over ground  | 102.3    | speed over ground |
      | course over ground | 360      | course over ground |

  # ─────────────────────────────────── Time ──────────────────────────────────

  @B-014
  Scenario: Last contact is the instant the feed reported
    Given vessels are live
     When a position report for "366999001" reporting 14:32:10 UTC arrives at 14:32:12 by the wall clock
     Then the vessel's last contact is 14:32:10 UTC

  @B-015
  Scenario: A reported instant advances the observed clock; a message without one does not
    Given vessels are live and the observed clock reads 14:32:10
     When a message reporting 14:32:25 arrives
     Then the observed clock reads 14:32:25
     When a message reporting no instant arrives
     Then the observed clock still reads 14:32:25

  # ───────────────────────────── The vessel and its description ─────────────────────────────

  @B-016
  Scenario: A vessel is one level below the tracked item, and one place builds it
    Given vessels are live
     When a position report for "366999001" arrives
     Then the fleet holds a tracked item that is a vessel
      And it was built by the vessel strategy and by nothing else

  @B-017
  Scenario Outline: A vessel's label is its name, or its MMSI until it has one
    Given vessels are live
     When "366999005" is reported with ship name <name>
     Then its label is <label>

    Examples:
      | name             | label         |
      | "  BAY RUNNER  " | "BAY RUNNER"  |
      | absent           | "366999005"   |
      | "   "            | "366999005"   |

  @B-018
  Scenario: Speed and course are held as the feed reports them
    Given vessels are live
     When a position report for "366999001" reports 12.4 knots over ground, course 87.5 and heading 90
     Then the vessel holds 12.4 knots, a course of 87.5 degrees and a heading of 90 degrees

  @B-019
  Scenario: The vessel description offers ship type and flag as groupings
    Given vessels are live
     When the live description is read
     Then it names columns, comparers, filter choices and detail lines
      And its grouping keys are ship type and flag state
      And every one of them reads a tracked item rather than naming the vessel type

  @B-020
  Scenario Outline: Grouping falls back to Unknown
    Given vessels are live
     When "<mmsi>" is held <static>
     Then it groups under ship type "<type>" and flag state "<flag>"

    Examples:
      | mmsi      | static                                 | type    | flag          |
      | 366999001 | with no static data received           | Unknown | United States |
      | 366999003 | with static data reporting "Cargo"     | Cargo   | United States |
      | 999999001 | with no static data received           | Unknown | Unknown       |

  # ───────────────────────────────── Silence ─────────────────────────────────

  @B-021
  Scenario: A silent vessel is removed, and comes back as new if it speaks again
    Given the vessel silence threshold is 10 minutes
      And vessels are live and "366999001" last reported at 14:00:00
     When the observed clock passes 14:10:00 with nothing more from "366999001"
     Then "366999001" is no longer in the fleet or in any collection bound from it
     When a position report for "366999001" arrives at 14:12:00
     Then "366999001" is added to the fleet as a new arrival

  @B-022
  Scenario: Removal follows the observed clock, not the wall clock
    Given the vessel silence threshold is 10 minutes
      And vessels are live and "366999001" last reported at 14:00:00
     When an hour passes by the wall clock and the observed clock stays at 14:05:00
     Then "366999001" is still in the fleet
     When a message for another vessel advances the observed clock to 14:10:01
     Then "366999001" is removed with no message having arrived for it

  @B-023
  Scenario: Vessels are removed; aircraft are still only marked
    Given the vessel silence threshold is 10 minutes and the aircraft staleness threshold is 5 minutes
     When aircraft are live and "a1b2c3" has been silent for an hour of observed time
     Then "a1b2c3" is marked stale and is still in the fleet
     When vessels are live and "366999001" has been silent for 6 minutes of observed time
     Then "366999001" is still in the fleet

  # ─────────────────────────────── Seam and swap ───────────────────────────────

  @B-024
  Scenario: Vessels are offered by the swap control like every other source
    Given the application is composed with aircraft and vessel strategies
     When the swap control asks for its targets
     Then "Vessels" is among them
      And selecting it makes vessels the live source through the same seam
      And nothing on the seam names a vessel, a provider or a kind

  @B-025
  Scenario: While vessels are live there is no next poll
    Given aircraft are live and a next poll is due at 14:32:25
     When the presenter swaps to vessels
     Then the tracker publishes no poll status
      And nothing the aircraft poller reported is published after the swap

  @B-026
  Scenario: Swapping changes the vehicles and the description, and nothing else
    Given aircraft are live and grouped by origin country
     When the presenter swaps to vessels and back to aircraft
     Then the live description changed on each swap
      And the fleet held vessels while vessels were live and aircraft afterwards
      And the tracker's pipeline was neither rebuilt nor edited
