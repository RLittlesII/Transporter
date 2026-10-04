---
name: coding-conventions
description: Transponder's C# naming, layout, and project conventions as enforced by .editorconfig and AGENTS.md — including the no-Async-suffix rule, private field prefix, feature folder layout, and central package versions. Use before writing or reviewing any C#.
---

# Coding conventions

The enforceable rules live in [`.editorconfig`](../../.editorconfig); the
project-level ones in [`AGENTS.md`](../../AGENTS.md). This file is the short
version with the traps called out. Where this file and `.editorconfig`
disagree, `.editorconfig` wins — it is the one the compiler reads.

## Naming

- **Types** (class, struct, interface, enum) — PascalCase.
- **Non-field members** (property, event, method) — PascalCase, at every
  accessibility.
- **Interfaces** — `I`-prefixed: `ITrackerSource`. An abstract base class is
  not an interface and takes no prefix: `TransportVehicle`, never
  `ITransportVehicle`.
- **Private fields** — `_camelCase`. The underscore prefix is a naming rule
  with warning severity, not a preference.
- **Methods do not carry an `Async` suffix** (`AGENTS.md`). The return type
  says whether it is asynchronous. This one catches everyone arriving from
  another .NET codebase: it is `Refresh()`, not `RefreshAsync()`.

## Layout

- `csharp_preferred_modifier_order` is explicit and ends with `async`:
  `public, internal, protected, private, static, extern, abstract, virtual,
  new, sealed, override, readonly, unsafe, volatile, async`.
- Braces on their own line (`csharp_new_line_before_open_brace = all`), and
  before `else`, `catch`, `finally`.
- `System` using directives sort first.
- Private fields sit at the **bottom** of the type in existing code — see
  [`ClickActor`](../../src/Transponder/Features/Demo/Actors/ClickActor.cs) and
  [`AkkaHostBuilder`](../../src/Gui/Container/AkkaHostBuilder.cs). Follow it
  for consistency; it is a style choice, not a rule the build checks.
- `CA2007` (await without `ConfigureAwait`) is excluded for async-void
  methods — see `AGENTS.md` before adding a suppression of your own.

## Project structure

- Feature code lives under
  `src/Transponder/Features/<FeatureName>/{ViewModels,Actors}`. The existing
  `Features/Demo` pair is the template.
- **Integration code does not live under `Features/`.** A provider's API
  contract, its implementation and the client and cache above it belong to that
  provider, not to one feature, and two features can want the same provider.
  They go under `src/Transponder/Integrations/<Provider>/`, split
  `Contracts/` (the interface callers name), `Http/` (the one `internal sealed`
  implementation) and `Container/` (its registration). Nothing in `Features/`
  may hold a contract, a wire type or a cache.
- `src/Transponder` is the model, the feature logic and the integrations;
  `src/Gui` is the MAUI host, pages and container wiring; `test/UnitTests` is
  the test project (root namespace `Transponder.UnitTests`).
- Container wiring extends the existing builder blocks —
  [`AkkaHostBuilder`](../../src/Gui/Container/AkkaHostBuilder.cs) and
  [`UserInterfaceBuilder`](../../src/Gui/Container/UserInterfaceBuilder.cs),
  which use C# `extension(MauiAppBuilder)` members — rather than piling
  registrations into [`MauiProgram`](../../src/Gui/MauiProgram.cs).

## Packages

- **Versions are central.** All versions live in
  [`Directory.Packages.props`](../../Directory.Packages.props) with
  `ManagePackageVersionsCentrally` and transitive pinning on. A `.csproj`
  carries `<PackageReference Include="..." />` with **no `Version`
  attribute** — pinning one there is the drift this setup exists to prevent.
- Adding a package means a `PackageVersion` entry in the props file and a
  reference in the project that needs it, in the same change.

## Generated files are not editable

- [`format.json`](../../format.json) and the Nuke-generated
  `.github/workflows/ci.yml` are generated (`AGENTS.md`). Change the
  generator, never the output.
- Mapperly's generated mappers are generated too — see
  [`mapping`](../mapping/SKILL.md).
- `obj/` and `bin/` are build output and never appear in a diff.

## Tests

xUnit in `test/UnitTests`, `GivenX_WhenY_ThenZ` naming, synthetic data. The
detail is in [`test-from-scenarios`](../test-from-scenarios/SKILL.md).

## Diagrams and docs

- Diagrams are Mermaid (`AGENTS.md`), inline in the markdown that needs them.
- Every tracked markdown file opens with YAML frontmatter (`title`,
  `description`, `type`). **Skills and agents are the exception**: they
  declare `name` and `description` only, and their type comes from the path.

## Never add

- An `Async` method suffix.
- A `Version` attribute on a `PackageReference`.
- A hand edit to a generated file.
- A blocking call on async code — no `.Result`, no `.Wait()`, no
  `GetAwaiter().GetResult()`.
- A suppression without a reason next to it saying why the rule does not
  apply here.
