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
  `// Given` / `// When` / `// Then` comments separating the phases inside. Those
  three markers are the only inline comments a test carries: **why** the case
  matters, what the hazard is, and which claim it belongs to go in an XML doc
  comment on the method, where a reader meets them before the body and a
  renamed claim is a cite that can be followed. A paragraph of prose above
  `// Given` is the same text in the worse place
  ([lesson 0003](../../../features/aircraft-source/.spec/lessons/0003-a-review-is-a-convention-nobody-wrote-down.md)).
- **AwesomeAssertions** for assertions, **NSubstitute** for test doubles,
  **`Rocket.Surgery.Extensions.Testing.AutoFixtures`** for building the system
  under test, `Akka.TestKit` for actors, and Flurl's `HttpTest` for anything
  HTTP ([`flurl-http-client`](../../flurl-http-client/SKILL.md) has the
  interception trap).
- **A fake is configured in the test that uses it, never built in a private
  helper.** `Substitute.For<T>()` and the `Returns` calls that arrange it belong
  in the test body: a helper that hands back a configured double puts the
  arrangement where the reader cannot see it, and makes one object the shared
  setup of every test that calls it — the state sharing xUnit's per-test
  construction exists to prevent. Nothing is shared the day the helper is
  written; the pitfall is the second caller that needs it configured slightly
  differently. **What varies comes from `[ClassData]` or `[MemberData]`** — a
  `TheoryData<…>` subclass in a `*Cases.cs` file beside the tests, which is also
  where the reason each case exists is written. A private helper may still build
  **data** a case needs (a domain value, a cache the test writes to); it may not
  build a double ([lesson 0013](../../../.spec/lessons/0013-a-fake-built-in-a-helper-hides-its-arrangement.md)).
- **Everything a test injects comes from a fixture, not only the system under
  test.** A collaborator the application composes — the scheduler provider, the
  clock — is built by its own fixture and handed in whole, so a test advances the
  real type rather than arranging a substitute's members. Substitute an interface
  the application has no implementation of; build the one it does.
- **A generated fixture names each builder method after its parameter's _type_**,
  not the parameter: `IObservedClockWriter` becomes `WithWriter`,
  `ISchedulerProvider` becomes `WithProvider`. Two parameters of one type
  therefore collide, and that type's fixture is hand-written over
  `AutoFixtureBase<T>` instead, saying so in its remarks.
- **A test that stands up a host stands up the application's own composition.**
  One registration extension takes configuration and does the whole wiring; a
  test calls that and varies configuration. A test that assembles the same graph
  by hand is a second composition to keep in step, and the first production
  scenario it misses passes.
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
- **Substituting an `internal` interface needs a second grant.** NSubstitute
  builds its doubles with Castle's dynamic proxy, which cannot see an
  `internal` type unless the assembly declaring it grants
  `InternalsVisibleTo("DynamicProxyGenAssembly2")`. `src/Transponder` does.
  Without it the failure is a run-time proxy error naming an inaccessible type,
  not a compile error, so it looks like a test bug rather than a missing grant.
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
- Fixtures are synthetic: invented callsigns, MMSIs, positions and countries.
- **A unit test reads no files.** Payload fixtures are `static readonly` fields
  on a static class — a raw string literal per payload — not `.json` files
  copied to the output directory. A test that reads from disk has a dependency
  on the file system and on a build step that places the file, and however small
  one read is it is paid on every run by every test. Keeping the payload in
  source also puts the row a test is about beside the index table it is read
  against.
- **Parsing a payload belongs to a type, not to each test class.** `IJson`
  carries the behaviour as a default interface implementation, and a payload type
  implements it, so no test holds a serializer or a `Deserialize` helper of its
  own.
- **Varying data goes through xUnit's own data pattern** — a `TheoryData<...>`
  subclass per set of cases, named `<Subject>Cases`, reached with `[ClassData]`
  (or `[MemberData]` where the cases are computed). Not a pile of `[InlineData]`
  attributes carrying escaped JSON, and **not** a static class named
  `...TestData` that is really a helper: the name says xUnit data, so it has to
  be xUnit data. Collaborators a test stands up are a builder or a fixture, and
  are named for what they build.

---
