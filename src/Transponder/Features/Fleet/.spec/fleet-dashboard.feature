@fleet-dashboard
Feature: Fleet dashboard — one page, thin view models, and the only legitimate downcast

  As a line-of-business .NET developer whose data lives behind REST endpoints
  I want one page whose view model binds the tracker's stream into the one collection, takes its
    columns from the live source, and turns a keystroke into a predicate and a header tap into a
    comparer
  So that I can see the pipeline working, read the view model that drives it in one sitting, and
    copy both into an application of my own

  # Scenarios are documentation. There is no Gherkin runner in this repository;
  # the xUnit tests in test/UnitTests execute, the analyzer reports the layer
  # rules, and three claims are proven by a recorded review because this
  # repository runs no UI test runner (§ 8).
  # Every value below is synthetic: invented callsigns, icao24 values and countries.

  # ─────────────────────────────── Happy path ───────────────────────────────

  @B-005
  Scenario: The view model binds the tracker's stream into the one collection
    Given a fleet tracker publishing four vehicles on its fleet stream
     When the view model is constructed and the scheduler is advanced
     Then its fleet holds those four vehicles
      And the collection was produced by binding the stream on the injected user interface scheduler
      And it is the only collection of tracked items in the application
      And disposing the view model disposes that subscription and nothing of the tracker's

  @B-007
  Scenario: The columns come from the live source's description
    Given a description offering the columns callsign, origin country and altitude
     When the view model's columns are read
     Then there are three, in the description's order
      And each carries the description's display name and selector
      And no column was compiled into the markup

  @B-009
  Scenario: Typing and choosing a filter hand the tracker one predicate
    Given a dashboard bound to a fleet of six aircraft
     When the search text becomes "FLT04" and the on-ground filter is chosen
     Then the tracker was asked to filter by one composed predicate
      And the view model enumerated no collection and edited none

  @B-010
  Scenario Outline: Search matches case-insensitively and ignores surrounding space
    Given an aircraft whose label is "FLT0421" and whose origin country is "Germany"
      And a description whose columns show the callsign, the origin country and the last contact
     When the search text is <text>
     Then the aircraft is <outcome>

    Examples:
      | text        | outcome     |
      | "flt0421"   | matched     |
      | "  FLT04  " | matched     |
      | ""          | matched     |
      | "germany"   | matched     |
      | "QFA"       | not matched |

  @B-011
  Scenario: A filter and a search compose, and clearing one keeps the other
    Given the search text "FLT" and the on-ground filter chosen
      And four aircraft, two airborne and matching, one on the ground and matching, one neither
     When the fleet is read
     Then one aircraft is visible
      And clearing the filter leaves the search applied
      And clearing the search leaves the filter applied

  @B-012
  Scenario: A header tap sorts, a second reverses, and a grouping is published
    Given a description whose altitude column carries a comparer
     When the altitude header is chosen, chosen again, and the category grouping is chosen
     Then the first choice publishes the description's comparer
      And the second publishes its reverse
      And the grouping published is the description's category grouping

  @B-013
  Scenario: The detail pane is the one surface that knows it has an aircraft
    Given an aircraft reporting squawk "0021", category 3 and a geometric altitude
     When it is selected and the detail pane's rows are read
     Then the rows include fields only an aircraft reports
      And the grid, the filters, the comparers and the summary named no concrete type to produce them

  @B-015
  Scenario: The summary is projected, never recomputed
    Given a tracker reporting four tracked, one stale and two groups
     When the summary view model's properties are read
     Then they are four, one and two
      And no count was derived by counting the bound collection

  @B-016
  Scenario: The swap control tells an actor, behind a busy indicator
    Given a dashboard bound to a live aircraft source
     When the swap control is invoked
     Then the actor is told, not asked
      And the busy indicator is showing
      And it stops showing when the new fleet's first change arrives

  @B-028
  Scenario: The refresh control tells an actor a poll is wanted
    Given a dashboard bound to a live aircraft source
     When the refresh control is invoked
     Then the actor is told, not asked
      And the view model awaited nothing and named no client
      And the refresh indicator is showing
      And whether a poll is performed is the actor's to decide

  @B-028
  Scenario: The indicator clears on the poll that landed, or on the cap
    Given the refresh control has been invoked and the indicator is showing
     When the next observed instant arrives, for a poll that changed no row
     Then the indicator stops showing
      And a press the actor refused stops showing after three seconds instead
      And a second press replaces the window rather than queueing one

  # ────────────────────────────── Failure mode ──────────────────────────────

  @B-006
  Scenario: One vehicle changing updates one card
    Given cards showing twelve aircraft
     When one aircraft reports a new altitude
     Then that card updates in place
      And the other eleven cards are not re-created

  @B-008
  Scenario: A stale card is marked and keeps its place
    Given an aircraft that has been silent past the threshold
     When the fleet is read
     Then its card carries the stale mark, as an icon and the word "Stale" beside its colour
      And it is still present, in the order the comparer put it

  @B-014
  Scenario: A selection does not outlive its vehicle
    Given an aircraft selected and showing in the detail pane
     When that aircraft leaves the collection
     Then the selection is absent
      And the detail pane is empty
      And nothing is shown for a vehicle the grid does not have

  @B-017
  Scenario: A view model that blocks or holds a transport is reported
    Given a view model holding an HttpClient, a timer, a socket, a cache write, a blocking call or an untimed Ask
     When the project is built
     Then each is reported at the member that holds it
      And the diagnostic names what owns that concern instead

  # ─────────────────────────── Validation failure ───────────────────────────

  @B-001
  Scenario: The page carries every surface the dashboard promises
    Given the fleet page
     When it is reviewed
     Then it carries the grid, the search and filter controls, the grouping selection, the summary and the detail pane

  @B-002
  Scenario: No XAML and no template page remain
    Given the user interface project
     When it is reviewed
     Then no page is defined in XAML but the application shell
      And the template's MainPage, its code-behind and the demo view model are gone
      And nothing in startup registers any of them

  @B-003
  Scenario: A page takes its view model and resolves nothing
    Given the fleet page
     When it is constructed
     Then its view model arrived by constructor
      And it resolved nothing from a static or a service locator

  @B-004
  Scenario: Pages and view models are registered in one place
    Given the constructed application
     When each page and view model is resolved
     Then every one was registered through the user-interface builder block
      And none was registered ad hoc elsewhere in startup

  @B-018
  Scenario: A view model that filters, sorts, groups or ages anything is reported
    Given a view model that filters a collection, sorts one, groups one or computes staleness
     When the project is built
     Then it is reported at the expression
      And the diagnostic names the pipeline as the owner

  @B-019
  Scenario: A unit is converted by a named member, not inside a binding
    Given an altitude reported in metres
     When the detail pane projects it for display
     Then the conversion happened in an explicitly named member
      And the domain value is still in metres

  @B-020
  Scenario: A view model's dependencies stop at the tracker
    Given a view or view model naming a contract, client, cache, snapshot, strategy, concrete tracker source or the decorator
     When the project is built
     Then the reference is reported at the line that writes it
      And the fleet view models depend on the tracker, the query and the actors only

  @B-021
  Scenario: Swapping the source edits no view
    Given a dashboard bound to the aircraft source
     When a second source's description arrives
     Then the columns, groupings and filter choices change
      And no view, markup or comparer was edited to make it happen

  @B-022
  Scenario: Only the detail pane asks which kind of vehicle it has
    Given a cast, an "is" check or a switch on a concrete vehicle type outside the detail pane
     When the project is built
     Then it is reported at the expression
      And the same expression inside the detail pane is not

  # ──────────────────────── The arrival notice on screen ────────────────────────

  @B-023
  Scenario: The banner shows the latest notice
    Given a dashboard bound to a fleet of thirty-seven aircraft
     When a notice arrives reporting 14:32:10, one added, two updated and one removed
     Then the banner shows that instant and those counts
      And it shows thirty-seven tracked
      And the view model computed none of the numbers

  @B-024
  Scenario: A quiet poll leaves the banner and the grid alone
    Given a banner showing a notice from 14:32:10
     When a changeset arrives that changed nothing, so no notice is raised
     Then the banner is unchanged
      And no row was cleared, reordered or re-created
      And the banner overlays no row of the grid

  @B-025
  Scenario: Only a notice worth interrupting for becomes a toast
    Given a dashboard whose toast interval is one second
     When an updated notice, a quiet notice and a resumed notice arrive
     Then a toast appears for the quiet notice and for the resumed notice
      And no toast appears for the updated notice
      And the swap completing also produces one

  @B-026
  Scenario Outline: The banner and the toast are paced separately, live
    Given a banner interval of <banner> and a toast interval of <toast>
     When notices arrive faster than both
     Then the banner updates at most once per <banner>
      And the toast interrupts at most once per <toast>
      And editing either field while running changes only that surface's pacing

    Examples:
      | banner     | toast      |
      | one second | one minute |
      | one second | one second |
      | ten seconds | one minute |

  @B-027
  Scenario: A view model that names a toast is reported
    Given a view model naming a toast, a snackbar or an alert type
     When the project is built
     Then it is reported at the reference
      And the same call inside a page is not

  # ───────────────────── Cards, the trail, and how a status looks ─────────────────────

  @B-029
  Scenario: A card is built from the roles the description names
    Given a description whose card roles are callsign, origin country, place, and altitude, speed and leg as readouts
     When the fleet is shown at a width that fits three cards across
     Then each aircraft is one card, three to a line
      And each card shows those columns' cells in those roles
     When the page narrows to fit one card across
     Then the same cards reflow into one column and none is re-created

  @B-029
  Scenario: A role the source leaves empty is absent, not blank
    Given a second description that names no place role
     When the live source is swapped for it
     Then its cards carry no place line and no empty space where one was
      And no view or markup was edited

  @B-030
  Scenario: A card shows how far its vehicle moved, from the element
    Given SYN101's element carries a leg of 11.8 km and a total of 101.2 km
     When its card is projected
     Then it shows "11.8 km" since the last poll and "101.2 km" since first seen
      And no view model measured either
     When SYN102's element carries no leg and no total
     Then its card shows neither, rather than "0 km"

  @B-031
  Scenario: A card's age moves with the observed instant, not with a clock
    Given SYN101 last reported at 14:32:10
      And the observed instant is 14:32:28
     When its card is projected
     Then it shows "18s ago"
     When no poll arrives for a minute
     Then it still shows "18s ago", because nothing ticked
     When the observed instant advances to 14:32:43
     Then it shows "33s ago"

  @B-032
  Scenario Outline: A status is an icon and a word as well as a colour
    Given a card whose vehicle is <status>
     When the card is projected
     Then it carries the icon <icon> and the word "<word>"
      And its colour is never the only thing that says so

    Examples:
      | status                           | icon        | word          |
      | fresh                            | ring-full   | Live          |
      | stale                            | ring-hatch  | Stale         |
      | without a position               | crosshair   | No position   |
      | on the ground                    | ground      | On ground     |

  @B-033
  Scenario: The page is one dark theme, drawn from tokens
    Given the page and every view on it
     When their colours are read
     Then each comes from one set of named tokens, and none is a literal in a view
      And body text contrasts with the surface beneath it at least 7:1
      And every other label at least 4.5:1

  @B-034
  Scenario: The detail map draws the trail, and extends it
    Given SYN101 is selected and its element carries a trail of 30 points
     When the detail pane is shown
     Then the map draws one line through the 30 points, coloured along the trail measure's range
      And the ramp stays in order under protan, deutan and tritan vision
     When a changeset adds a 31st point
     Then the line is extended by one segment and not redrawn from empty

  @B-034
  Scenario: A gap in the trail is a break in the line
    Given SYN101's trail has a point marked as following a gap
     When the detail map is drawn
     Then the line breaks before that point
      And no segment joins the two sides of the silence

  @B-035
  Scenario: The detail pane lists the trail, newest first
    Given SYN101's trail of three points, the last two 9.6 km and 10.2 km from the one before
     When the detail pane is shown
     Then it lists three points, newest first, each with its last contact
      And the newest two show "10.2 km" and "9.6 km", and the first shows none
      And no view model computed a distance

  @B-036
  Scenario: The page shows when the next poll is due, and a refusal
    Given the tracker's poll status says the next poll is due at 14:32:25
     When the page is projected
     Then it shows the next poll due at 14:32:25
     When the status carries a refusal asking for 42 seconds
     Then it shows "Throttled" with its icon, and "retry in 42s"
      And nothing on the page started a timer to say so

  @B-036
  Scenario: A source that does not poll shows no poll status
    Given the live source is one that pushes, and the tracker publishes no poll status
     When the page is projected
     Then it shows no next poll and no throttling

  @B-037
  Scenario: The banner shows the recent window, not a list it keeps
    Given the tracker's window of recent notices holds twenty
     When the banner is projected
     Then it shows the latest notice's counts beside the added, updated and removed counts of all twenty
      And the view model holds no list of notices of its own
