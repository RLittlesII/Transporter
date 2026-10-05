# Testing

xUnit, the assertion and double libraries, generated fixtures, and where analyzer tests go.

- **An analyzer, a code fix or a generator is tested through
  [`analyzer-testing`](../../analyzer-testing/SKILL.md)**, which holds the harness,
  the traps in it, and the rule that a diagnostic ships a fix or records why it
  cannot. Do not hand-roll a compilation.
- **xUnit**, in [`test/UnitTests`](../../../test/UnitTests); `Xunit` is a global
  using in the project file. For xUnit's own attributes and fixtures, see
  [xunit.net](https://xunit.net).
- **`GivenX_WhenY_ThenZ` method names**, with
  `// Given` / `// When` / `// Then` comments separating the phases inside.
- **AwesomeAssertions** for assertions, **NSubstitute** for test doubles,
  **`Rocket.Surgery.Extensions.Testing.AutoFixtures`** for building the system
  under test, `Akka.TestKit` for actors, and Flurl's `HttpTest` for anything
  HTTP ([`flurl-http-client`](../../flurl-http-client/SKILL.md) has the
  interception trap).
- **A system under test is built by a generated fixture, never by a
  constructor call in the test.** Declare
  `[AutoFixture(typeof(T))] internal partial class TFixture;` beside the tests
  that use it, and take the subject through the implicit conversion:
  `T sut = new TFixture().WithX(x);`. A constructor change then edits one
  fixture rather than every test that names the type. The fixture is a builder
  and holds nothing — no `Sut` property, no seeding methods, no test data; a
  constructor on it exists only to give a concrete dependency a default the
  generator would otherwise leave `null`. The package's own documentation is
  the authority on the generated surface.
- `coverlet.collector` is referenced, so coverage is collectible; no threshold
  is enforced.
- **A test reaches an `internal` type through `InternalsVisibleTo`, not by
  widening the type.** An integration keeps its contract, its wire types and
  its implementation `internal` so nothing outside can name them; the project
  that holds them grants `InternalsVisibleTo("Transponder.UnitTests")` once, in
  its `.csproj`. Making a type `public` so a test can see it is the visibility
  claim being lost to the convenience of testing it.
- **Scenarios here are documentation.** A Feature's `.feature` file is the
  readable specification and the xUnit tests execute; there is no Gherkin
  runner, no bindings and no step definitions, so no `@ignore` tag and nothing
  else implying the scenarios run.
- Fixtures are synthetic: invented callsigns, MMSIs, positions and countries
  committed as JSON beside the tests that use them.

---
