@aircraft-source
Feature: Aircraft source — API types, snapshots, the cache, and the fleet tracker

  As a line-of-business .NET developer whose data lives behind REST endpoints
  I want polled aircraft rows to become named snapshots, those snapshots to be stored and diffed
    by a cache that knows nothing else, and a tracker to project that cache into domain vehicles
  So that I can point at the exact boundary where a request/response feed becomes a reactive
    collection, and copy that boundary into an application of my own

  # Scenarios are documentation. There is no Gherkin runner in this repository;
  # the xUnit tests in test/UnitTests execute, each citing the claim it proves.
  # Every value below is synthetic: invented callsigns, icao24 values and positions.

  # ─────────────────────────────── Happy path ───────────────────────────────

  @B-001 @B-004 @B-005
  Scenario: A complete row becomes one snapshot with every field read by index
    Given a states response containing one 18-element row
      And that row reports icao24 "a1b2c3", callsign "FLT0421  ", and origin country "Pacifica"
      And it reports longitude -121.84, latitude 37.35, and barometric altitude 9144.0
      And it reports on ground false, velocity 231.5, true track 287.4, vertical rate -2.6
      And it reports geometric altitude 9280.0, squawk "0021", spi false, position source 0
      And it reports category 3
     When the response is read
     Then one snapshot is produced
      And its callsign is "FLT0421" with no trailing padding
      And its squawk is the four-character string "0021"
      And every other member holds the value at its own index in the response shape table

  @B-009
  Scenario: Two identical snapshots compare equal, so no change is emitted
    Given a snapshot for icao24 "a1b2c3" built from a row
      And a second snapshot built from an identical row
     When the two are compared
     Then they are equal
      And applying the second to a cache already holding the first emits no change

  @B-010
  Scenario: A snapshot is identified by its icao24
    Given two snapshots reporting icao24 "a1b2c3" and "d4e5f6"
     When they are placed in the cache
     Then the cache holds two items
      And each is addressable by its icao24
      And neither has an absent or null identifier

  @B-013 @B-014 @B-015
  Scenario: A poll emits one set carrying the records and the instant observed
    Given a source whose next poll will return three aircraft
      And the source observes that poll at 14:32:10
     When a subscriber is listening to the source
     Then exactly one set is emitted
      And that set contains three snapshots
      And that set's observed instant is 14:32:10
      And the subscriber reads both the snapshots and the instant from the set alone

  @B-017
  Scenario: A source that makes no network call satisfies the seam with nothing stubbed
    Given a source that returns two snapshots from an in-memory list
     When it is used wherever a source is expected
     Then it satisfies the seam
      And it supplies no bounding box, interval, credential, credit or HTTP status
      And nothing had to be faked for it to compile or run

  @B-018
  Scenario: The bounding box and the polling interval are given to the source
    Given a bounding box and a ten-second interval
     When the OpenSky source is constructed with them
     Then the source polls that box on that interval
      And neither value appears on the seam, on an emitted set, or on anything the cache reads

  @B-019
  Scenario: Offering category grouping makes the request ask for extended rows
    Given the application offers grouping by aircraft category
     When a poll is made
     Then the request asks for extended rows
      And the resulting snapshots carry a category

  @B-021
  Scenario: Every poll records the remaining credit at debug level
    Given a poll whose response reports 3,412 credits remaining
     When the poll completes
     Then the remaining credit is written to the log at debug level
      And the log line contains no token, client id or client secret

  @B-025
  Scenario: Two successive sets become exactly the changes between them
    Given a cache that has been given a set containing "a1b2c3", "d4e5f6" and "a7b8c9"
     When it is given a second set in which
       | icao24 | difference                  |
       | a1b2c3 | unchanged                   |
       | d4e5f6 | barometric altitude 10668.0 |
       | a7b8c9 | absent                      |
       | b1c2d3 | newly present               |
     Then the cache's changeset stream emits exactly three changes
      And they are one add for "b1c2d3", one update for "d4e5f6", and one remove for "a7b8c9"
      And no change is emitted for "a1b2c3"

  @B-026 @B-029
  Scenario: The cache holds snapshots, names no domain type, and reads no clock
    Given a cache holding four snapshots
     When its contents and its changeset stream are inspected
     Then both carry snapshots and not domain vehicles
      And the cache has constructed no domain vehicle
      And the cache has read no clock, derived no staleness, and expired nothing

  @B-027
  Scenario: A push source's set is diffed exactly as a polled source's set is
    Given a cache holding two snapshots
     When a push source emits a set adding one aircraft and dropping one of the two
     Then the cache's changeset stream emits one add and one remove
      And those changes are indistinguishable in shape from a polled source's changes
      And the cache made no decision based on how the source obtained the set

  @B-030 @B-032
  Scenario: The tracker projects a snapshot into an aircraft, and absent values stay absent
    Given a cache emitting an add for a snapshot of icao24 "a1b2c3"
      And that snapshot reports barometric altitude 9144.0 and no geometric altitude
     When the tracker projects the cache's stream
     Then one add of a domain vehicle is emitted, keyed "a1b2c3"
      And its barometric altitude is present and its geometric altitude is absent
      And this is the first point at which a domain vehicle exists

  # ────────────────────────────── Failure mode ──────────────────────────────

  @B-022
  Scenario: A throttled poll waits exactly as long as the provider asked
    Given a source polling on a one-second interval
     When a poll is throttled and the response asks for a 12-second wait
     Then no exception reaches the subscriber
      And no set is emitted for that poll
      And the next request is not made until 12 seconds have elapsed on the injected scheduler
      And the wait is 12 seconds and not a value the source chose for itself

  @B-022
  Scenario: A server error stays an exception rather than becoming a deferred poll
    Given a source polling on a one-second interval
     When a poll returns a 500 response
     Then the failure surfaces as an exception rather than as a deferred poll
      And no wait is derived from a retry-after value, because the response carries none

  @B-020
  Scenario: An expired token is refreshed before the next poll is sent
    Given a token that expired one minute ago
     When the next poll is due
     Then the token is refreshed first
      And the poll carries the new token

  @B-020
  Scenario: An unauthorized response refreshes the token and retries the request once
    Given a token the source believes is still valid
     When a poll is rejected as unauthorized
     Then the token is refreshed
      And that same request is retried exactly once
      And a second rejection is not retried again

  @B-021
  Scenario: A token refresh is recorded without recording the token
    Given a token refresh that succeeds
     When the refresh is logged
     Then the log records that a refresh happened
      And it records neither the token nor the credentials that obtained it

  @B-024
  Scenario: A timed-out poll does not end the stream
    Given a subscriber listening to a source
     When one poll times out
      And the following poll succeeds
     Then the subscriber receives the following poll's set
      And the subscription neither completed nor errored in between

  @B-007
  Scenario: One unreadable row does not cost the poll its other rows
    Given a states response of four rows in which one row has nine elements
     When the response is read
     Then three snapshots are produced
      And one row is counted as unreadable
      And the three readable rows are unaffected

  @B-016
  Scenario: A poll that finds no aircraft empties the cache rather than being ignored
    Given a cache holding three snapshots
     When the next set arrives carrying no snapshots at all
     Then the set is treated as a valid emission
      And the cache's changeset stream emits three removes
      And the empty set is distinguishable from the source not having emitted

  @B-033
  Scenario: An aircraft that stops reporting goes stale on the injected clock
    Given a vehicle projected from a snapshot whose last contact was 14:32:10
      And an injected clock reading 14:32:40
     When the injected clock is advanced to 14:35:10 and no new set arrives
     Then the vehicle is stale
      And the staleness was determined without reading the machine's current time
      And the cache took no part in the decision

  # ─────────────────────────── Validation failure ───────────────────────────

  @B-023
  Scenario: A missing credential stops the application at startup and names which one
    Given no OpenSky client secret has been configured
     When the application starts
     Then startup fails
      And the failure message names the client secret as the absent credential
      And no poll was attempted
      And the message does not contain the value of any credential that was configured

  @B-002
  Scenario: A null element is read as absent rather than as a zero
    Given a states row whose velocity, true track and vertical rate are all null
     When the row is read
     Then the snapshot's velocity, true track and vertical rate are all absent
      And none of them is 0

  @B-006
  Scenario: The sensors field is read past and never reaches a vehicle
    Given a states row whose sensors element carries a value
     When the row is read and projected
     Then neither the snapshot nor the resulting vehicle carries a sensors value
      And the exclusion is stated at the converter rather than implied by disuse

  @B-008
  Scenario: Only the converter reads a row by index, and it produces the snapshot directly
    Given the code that reads a states response
     When every reader of a positional element is identified
     Then the converter is the only one
      And no intermediate positional type exists between the response and the snapshot

  @B-011
  Scenario: A snapshot carries the reported values and interprets none of them
    Given a states row reporting velocity 231.5 and barometric altitude 9144.0
     When the row is read
     Then the snapshot reports 231.5 and 9144.0 as given
      And it has converted no unit, derived no value, and interpreted no field

  @B-012
  Scenario: A snapshot carries no staleness flag, label or grouping key
    Given a snapshot built from a complete row
     When its members are inspected
     Then none of them is a staleness flag, a display label, or a grouping key
      And every member it carries was reported by the server

  @B-028
  Scenario: There is one cache of snapshots and one stream out of it
    Given the running application
     When every collection of tracked snapshots is identified
     Then exactly one exists
      And exactly one changeset stream leaves it
      And no source, actor, tracker or view model holds a second

  @B-034
  Scenario: The response envelope and the positional row are referenced only by the converter
    Given the response envelope and the positional row type
     When every reference to them is identified
     Then the converter is the only one
      And no snapshot, source, cache, tracker, domain type or downstream consumer names either

  @B-035
  Scenario: A snapshot does not survive past the projection
    Given the snapshot type
     When every reference to it is identified
     Then they are the converter, the source, the cache, and the tracker's projection
      And nothing downstream of the tracker names it

  @B-036
  Scenario: A consumer of the tracker's stream names neither the source, the snapshot, nor the cache
    Given a consumer subscribing to the tracker's stream
     When its references are identified
     Then it names none of the source, the snapshot, or the cache
      And the tracker's stream is its only route to tracked state

  # ────────────────────────────── Data-driven ───────────────────────────────

  @B-003
  Scenario Outline: An absent category and an unknown category are two different answers
    Given a states row of <elements> elements whose index 17 is <index_17>
     When the row is read
     Then the snapshot's category is <outcome>

    Examples:
      | elements | index_17 | outcome              |
      | 17       | n/a      | absent               |
      | 18       | null     | absent               |
      | 18       | 0        | present with value 0 |
      | 18       | 3        | present with value 3 |

  @B-004
  Scenario Outline: The wire's callsign padding is removed, and padding alone is no callsign
    Given a states row whose callsign element is <wire_value>
     When the row is read
     Then the snapshot's callsign is <callsign>

    Examples:
      | wire_value | callsign  |
      | "FLT0421"  | "FLT0421" |
      | "FLT42   " | "FLT42"   |
      | "        " | absent    |
      | null       | absent    |

  @B-005
  Scenario Outline: A squawk keeps its leading zeros because it is a code, not a number
    Given a states row whose squawk element is <wire_value>
     When the row is read
     Then the snapshot's squawk is <squawk>

    Examples:
      | wire_value | squawk |
      | "0021"     | "0021" |
      | "7700"     | "7700" |
      | "0000"     | "0000" |
      | null       | absent |

  @B-007
  Scenario Outline: An unreadable row is excluded and counted, and the poll survives
    Given a states response of four rows in which one row is <defect>
     When the response is read
     Then three snapshots are produced
      And one row is counted as unreadable

    Examples:
      | defect                                  |
      | an array of 12 elements                 |
      | an array of 25 elements                 |
      | a row whose index 0 is null             |
      | a row whose index 8 is the string "yes" |

  @B-031
  Scenario Outline: Reported units survive the projection unconverted
    Given a snapshot reporting <field> as <value>
     When it is projected into a domain vehicle
     Then the vehicle's corresponding value is <stored> <unit>

    Examples:
      | field               | value  | stored | unit               |
      | barometric altitude | 9144.0 | 9144.0 | metres             |
      | geometric altitude  | 9280.0 | 9280.0 | metres             |
      | velocity            | 231.5  | 231.5  | metres per second  |
      | vertical rate       | -2.6   | -2.6   | metres per second  |
      | true track          | 287.4  | 287.4  | degrees from north |
