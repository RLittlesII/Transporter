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
      And an error is logged naming the setting "AisStream:ApiKey" and no value
      And selecting the aircraft strategy afterwards still works

  @B-005
  Scenario: A connection is open only while vessels are live
    Given an API key is configured
     When the presenter swaps from aircraft to vessels, back to aircraft, and to vessels again
     Then a vessel connection was opened on each swap to vessels
      And it was closed on the swap back to aircraft
      And at no point was more than one vessel connection open

  @B-005
  Scenario: A reconnect closes the old connection first, and a swap during one leaves none
    Given vessels are live and the service has closed the connection
     When the vessel client reconnects
     Then the old connection was closed before the new one was opened
     When the service closes the new connection and the presenter swaps to aircraft before the reconnect delay passes
     Then no vessel connection is open
      And none is opened when the delay passes

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
     When the subscription confirmation reports compression not granted
     Then a warning saying so is logged
      And the vessel stream stays open

  @B-007
  Scenario: A binary compression confirmation is read across receive fragments
    Given vessels are live and compression was requested
     When a binary WebSocket message arrives in two receive fragments carrying a UTF-8 JSON subscription confirmation that denies compression
     Then a warning saying compression was not granted is logged
      And the vessel stream stays open

  # ───────────────────────────────── Messages ────────────────────────────────

  @B-008
  Scenario Outline: A vessel is keyed by its nine-digit MMSI
    Given vessels are live
     When a position report arrives reporting MMSI <reported>
     Then the fleet holds a vessel keyed "<key>"
      And later reports for the same MMSI update that vessel without changing its key

    Examples:
      | reported  | key       |
      | 366999001 | 366999001 |
      | 36699900  | 036699900 |

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
     When a static-data message reports "366999003" as "GULF TRADER" of ship type 70
     Then "366999003" is held with ship name "GULF TRADER" and ship type "Cargo"
      And its position is <position>

    Examples:
      | held                                         | position     |
      | "366999003" is held at 29.40, -94.90         | 29.40, -94.90 |
      | no vessel "366999003" is held                | absent       |

  @B-010
  Scenario: A vessel no message has timed is held but not yet in the fleet
    Given vessels are live and no vessel "366999006" is held
     When a static-data message reporting no instant names "366999006"
     Then "366999006" is held in the vessel cache
      And it is not in the fleet
     When a position report for "366999006" reporting 14:32:10 UTC arrives
     Then "366999006" is in the fleet with last contact 14:32:10 UTC

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
      | latitude           | missing  | position         |
      | longitude          | missing  | position         |

  # ─────────────────────────────────── Time ──────────────────────────────────

  @B-014
  Scenario: Last contact is the instant the feed reported
    Given vessels are live
     When a position report for "366999001" reporting 14:32:10 UTC arrives at 14:32:12 by the wall clock
     Then the vessel's last contact is 14:32:10 UTC
     When a static-data message for "366999001" reporting no instant arrives
     Then the vessel's last contact is still 14:32:10 UTC

  @B-015
  Scenario: A reported instant advances the observed clock; a message without one does not
    Given vessels are live and the observed clock reads 14:32:10
     When a message reporting 14:32:25 arrives
     Then the observed clock reads 14:32:25
     When a message reporting no instant arrives
     Then the observed clock still reads 14:32:25
     When a message reporting 14:32:18 arrives
     Then the observed clock still reads 14:32:25

  @B-014 @B-015
  Scenario Outline: Provider UTC text is read at tick precision
    Given vessels are live
     When a <kind> message carries MetaData.time_utc "2026-10-20 14:32:25.123456789 +0000 UTC"
     Then the vessel's last contact is 2026-10-20 14:32:25.1234567 UTC
      And the observed clock reads 2026-10-20 14:32:25.1234567 UTC
    Examples:
      | kind           |
      | position       |
      | static-data    |

  @B-014 @B-015
  Scenario: Late messages preserve each vessel's latest contact independently
    Given vessel "366999001" last reported 14:32:25 UTC
      And vessel "366999002" last reported 14:32:18 UTC
     When vessel "366999001" reports 14:32:10 UTC
     Then its last contact is still 14:32:25 UTC
     When vessel "366999002" reports 14:32:20 UTC
     Then its last contact is 14:32:20 UTC
      And the observed clock is still 14:32:25 UTC

  @B-014 @B-015
  Scenario: An unreadable metadata instant leaves time unchanged and the message readable
    Given vessels are live and a vessel last reported 14:32:25 UTC
     When its position report has an unreadable MetaData.time_utc and payload Timestamp 30
     Then its position is updated
      And its last contact and the observed clock remain 14:32:25 UTC

  @B-015
  Scenario: Equal or late vessel times do not overwrite another writer
    Given the vessel client last reported 14:32:25 UTC
      And another source has reset the shared observed clock to 13:00:00 UTC
     When a vessel message reports 14:32:25 UTC
     Then the observed clock still reads 13:00:00 UTC
     When a vessel message reports 14:32:26 UTC
     Then the observed clock reads 14:32:26 UTC

  # ───────────────────────────── The vessel and its description ─────────────────────────────

  @B-016
  Scenario: A vessel is one level below the tracked item, and one place builds it
    Given vessels are live
     When a position report for "366999001" arrives
     Then the fleet holds a tracked item that is a vessel
      And it was built by the vessel strategy and by nothing else

  @B-017
  Scenario Outline: A vessel keeps a normalized name and labels itself from it
    Given vessels are live
      And "366999005" was last reported at 14:00:00 UTC
     When "366999005" is reported with ship name <name>
     Then its cached ship name is <normalized>
      And its vessel name is <normalized>
      And its label is <label>

    Examples:
      | name             | normalized   | label         |
      | "  BAY RUNNER  " | "BAY RUNNER" | "BAY RUNNER"  |
      | absent           | absent       | "366999005"   |
      | "   "            | absent       | "366999005"   |
      | "BAY RUNNER@@@@" | "BAY RUNNER" | "BAY RUNNER"  |
      | "@@@@@@@@"       | absent       | "366999005"   |

  @B-018
  Scenario: Speed and course are held as the feed reports them
    Given vessels are live
     When a position report for "366999001" reports 12.4 knots over ground, course 87.5 and heading 90
     Then the vessel holds 12.4 knots, a course of 87.5 degrees and a heading of 90 degrees

  @B-019
  Scenario: The vessel description offers ship type and flag as groupings
    Given vessels are live
     When the vessel description is read
     Then it names columns and detail lines including MMSI and true heading
      And it offers Moving, Named vessel and Reporting a position as filter choices
      And its grouping keys are ship type and flag state
      And its comparers order by label, last contact and group key
      And its card's title is the label, its subtitle the ship type, and its readouts speed and course over ground
      And its trail measure is speed over ground in knots, from 0 to 30
      And every one of them reads a tracked item rather than naming the vessel type

  @B-019
  Scenario Outline: Each vessel filter admits only reports with its required value
    Given vessels are live
     When a vessel has ship name <name>, speed over ground <speed> and position <position>
     Then the matching vessel filters are <filters>

    Examples:
      | name         | speed      | position | filters                         |
      | absent       | absent     | absent   | none                            |
      | "BAY RUNNER" | 0 knots    | absent   | Named vessel                    |
      | absent       | 12.4 knots | absent   | Moving                          |
      | absent       | 0 knots    | present  | Reporting a position            |
      | "@@@@"       | 12.4 knots | present  | Moving and Reporting a position |

  @B-020
  Scenario Outline: Grouping falls back to Unknown
    Given vessels are live
     When "<mmsi>" is held <static>
     Then it groups under ship type "<type>" and flag state "<flag>"

    Examples:
      | mmsi      | static                                 | type    | flag          |
      | 366999001 | with no static data received           | Unknown | United States of America |
      | 366999003 | with static data reporting type 70     | Cargo   | United States of America |
      | 366999007 | with static data reporting type 15     | Unknown | United States of America |
      | 306999001 | with no static data received           | Unknown | Netherlands (Kingdom of the) - Bonaire, Sint Eustatius and Saba; Netherlands (Kingdom of the) - Curaçao; Netherlands (Kingdom of the) - Sint Maarten (Dutch part) |
      | 999999001 | with no static data received           | Unknown | Unknown                  |

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
    Given no vessel silence threshold is configured and the aircraft staleness threshold is 5 minutes
     When aircraft are live and "a1b2c3" has been silent for an hour of observed time
     Then "a1b2c3" is marked stale and is still in the fleet
     When vessels are live and "366999001" has been silent for 6 minutes of observed time
     Then "366999001" is still in the fleet and is not marked stale
     When "366999001" has been silent for 10 minutes and 1 second of observed time
     Then "366999001" is no longer in the fleet

  @B-023
  Scenario: The vessel silence threshold is configured, not compiled in
    Given "AisStream:SilentAfter" is configured as 20 minutes
      And vessels are live and "366999001" last reported at 14:00:00
     When the observed clock reaches 14:15:00
     Then "366999001" is still in the fleet
     When the observed clock passes 14:20:00
     Then "366999001" is no longer in the fleet

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
      And Refresh remains visible but cannot be invoked

  @B-026
  Scenario: Swapping changes the vehicles and the description, and nothing else
    Given aircraft are live and grouped by origin country
     When the presenter swaps to vessels and back to aircraft
     Then the live description changed on each swap
      And the fleet held vessels while vessels were live and aircraft afterwards
      And the tracker's pipeline was neither rebuilt nor edited

  @B-027
  Scenario: The vessel description requests removal on silence
    Given the vessel silence threshold is 10 minutes
      And vessels are live
     When the vessel description is read
     Then its silence treatment is removal after the vessel silence threshold
