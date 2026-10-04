# Transponder Agent Instructions

This document establishes development practices for the Transponder repository, emphasizing specification-driven development for a demo codebase (DynamicData + .NET MAUI "Fleet Tracking Dashboard") built to teach reactive patterns to line-of-business .NET developers.

## Core Principles

**Design Authority**: The canonical specification resides in `features/README.md`. When conflicts arise between implementation and specification, the specification takes precedence and must be updated alongside code changes.

**Specification-Driven Chain**: Development follows an artifact progression from issue → scenario → supporting detail → step definitions → implementation. All four stages must be traceable and none can be skipped without documented exemption.

**Four Mandatory Rules**:
1. Author or amend scenarios before implementing changes
2. Correct specifications rather than relying on conversation for clarification
3. Pull requests document what changed upstream through their body text
4. Specifications explicitly state boundaries — what not to build — as clearly as what to build

**Exemption Requirements**: Refactoring that preserves behavior may skip new scenarios only by citing specific preserved claim IDs (format: `REQ-<AREA>-<NNN>`). The exemption cannot be claimed merely because scenario-writing feels slow; the contributor must demonstrate which existing claims remain valid.

## Traceability and Identifiers

Every scenario carries a stable tag (`@REQ-<AREA>-<NNN>`) that never changes or gets reused. `<AREA>` names the feature area under `src/Transponder/Features/<AREA>` (e.g. `Demo`), so a claim ID traces directly to the code it describes. Canonical documentation uses `CON-<PAGE>-<NNN>` identifiers. A generated traceability matrix (`docs/traceability.md`) tracks all claim coverage and fails the build if IDs are missing, malformed, or dangling.

## Roles and Workflow

Four roles own the specification chain, declared under `.agents/`:
- **spec-author** (`.agents/spec-writer.md`): writes scenarios and states out-of-scope boundaries
- **test-writer** (`.agents/test-writer.md`): converts claims into failing step definitions
- **implementer** (`.agents/implementer.md`): makes tests pass against cited claims only
- **spec-reviewer** (`.agents/spec-reviewer.md`): judges diffs against claims and ADRs

Each role trusts only the artifact from the preceding role.

## Lessons and Bug Fixes

When a bug fix reveals a specification gap, a lesson document (`docs/lessons/`) records symptom, root cause, spec delta, and the relevant claim ID in the same pull request. Process-related lessons also update the corresponding skill. Specifications never stay silent about fixed bugs — silence perpetuates the same issue later.

## Delivery Workflow

- Every change begins with an issue
- Rebase onto fresh `origin/main` before committing (not just before pushing)
- Use `Closes #<number>` on its own line in PR bodies
- Titles must be squash-ready
- No `Co-Authored-By` trailers
- This repository has no remote configured yet; the above conventions apply from the first push onward

## Code Conventions

- Types and non-field members use PascalCase; interfaces are `I`-prefixed; private fields use `_camelCase` (see `.editorconfig`)
- Explicit modifier order, `async` last; `CA2007` async-void methods are excluded
- Methods do not carry an `Async` suffix; return types indicate asynchronicity
- Feature code lives under `src/Transponder/Features/<FeatureName>/{ViewModels,Actors}` — follow this layout for new features
- Use xUnit (`test/UnitTests`), Shouldly for assertions, `GivenX_WhenY_ThenZ` test names
- Use Mermaid for diagrams, synthetic data in tests
- Never hand-edit generated files (`format.json`, the Nuke-generated `.github/workflows/ci.yml`)
- Build via Nuke (`.build/Build.cs`): `Clean → Restore → Format → Compile`, invoked through `build.cmd`/`build.sh`/`build.ps1`
- Package versions are centrally managed in `Directory.Packages.props`; never pin a version in a `.csproj`

## Documentation Structure

Every tracked markdown file opens with YAML frontmatter (`title`, `description`, `type`). Types include `adr`, `spec`, `guide`, `readme`, `lesson`, `instructions`, or `template`. Skills and agents declare `name` and `description` only; their type is path-derived. Runtime prompts are exempt from frontmatter requirements.

## Knowledge Graph Integration

The `graphify` tool maintains a codebase knowledge graph. Queries, path analysis, and concept explanations surface relevant architecture before full browsing. The specification reaches the graph through `docs/traceability.md` (which carries all claim IDs) rather than directly from `.feature` files, since graphify ingests markdown, not Gherkin.

## Skills Reference

Focused guidance documents address specific domains relevant to this codebase: `dynamic-data`, `application-architecture` (Clean Architecture/CQRS/BLoC-style MVVM), `domain-driven-design`, `xunit`, `dotnet-build`, `nuke`, and `logging`. Consult the project skill extending each role agent before acting in that role.
