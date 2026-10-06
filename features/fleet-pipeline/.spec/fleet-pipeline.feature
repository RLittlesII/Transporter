@fleet-pipeline
Feature: Fleet pipeline — filter, sort, group, count, mark and bind

  As a line-of-business .NET developer whose data lives behind REST endpoints
  I want one collection of tracked vehicles, built once, that filters and sorts from inputs the
    user changes, groups and counts itself, and marks a vehicle that has gone silent instead of
    dropping it
  So that I can see that nothing after the snapshot-to-changeset step is special, and copy the
    whole pipeline into an application whose data arrives the same way mine does

  # Scenarios are documentation. There is no Gherkin runner in this repository;
  # the xUnit tests in test/UnitTests execute, each citing the claim it proves,
  # and the layer rules are reported by the analyzer instead.
  # Every value below is synthetic: invented callsigns, icao24 values and countries.

  # ─────────────────────────────── Happy path ───────────────────────────────

  @B-001
  Scenario: The pipeline is built once and survives a change of live source
    Given a fleet tracker subscribed to the tracker seam
      And a consumer bound to its fleet stream, holding three vehicles
     When the live source behind the seam is swapped for another
     Then the pipeline is not rebuilt
      And the collection the consumer bound is not replaced
      And no stage re-subscribes to the seam

  @B-002
  Scenario: The pipeline publishes streams, and one consumer materialises one collection
    Given a fleet tracker reporting four vehicles in two countries
     When a consumer binds the fleet stream and the groups stream
     Then the tracker exposes no collection of its own
      And each bound row carries the vehicle and its stale mark
      And the groups carry the same vehicle instances the fleet does
      And no second store of tracked items exists anywhere downstream of the seam

  @B-006
  Scenario: A new predicate re-filters what is already there
    Given a bound fleet of five aircraft, two of them on the ground
      And every row is visible
     When the tracker is asked to filter to airborne aircraft only
     Then three rows are visible
      And the two filtered out are still in the source
      And the seam was not re-subscribed and nothing was refetched

  @B-007
  Scenario: Before anyone sets a predicate, everything is visible
    Given a fleet tracker nobody has asked to filter
     When six vehicles are reported
     Then six rows are visible
      And the tracker supplied that default itself

  @B-009
  Scenario: A new comparer reorders the rows in place
    Given a bound fleet of aircraft sorted by callsign
     When the tracker is asked to sort by origin country
     Then the rows are in country order
      And each row is the same instance it was before
      And nothing was re-fetched, no stage was rebuilt and the seam is connected once

  @B-011
  Scenario Outline: Ties break on the key, so two sorts agree
    Given two aircraft that compare equal under the <column> comparer
     When the fleet is sorted, re-sorted, and sorted a third time
     Then every sort orders the two by their key
      And the three orderings are identical

    Examples:
      | column            |
      | origin country    |
      | on ground         |
      | barometric altitude |

  @B-028
  Scenario: Two subscribers, one connection to the seam
    Given a fleet tracker with no subscribers
     When one consumer binds the fleet stream and a second subscribes to it
     Then the seam was connected once
      And the diff, the filter and the sort each ran once per changeset
      And both subscribers saw the same changes
      And the stages tear down when the last subscriber unsubscribes

  @B-012
  Scenario: Changing the grouping reforms the groups
    Given a bound fleet of six aircraft grouped by origin country
      And there are three groups
     When the tracker is asked to group by category
     Then the groups reform under the new key
      And no pipeline stage is rebuilt
      And the collection's rows are unchanged

  @B-013
  Scenario: An aircraft answers the grouping key with its origin country
    Given an aircraft reporting origin country "Pacifica"
     When its grouping key is read
     Then it is "Pacifica"
      And a source whose vehicle answers no grouping key does not compile

  @B-014
  Scenario: A group counts its vehicles and its silent ones
    Given a group holding two aircraft, one of which last reported six minutes ago
      And the threshold is five minutes
     When the group's counts are read
     Then it reports two tracked and one stale
      And neither count was produced by enumerating the bound collection

  @B-015
  Scenario: The summary derives from the same stream as the collection
    Given a fleet tracker with an observed summary
     When one aircraft is added, one updated and one removed
     Then the summary reports the new counts of vehicles tracked, vehicles stale and groups present
      And it changed once per change rather than on a timer

  @B-020
  Scenario: The source description carries the columns and the groupings
    Given the aircraft source's description
     When its columns are read
     Then each carries a display name and a selector over the abstract vehicle
      And a sortable column carries a comparer and an unsortable one carries none
      And its groupings name what the aircraft feed can be grouped by

  # ────────────────────────────── Failure mode ──────────────────────────────

  @B-004
  Scenario: Disposing the tracker completes every stream it publishes
    Given a fleet tracker with a consumer bound to its fleet, groups and summary streams
     When the tracker is disposed
     Then every stream it published completes
      And the consumer's bound collection stops receiving changes
      And the seam has no remaining subscriber
      And disposing it a second time does nothing

  @B-004
  Scenario: An idle tracker holds nothing
    Given a fleet tracker nobody has subscribed to
     When its fields are inspected
     Then it holds no subscription of its own
      And the seam has not been connected

  @B-005
  Scenario: Every scheduler the pipeline uses is one it was given
    Given a fleet tracker built with one test scheduler in every scheduler position
      And a vehicle reported on the background thread
     When the scheduler is advanced
     Then every work item the pipeline scheduled ran on the scheduler it was given
      And no stage read an ambient scheduler to get there
      And no stage marshalled to a user interface thread for its consumer

  @B-016
  Scenario: A silent vehicle is marked and kept
    Given an aircraft that last reported at 14:30:00
      And the threshold is five minutes
     When the observed instant is 14:36:00
     Then the aircraft is marked stale
      And it is still in the collection
      And it is still addressable by its key

  @B-017
  Scenario: The threshold defaults to five minutes
    Given a fleet tracker nobody has given a threshold
      And an aircraft that last reported four minutes before the observed instant
     When the fleet is read
     Then the aircraft is not stale
      And an aircraft silent for six minutes is

  @B-018
  Scenario: Time passing is enough to make a vehicle stale
    Given an aircraft that last reported at 14:30:00
      And the threshold is five minutes
      And no further data arrives for it
     When the observed instant advances from 14:31:00 to 14:36:00
     Then the aircraft was not stale at 14:31:00 and is at 14:36:00
      And the mark changed with no changeset arriving from the seam
      And nothing in the pipeline read the wall clock to decide it

  @B-019
  Scenario: Silence never removes a vehicle
    Given an aircraft that last reported an hour before the observed instant
     When the fleet is read
     Then the aircraft is present and marked stale
      And no remove was emitted for it
      And the pipeline contains no expiry stage

  # ─────────────────────────── Validation failure ───────────────────────────

  @B-003
  Scenario: The bound collection cannot be edited by hand
    Given code that adds a vehicle to the bound fleet collection directly
     When the project is built
     Then the imperative edit is reported at the call
      And the same is reported for a remove, a clear and a reorder

  @B-008
  Scenario: A filter re-evaluated from an event handler is reported
    Given a handler that re-applies a filter or a sort when the user types
     When the project is built
     Then it is reported at the handler
      And the diagnostic names the observable input that should have carried it

  @B-010
  Scenario: Every comparer reads only what the abstract vehicle carries
    Given every comparer the description supplies
     When each is applied to two vehicles
     Then each reads members of the abstract vehicle only
      And a comparer that reaches a concrete aircraft's member is reported at the expression

  @B-021
  Scenario: Swapping the source swaps the description and edits nothing
    Given a bound fleet described by the aircraft source
     When a second source's description arrives
     Then the available columns and groupings change
      And no comparer, predicate or grouping key was edited to make it work
      And no pipeline stage was rebuilt

  @B-022
  Scenario: Nothing in the pipeline asks which kind of vehicle it has
    Given a cast, an "is" check or a switch on a concrete vehicle type in a column, comparer, predicate, grouping key or aggregate
     When the project is built
     Then it is reported at the expression
      And the detail pane remains the one surface where it is allowed

  @B-023
  Scenario: Nothing downstream of the seam can name the provider's layers
    Given code under the tracking layer referencing an API contract, a client, a cache or a snapshot
     When the project is built
     Then the reference is reported at the line that writes it

  @B-024
  Scenario: Nothing here reaches a network, a file or the wall clock
    Given this feature's test suite
     When its arrangements are inspected
     Then no test constructs an HTTP type, opens a file or reads the wall clock
      And time advances because a test advanced it

  # ──────────────────────────── The arrival notice ────────────────────────────

  @B-025
  Scenario: A changeset that changed something raises one notice
    Given a bound fleet of five aircraft
      And the observed instant is 14:32:10
     When a changeset arrives adding one aircraft, updating two and removing one
     Then one notice is raised
      And it reports the instant 14:32:10
      And it reports five tracked, one added, two updated and one removed

  @B-025
  Scenario: A poll where nothing moved raises nothing
    Given a bound fleet of five aircraft
     When a changeset arrives carrying no change
     Then no notice is raised
      And the silence means nothing moved rather than that the feed stopped

  @B-026
  Scenario Outline: Two consumers pace the same notices differently
    Given two subscribers to the notices, one at <first> and one at <second>
     When notices are raised faster than either interval
     Then each subscriber receives at most one notice per its own interval
      And each receives the most recent notice in its window
      And changing an interval while running takes effect without rebuilding the pipeline

    Examples:
      | first       | second     |
      | one second  | one minute |
      | one minute  | one second |
      | one second  | one second |

  @B-027
  Scenario: Silence is reported once, and the resumption is reported too
    Given a bound fleet and a staleness threshold of five minutes
      And the last changeset arrived at 14:30:00
     When the observed instant advances to 14:36:00 with no changeset arriving
     Then one quiet notice is raised
      And advancing further raises no second quiet notice
      And the next changeset that changes something raises a resumed notice
