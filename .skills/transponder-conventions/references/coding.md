# Coding conventions

Naming, layout, project structure, dependencies, generated files, comments.

## Before implementing

- Code graph, when one exists: `graphify query` / `graphify explain`
  (`AGENTS.md` § "Knowledge Graph Integration").
- Specification: [`README.md`](../../../README.md) project-wide, and the
  `.spec/README.md` beside the code where a Feature covers the subject.
- Lessons: [`.spec/lessons/`](../../../.spec/lessons/) repository-wide, and a
  Feature's own `.spec/lessons/`.

## Naming and layout

[`.editorconfig`](../../../.editorconfig) is the authority and the compiler reads
it. The traps:

- **Types and non-field members** — PascalCase, at every accessibility.
- **Interfaces** — `I`-prefixed. An abstract base class is not an interface and
  takes no prefix.
- **Private fields** — `_camelCase`, at warning severity.
- **A type is named for what every instance of it is**, not for the state some
  of them carry. `TrackedVehicle` holds a vehicle and a derived stale mark;
  `StaleVehicle` named it after the minority
  ([lesson 0012](../../../.spec/lessons/0012-a-type-named-for-the-exception-describes-the-minority.md)).
- **No `Async` suffix on a method.** The return type says whether it is
  asynchronous. This one catches everyone arriving from another .NET
  codebase: `Refresh()`, not `RefreshAsync()`.
- `csharp_preferred_modifier_order` is explicit and ends with `async`.
- Braces on their own line, and before `else`, `catch`, `finally`. `System`
  using directives sort first.
- Private fields sit at the **bottom** of the type in existing code. Follow it
  for consistency; it is a style choice, not a rule the build checks.
- `CA2007` is excluded for async-void methods. A suppression of your own needs
  a reason written beside it, not a second blanket exclusion.
- **Terse, and XML over inline.** A `<summary>` is one line. `<remarks>` is one
  sentence, and only for a fact the code cannot state. An inline comment is one
  line, says a _why_, and is never a paragraph. Reasoning belongs in the
  specification, an ADR or a lesson — those are linkable; a comment restating one
  drifts from it. Cite instead of explaining.
- **A comment is a maintenance burden, and the default is not to write one.**
  Two shapes are the ones reviews keep catching, and both are deletions rather
  than rewrites:
    - **Prose that restates the line below it.** A sentence above
      `AddSingleton<IAircraftTrackerSource, AircraftTrackerSource>()` saying the
      strategy is registered behind its seam says what the call says. If an
      analyzer covers the rule, or should, the comment is the wrong place for
      it ([`0051`](../../../.issue/0051-verbose-comment-diagnostic.yml) is the
      analyzer; [`0050`](../../../.issue/0050-scrub-comment-maintenance-burden.yml)
      is the scrub).
    - **A claim or record identifier inside comment prose.** A sentence
      carrying `(B-034, ADR-0002 item 6)` puts a reference in a second place,
      where no traceability matrix reads it and nothing fails when the claim is
      renumbered or withdrawn. A bare citation on a line that would be wrong
      without it is the form that survives; the paragraph around it is not.

    The test before writing one: delete it, and name what a reader loses. Nothing
    loses means it was never a comment.

## Project structure

- `src/Transponder` — the model, the feature logic and the integrations;
  `src/Gui` — the MAUI host, pages and container wiring; `test/UnitTests` — the
  test project, root namespace `Transponder.UnitTests`.
- **`tools/` holds development tooling, which does not ship.** A tool is its own
  console project referencing the application, reached through one more
  `InternalsVisibleTo`, and invented data lives there rather than in the product
  assembly — the synthetic payload generator
  ([`0052`](../../../.issue/0052-sample-recording-generator.yml)) is the first.
  A build target invokes one with `dotnet run` against its project path, because
  a `.build` that referenced the application could not compile while the
  application did not.
- Feature code lives under
  `src/Transponder/Features/<FeatureName>/{ViewModels,Actors}`.
