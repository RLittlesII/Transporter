@aircraft-source
Feature: Aircraft source — contract, client, cache, strategy, decorator, tracker

  As a line-of-business .NET developer whose data lives behind REST endpoints
  I want a typed contract that mirrors the provider, a client that caches what it fetches, a
    per-type projection that is the only place a domain vehicle is built, and a swap that happens
    behind a decorator
  So that I can point at the exact boundary where a request/response feed becomes a reactive
    collection, and know exactly where to add a second source in an application of my own

  # Scenarios are documentation. There is no Gherkin runner in this repository;
  # the xUnit tests in test/UnitTests execute, each citing the claim it proves.
  # Every value below is synthetic: invented callsigns, icao24 values and positions.

  # ─────────────────────────────── Happy path ───────────────────────────────

  @B-001 @B-002 @B-003
  Scenario: The response envelope mirrors what the provider sends
    Given a response reporting a time of 14:32:10 and three aircraft rows
     When it is read through the contract
     Then the envelope carries the reported time and the rows, named as the provider names them
      And the rows are still the provider's positional shape
      And no member of the envelope is a named per-aircraft type
      And 14:32:10 is the observed instant for everything downstream

  @B-005 @B-008
  Scenario: The contract offers one method per endpoint and is reached only through itself
    Given the states endpoint is the only endpoint this feature consumes
     When the contract is inspected
     Then it declares exactly one method for that endpoint
      And that method returns a task and takes a cancellation token last
      And resolving the contract yields the implementation its own chain was built with
      And no implementing type can be resolved or named from outside the integration code
      And no consumer is offered a choice between implementations

  @B-016 @B-019 @B-020
  Scenario: A complete row becomes one snapshot with every field read by index
    Given a response containing one 18-element row
      And that row reports icao24 "a1b2c3", callsign "FLT0421  ", and origin country "Pacifica"
      And it reports longitude -121.84, latitude 37.35, and barometric altitude 9144.0
      And it reports on ground false, velocity 231.5, true track 287.4, vertical rate -2.6
      And it reports geometric altitude 9280.0, squawk "0021", spi false, position source 0
      And it reports category 3
     When the client reads the response
     Then one snapshot is produced
      And its callsign is "FLT0421" with no trailing padding
      And its squawk is the four-character string "0021"
      And every other member holds the value at its own index in the response shape table

  @B-011
  Scenario: Two identical snapshots compare equal, so no change is emitted
    Given a snapshot for icao24 "a1b2c3"
      And a second snapshot built from an identical row
     When the two are compared
     Then they are equal
      And writing the second to a cache already holding the first emits no change

  @B-012
  Scenario: A snapshot is identified by its icao24
    Given two snapshots reporting icao24 "a1b2c3" and "d4e5f6"
     When they are written to the cache
     Then the cache holds two items
      And each is addressable by its icao24
      And neither has an absent identifier

  @B-015 @B-023
  Scenario: The client is handed its contract and its cache, and writes what it fetches
    Given a client constructed with a contract and a cache
      And the cache already holds snapshots for "a1b2c3", "d4e5f6" and "a7b8c9"
     When a fetch returns a set in which
       | icao24 | difference                  |
       | a1b2c3 | unchanged                   |
       | d4e5f6 | barometric altitude 10668.0 |
       | a7b8c9 | absent                      |
       | b1c2d3 | newly present               |
     Then the client wrote the whole set as one differential update
      And its snapshot stream emits exactly three changes
      And they are one add for "b1c2d3", one update for "d4e5f6", and one remove for "a7b8c9"
      And no change is emitted for "a1b2c3"
      And the client constructed neither the contract nor the cache

  @B-030 @B-032
  Scenario: The cache is a plain store and names no domain type
    Given a cache holding four snapshots
     When its contents and its stream are inspected
     Then both carry snapshots
      And the cache has constructed no domain vehicle and holds none
      And the cache has applied no diff policy of its own, no projection, and read no clock

  @B-031
  Scenario: Each client has its own cache, and the cache outlives the client
    Given an aircraft client and a second client of a different source
     When each is constructed
     Then each has its own cache, typed to its own snapshot
      And neither cache is reachable from the other client
      And stopping a client leaves its cache intact

  @B-033 @B-034
  Scenario: The aircraft strategy projects a snapshot into an aircraft
    Given a snapshot stream emitting an add for icao24 "a1b2c3"
     When the aircraft strategy projects it
     Then one add of a domain vehicle is emitted, keyed "a1b2c3"
      And this is the first point at which a domain vehicle exists
      And the strategy is reachable through the tracker-source seam without a cast

  @B-035
  Scenario: Reported units survive the projection
    Given a snapshot reporting velocity 231.5 metres per second
     When the strategy projects it
     Then the vehicle reports 231.5 metres per second
      And no value was converted to feet, knots or a local time

  @B-038 @B-039
  Scenario: The decorator picks the live strategy and nothing downstream can tell
    Given an aircraft strategy and a vessel strategy, both registered
     When a consumer subscribes through the decorator
     Then it receives the live strategy's vehicles
      And it asked nothing which strategy to use
      And after a swap it receives the other strategy's vehicles on the same subscription
      And nothing in the stream reveals that a swap occurred

  @B-041
  Scenario: View models depend on the fleet tracker and nothing upstream of it
    Given a view model that shows the fleet
     When its dependencies are identified
     Then it depends on the fleet tracker
      And it depends on no strategy, no client, no cache, and not the decorator

  @B-042
  Scenario: The pipeline is built once and survives a swap
    Given a running application whose pipeline has been constructed
     When the live source is swapped
     Then the filters, comparers, groups, aggregates and bindings are the same instances
      And none of them was reconstructed
      And the fleet tracker is what owns them

  @B-024
  Scenario: The bounding box and the polling interval are given to the client
    Given a bounding box and a fifteen-second interval
     When the client is constructed with them
     Then the client fetches that box on that interval
      And neither value appears on the contract, on the client's stream, or on anything downstream

  @B-050
  Scenario: The interval defaults to fifteen seconds and the box has no default
    Given configuration that sets neither the interval nor the bounding box
     When the client is constructed
     Then its interval is fifteen seconds
      And construction fails for the absent bounding box rather than choosing one
      And configuration supplying either value overrides the default

  @B-025
  Scenario: Offering category grouping makes the request ask for extended rows
    Given the application offers grouping by aircraft category
     When a fetch is made
     Then the request asks for extended rows
      And the resulting snapshots carry a category

  @B-027
  Scenario: Every poll records the remaining credit at debug level
    Given a response reporting 3,412 credits remaining
     When the poll completes
     Then the remaining credit is written to the log at debug level
      And the log line contains no token, client id or client secret

  # ────────────────────────────── Failure mode ──────────────────────────────

  # The interval below is deliberately shorter than the retry-after it is tested
  # against, and shorter than the fifteen-second default. With an interval of
  # fifteen and a wait of twelve, waiting for the next tick and honouring the
  # header are indistinguishable — the assertion would pass either way. Do not
  # "correct" these two to the default.

  @B-028
  Scenario: A throttled poll waits exactly as long as the provider asked
    Given a client polling on a one-second interval
     When a poll is throttled and the response asks for a 12-second wait
     Then no exception reaches the subscriber
      And nothing is written to the cache for that poll
      And the next request is not made until 12 seconds have elapsed on the injected scheduler
      And the wait is 12 seconds and not the one-second interval the client would otherwise use

  @B-028
  Scenario: A server error stays an exception rather than becoming a deferred poll
    Given a client polling on a one-second interval
     When a poll returns a 500 response
     Then the failure surfaces as an exception rather than as a deferred poll
      And no wait is derived from a retry-after value, because the response carries none

  @B-026
  Scenario: An expired token is refreshed before the next poll is sent
    Given a token that expired one minute ago
     When the next poll is due
     Then the token is refreshed first
      And the poll carries the new token

  @B-026
  Scenario: An unauthorized response refreshes the token and retries the request once
    Given a token the client believes is still valid
     When a poll is rejected as unauthorized
     Then the token is refreshed
      And that same request is retried exactly once
      And a second rejection is not retried again

  @B-027
  Scenario: A token refresh is recorded without recording the token
    Given a token refresh that succeeds
     When the refresh is logged
     Then the log records that a refresh happened
      And it records neither the token nor the credentials that obtained it

  @B-029
  Scenario: A missing credential stops the application at startup and names which one
    Given no OpenSky client secret has been configured
     When the application starts
     Then startup fails
      And the failure message names the client secret as the absent credential
      And no poll was attempted
      And the message does not contain the value of any credential that was configured

  @B-029
  Scenario: A timed-out poll does not end the client's stream
    Given a subscriber listening to a client
     When one poll times out
      And the following poll succeeds
     Then the subscriber receives the changes from the following poll
      And the subscription neither completed nor errored in between

  @B-022
  Scenario: One unreadable row does not cost the poll its other rows
    Given a response of four rows in which one row has nine elements
     When the client reads it
     Then three snapshots are produced
      And one row is counted as unreadable
      And the three readable rows are unaffected

  @B-043 @B-051
  Scenario: An aircraft that stops reporting goes stale and stays in the collection
    Given a staleness threshold of five minutes
      And a vehicle whose last contact was 14:32:10
      And an injected clock reading 14:32:40
     When the clock is advanced to 14:37:11 and no new snapshot arrives
     Then the vehicle is stale
      And it is still in the collection
      And it was not removed for being stale
      And the staleness was determined without reading the machine's current time
      And the fleet tracker is what made the decision

  @B-051
  Scenario: The staleness threshold is configuration, not a constant
    Given configuration that sets no staleness threshold
     When the fleet tracker is constructed
     Then the threshold is five minutes
      And configuration supplying a different threshold overrides it
      And a vehicle under the threshold is not stale

  @B-040
  Scenario: Swapping stops the outgoing source
    Given an aircraft source polling on a fifteen-second interval
     When the live source is swapped to vessels
     Then the aircraft source stops polling
      And it spends no further credits
      And its in-flight result does not reach any cache after the swap

  @B-010
  Scenario: The contract's fake refuses to invent a response
    Given a hand-written fake of the contract with no response configured
     When a caller invokes its endpoint
     Then it throws
      And the message names the response that was not set
      And the fake was not produced by a mocking framework

  # ─────────────────────────── Validation failure ───────────────────────────

  @B-004 @B-045
  Scenario: The positional row is confined to the integration code
    Given the envelope and the positional row type
     When every reference to them is identified
     Then they are the class implementing the contract and the client
      And no domain type, cache, strategy, tracker or view can name either

  @B-006
  Scenario: The contract names nothing but its endpoint and its arguments
    Given the contract
     When its members and their types are inspected
     Then it names no observable, no cache, no changeset
      And it names no bounding box, no polling interval, and no credential

  @B-007
  Scenario: One internal sealed class implements the contract per transport, with nothing public on it
    Given the production code
      And the provider is reached over HTTP and, on replay, from a recording
     When every implementor of the contract is identified
     Then exactly one is found for each of those two transports
      And neither transport has a second implementation
      And each is internal and sealed
      And every one of their contract methods is implemented explicitly
      And neither exposes a public method for any endpoint

  @B-049
  Scenario: A push provider gets no contract layer, and none is invented for it
    Given a provider reached by subscribing to a socket rather than by requesting
     When its strategy is built
     Then it has a client, a cache and a projection
      And it has no contract layer, because there is no request to return a response
      And no contract was invented to make it resemble the polled strategy
      And the only surface it shares with the polled strategy is the tracker-source seam

  @B-048
  Scenario: The contract carries no version the provider never published
    Given OpenSky publishes no API version and there is no version in its path
     When the contract is named
     Then it carries no version suffix
      And there is no marker interface above it
      And nothing in the code names a version OpenSky did not declare

  @B-009 @B-048
  Scenario: The provider publishing a version is what introduces the versioned layer
    Given a contract with no version suffix that an implementing class satisfies
     When OpenSky publishes an API version for the first time
     Then a versioned contract interface is added for it
      And the existing contract interface is left unedited
      And from then on a further provider version adds a further interface

  @B-013
  Scenario: A snapshot carries the reported values and interprets none of them
    Given a row reporting velocity 231.5 and barometric altitude 9144.0
     When the client reads it
     Then the snapshot reports 231.5 and 9144.0 as given
      And it has converted no unit, derived no value, and interpreted no field

  @B-014
  Scenario: A snapshot carries no staleness flag, label or grouping key
    Given a snapshot built from a complete row
     When its members are inspected
     Then none of them is a staleness flag, a display label, or a grouping key
      And every member it carries was reported by the server

  @B-017
  Scenario: A null element is read as absent rather than as a zero
    Given a row whose velocity, true track and vertical rate are all null
     When the client reads it
     Then the snapshot's velocity, true track and vertical rate are all absent
      And none of them is 0

  @B-021
  Scenario: The sensors field is read past and never reaches a vehicle
    Given a row whose sensors element carries a value
     When the row is read and projected
     Then neither the snapshot nor the resulting vehicle carries a sensors value
      And the exclusion is stated in the client rather than implied by disuse

  @B-036
  Scenario: An aircraft with no position fix is not placed in the Gulf of Guinea
    Given a snapshot whose longitude and latitude are both absent
      And whose barometric altitude is absent and whose geometric altitude is 9280.0
     When the strategy projects it
     Then the vehicle has no position
      And it does not have a position of latitude 0, longitude 0
      And it has no barometric altitude, and not an altitude of 0 metres
      And its geometric altitude is present, so the two altitudes did not collapse into one

  @B-037
  Scenario: The vehicle's key is its icao24, and the per-type seam stays narrow
    Given a snapshot reporting icao24 "A1B2C3"
     When the strategy projects it
     Then the vehicle's key is "a1b2c3" in lowercase hex
      And the key was set when the vehicle was constructed
      And the per-type tracker-source interface declares no display name, column list or budget

  @B-044
  Scenario: Nothing edits a bound collection by hand
    Given a running application
     When every write to a bound collection is identified
     Then all of them arrive through the pipeline the fleet tracker owns
      And no code adds to or removes from a bound collection imperatively

  @B-046
  Scenario: A snapshot does not survive past the projection
    Given the snapshot type
     When every reference to it is identified
     Then they are the client, its cache, and the strategy's projection
      And nothing downstream of that projection names it

  @B-047
  Scenario: A consumer of the fleet tracker names nothing upstream of it
    Given a consumer of the fleet tracker
     When its references are identified
     Then it names no contract, no client, no cache, no snapshot
      And it names no concrete tracker source

  # ────────────────────────────── Data-driven ───────────────────────────────

  @B-018
  Scenario Outline: An absent category and an unknown category are two different answers
    Given a row of <elements> elements whose index 17 is <index_17>
     When the client reads it
     Then the snapshot's category is <outcome>

    Examples:
      | elements | index_17 | outcome              |
      | 17       | n/a      | absent               |
      | 18       | null     | absent               |
      | 18       | 0        | present with value 0 |
      | 18       | 3        | present with value 3 |

  @B-019
  Scenario Outline: The wire's callsign padding is removed, and padding alone is no callsign
    Given a row whose callsign element is <wire_value>
     When the client reads it
     Then the snapshot's callsign is <callsign>

    Examples:
      | wire_value | callsign  |
      | "FLT0421"  | "FLT0421" |
      | "FLT42   " | "FLT42"   |
      | "        " | absent    |
      | null       | absent    |

  @B-020
  Scenario Outline: A squawk keeps its leading zeros because it is a code, not a number
    Given a row whose squawk element is <wire_value>
     When the client reads it
     Then the snapshot's squawk is <squawk>

    Examples:
      | wire_value | squawk |
      | "0021"     | "0021" |
      | "7700"     | "7700" |
      | "0000"     | "0000" |
      | null       | absent |

  @B-022
  Scenario Outline: An unreadable row is excluded and counted, and the poll survives
    Given a response of four rows in which one row is <defect>
     When the client reads it
     Then three snapshots are produced
      And one row is counted as unreadable

    Examples:
      | defect                                  |
      | an array of 12 elements                 |
      | an array of 25 elements                 |
      | a row whose index 0 is null             |
      | a row whose index 8 is the string "yes" |

  @B-035
  Scenario Outline: SI units reach the domain vehicle unconverted
    Given a snapshot reporting <field> as <value>
     When the strategy projects it
     Then the vehicle's corresponding value is <stored> <unit>

    Examples:
      | field               | value  | stored | unit               |
      | barometric altitude | 9144.0 | 9144.0 | metres             |
      | geometric altitude  | 9280.0 | 9280.0 | metres             |
      | velocity            | 231.5  | 231.5  | metres per second  |
      | vertical rate       | -2.6   | -2.6   | metres per second  |
      | true track          | 287.4  | 287.4  | degrees from north |
