@boundary-analyzer
Feature: Boundary analyzer — the layer claims reported as compiler diagnostics

  As the person who has to decide whether this repository ships
  I want the eighteen structural claims another specification assigns to an analyzer
    reported at the line that violates them, while it is being written
  So that a boundary is a build error for everyone rather than an agreement the author
    remembers, and so that eighteen Missing rows can close for a reason other than hope

  # Scenarios are documentation. There is no Gherkin runner in this repository;
  # the xUnit tests execute, each citing the claim it proves. Every claim id
  # below is this Feature's own; a claim belonging to the specification this
  # analyzer enforces is written with its name, as "aircraft-source B-045".
  #
  # The eighteen rules' own subject matter is NOT specified here. Each is proven
  # by the test aircraft-source § 9 already names for it, and that matrix owns
  # those names. What follows is the analyzer's conventions: what a diagnostic
  # is, where it lands, what it may look at, and what it may not become.

  # ─────────────────────────────── Diagnostic surface ───────────────────────────────

  @B-001
  Scenario: One diagnostic per claim, one claim per diagnostic
    Given the analyzer's supported diagnostics
      And the mapping table in § 7 of the specification
    When each diagnostic id is read against the claim it enforces
    Then every diagnostic enforces exactly one claim
      And every enforced claim has exactly one diagnostic
      And no two diagnostics enforce the same claim

  @B-002
  Scenario: Every diagnostic id is TRN-prefixed and carries no claim number
    Given the analyzer's supported diagnostics
    When their ids are read
    Then every id matches the TRN prefix followed by four digits
      And the ids run from TRN0001 upwards with no gaps
      And no id is the number of the claim it enforces

  @B-003
  Scenario: A build message names the agreement that broke
    Given a diagnostic reported for a violation of aircraft-source B-045
    When its title and message are read
    Then they name the specification the claim belongs to
      And they name the claim id
      And a reader who does not hold this specification can tell which rule fired

  @B-004
  Scenario: A diagnostic lands on the node that violated the claim
    Given a view model that constructs a response envelope inside one of its methods
    When the compilation is analyzed
    Then a diagnostic is reported at that construction, not at the type or the file
      And it carries a file, a line and a column
      And its message names the symbol that was referenced

  @B-004
  Scenario: A diagnostic is never reported with no location
    Given any source the analyzer reports on
    When every reported diagnostic is collected
    Then none of them has an empty location
      And none of them has an empty symbol name in its message

  @B-005
  Scenario: Generated code is not analyzed
    Given a generated source file that names the positional row from outside the integration
    When the compilation is analyzed
    Then nothing is reported for that file
      And the same violation written by hand in a file of its own is still reported

  @B-006
  Scenario: Every diagnostic stops the build by default
    Given the analyzer's supported diagnostics
    When their default severities are read
    Then every one of them is error
      And weakening one requires a severity entry in .editorconfig

  @B-007
  Scenario: A diagnostic id is never renumbered and never reused
    Given the analyzer's supported diagnostics
      And the list of ids retired by a withdrawn rule
    When the ids are compared
    Then no id appears twice
      And no retired id has been handed to a different rule

  @B-008
  Scenario: The analyzer carries exactly the claims it was assigned and nothing more
    Given the eighteen aircraft-source claims ADR-0006 assigns to this mechanism
      And the mapping table in § 7 of the specification
    When the two sets are compared
    Then every assigned claim has a diagnostic
      And no diagnostic enforces a claim that is not in that set
      And no diagnostic exists that no claim asked for

  # ──────────────────────── What a rule may examine ────────────────────────

  @B-009
  Scenario: A reference inside a method body is a reference
    Given a cache whose signatures name no domain type
      And one of its methods that declares a local of a domain type, casts to it, and discards it
    When the compilation is analyzed
    Then the reference is reported
      And the rule does not report the cache as satisfied on the strength of its signatures

  @B-010
  Scenario: A claim about a declaration is reported on that declaration
    Given a snapshot that declares a member derived rather than reported
    When the compilation is analyzed
    Then the diagnostic is reported on that member's declaration
      And not at a call site that reads it

  @B-011
  Scenario: A claim about registration is read where the registration is written
    Given a container registration that aliases the contract to an implementation twice
    When the compilation is analyzed
    Then the diagnostic is reported at the registration call
      And not on the implementation type's declaration, which is unchanged and legal

  @B-012
  Scenario: A double produced by a mocking framework is reported at the call that produces it
    Given a test that obtains the API contract's double from a mocking framework
    When the compilation is analyzed
    Then the diagnostic is reported at that call
      And a test using the hand-written fake is not reported

  @B-013
  Scenario: No diagnostic asserts a computed value
    Given the mapping table in § 7 of the specification
    When each enforced claim is classified as structural or behavioural
    Then none of them is a claim about a computed value
      And the padded callsign, the absent category and the deferred poll remain xUnit tests

  # ──────────────────── Shipping, and the build gate ────────────────────

  @B-014
  Scenario: The analyzer is loaded by the compiler and shipped to nobody
    Given the analyzer project referenced by the projects it analyzes
    When the application is built and published
    Then the analyzer's diagnostics were reported during compilation
      And no analyzer assembly appears in the application's output
      And no application project references it as an assembly

  @B-015
  Scenario: The analyzer and the application share no dependency
    Given the analyzer project's package references
      And the application projects' package references
    When the two sets are compared
    Then the Roslyn packages the analyzer needs are referenced by the analyzer project and its tests
      And no application file names a type from the analyzer
      And the test project names it only in order to test it

  @B-016
  Scenario: Each rule is proven under the name the other specification already gave it
    Given the eighteen test names in § 9 of aircraft-source
    When the test project is searched for each of them
    Then every name exists, spelled exactly as that matrix spells it
      And no rule is proven by a test named something else instead

  @B-017
  Scenario: A violation fails the ordinary build
    Given a seeded violation of aircraft-source B-004 on a branch
    When ./build.sh runs as the pull-request workflow runs it
    Then the build fails
      And the failure names the TRN diagnostic and the line that caused it
      And no separate lint step had to be invoked to find it

  # ──────────────────── One store for the mapping ────────────────────

  @B-018
  Scenario: A rule cannot exist before the claim it enforces
    Given a diagnostic descriptor that no row of the mapping table claims
    When the descriptors are read
    Then it is reported as unclaimed
      And the remedy is a § 3 row in the specification it would serve, written by that specification's spec-author

  @B-019
  Scenario: The mapping table names a claim and restates nothing
    Given a row of the mapping table in § 7 of the specification
    When the row is read
    Then it names the specification and the claim id
      And it does not restate the claim's text
      And it does not name the test, because § 9 of that specification owns it