- **Integration code does not live under `Features/`.** A provider's API
  contract, its implementation, and the client and cache above it belong to that
  provider, not to one feature, and two features can want the same provider.
  They go under `src/Transponder/Integrations/<Provider>/`, split `Contracts/`
  (the interface callers name), `Http/` (the implementation) and `Container/`
  (its registration). Nothing under `Features/` holds a contract, a wire type or
  a cache.
- **The three folders are for the provider's own surface, not for everything
  above it.** `Contracts/` holds the interface and the types its methods name,
  `Http/` the implementation and its converters, `Container/` the registration
  and nothing else. A snapshot record, the client that fills a cache from it,
  and the options and credentials the provider needs sit directly under
  `src/Transponder/Integrations/<Provider>/` — they belong to the provider but
  are not its wire surface. The domain model and the per-type strategies are
  not the provider's at all: they go under `src/Transponder/Model/` and
  `src/Transponder/Tracking/`, because they survive the provider being
  replaced.
- **`Features/` holds view models and actors, and the layer below them is not a
  Feature.** The domain model is `Model/`; the tracker seam, the pipeline over
  it, the observed clock and the published element are `Tracking/`. Two Features
  and the replay source all consume that layer, so filing it under one of them
  would make the other two reach into a sibling Feature's folder — the same
  reason integrations are not under `Features/` either.
- **A message a consumer tells an actor lives in `src/Transponder/Messages/`.**
  Not in the integration that receives it — a view model naming
  `Transponder.Integrations.OpenSky.Polling.PollAircraft` puts a provider's name
  on a surface that must survive the provider being swapped. Not in `Tracking/`
  either: `TRN0006` lets a view model name a class there only when
  `IFleetTracker` publishes it, and a message is not something the seam
  publishes. The message doubles as the `IActorRegistry` key, so the integration
  registers its actor under the message and a second provider's actor registers
  under the same one (`fleet-dashboard` § 10, 2026-10-07; ADR-0012).
- Container wiring extends the existing builder blocks in `src/Gui/Container/`,
  which use C# `extension(MauiAppBuilder)` members, rather than piling
  registrations into [`MauiProgram`](../../../src/Gui/MauiProgram.cs).

## Dependencies

All versions live in
[`Directory.Packages.props`](../../../Directory.Packages.props) with
`ManagePackageVersionsCentrally` and transitive pinning on. A `.csproj` carries
`<PackageReference Include="..." />` with **no `Version` attribute**.
`global.json` pins the SDK; `dotnet-tools.json` holds the local tool manifest.

## Generated files and guards

- [`.github/workflows/ci.yml`](../../../.github/workflows/ci.yml) is generated.
  **Never hand-edit it** — the next regeneration discards the edit. The
  workflow is regenerated deliberately, because `AutoGenerate` is off:

    ```
    ./build.sh --generate-configuration GitHubActions_ci --host GitHubActions
    ```

    Change the attribute, regenerate, and commit `.build/Build.cs` and `ci.yml`
    together. [`nuke-build`](../../nuke-build/SKILL.md) has the traps — what
    regeneration also rewrites, and why a negative-only path filter kills the
    workflow. Mapperly's mappers are generated too
    ([`mapping`](../../mapping/SKILL.md)).

- `format.json` is `dotnet format`'s report. It is gitignored and **never
  committed**: every entry carries the absolute `FilePath` of the machine that
  produced it, so committing one publishes a developer's home directory layout
  to a public repository. The same rule covers any tool report written to the
  repository root.

- `obj/` and `bin/` are build output and never appear in a diff.

## Documentation and diagrams

- Every tracked markdown file opens with YAML frontmatter `title`,
  `description`, `type`, where `type` is one of `adr`, `spec`, `decision`,
  `guide`, `readme`, `lesson`, `instructions`, `template`.
- **Skills and agent files declare `name` and `description` only**, with the
  type derived from the path. No `permalink` and no `metadata` block — a skill
  carrying either was copied from another repository.
- `.skills/` and `.agents/` files are edited directly; there is no install
  manifest or generated copy here.
- Diagrams are Mermaid, inline in the markdown that needs them.

---
