# Transponder Agent Instructions

This document establishes development practices for the Transponder repository, emphasizing specification-driven development for a demo codebase (DynamicData + .NET MAUI "Fleet Tracking Dashboard") built to teach reactive patterns to line-of-business .NET developers.

## Core Principles

**Design Authority**: `README.md` is the canonical specification today — audience, core idea, the app, the operators, the data sources and their limits, demo resilience, the closing act, and the open items. When a Feature gets its own specification it lives in that Feature's `.spec/README.md` (see the `specification` skill and `.skills/spec-and-traceability`). When conflicts arise between implementation and specification, the specification takes precedence and must be updated alongside code changes.

**Specification-Driven Chain**: Development follows an artifact progression from work item (`.issues/<id>.yml`) → claim (§ 3 of the Feature's `.spec/README.md`) → scenario (its `.feature` file) → xUnit test → implementation. Every stage must be traceable through § 9 Traceability Matrix, and none can be skipped without documented exemption. Scenarios are documentation — there is no Gherkin runner in this repository; the tests in `test/UnitTests` are what execute.

**Four Mandatory Rules**:
1. Author or amend scenarios before implementing changes
2. Correct specifications rather than relying on conversation for clarification
3. Pull requests document what changed upstream through their body text
4. Specifications explicitly state boundaries — what not to build — as clearly as what to build

**Exemption Requirements**: Refactoring that preserves behavior may skip new scenarios only by citing specific preserved claim IDs. The exemption cannot be claimed merely because scenario-writing feels slow; the contributor must demonstrate which existing claims remain valid. Nothing automates this check — review enforces it.

## Traceability and Identifiers

Claims are numbered rows in § 3 Acceptance Criteria of a Feature's `.spec/README.md`, using `B-00n` identifiers that never change or get reused. Each scenario in the companion `.feature` file carries the matching `@B-00n` tag, and § 9 Traceability Matrix is the gate: every claim appears there exactly once, and a `Missing` row blocks ship. This matches the global `specification` skill, which owns the template and the tooling. There is no separate generated `docs/traceability.md` and no build-time ID check in this repository.

## Roles and Workflow

Four roles own the specification chain, declared under `.agents/` (see `.agents/README.md`). Each owns named sections of a Feature's `.spec/README.md` and writes no others:
- **spec-author** (`.agents/spec-author.md`): §§ 1-5, the `.feature` file, `## Tasks`, `## Scoring`
- **test-writer** (`.agents/test-writer.md`): §§ 8-9, and the failing xUnit tests
- **implementer** (`.agents/implementer.md`): §§ 6-7, and the production code
- **spec-reviewer** (`.agents/spec-reviewer.md`): § 12, judging diffs against cited claims

Each role trusts only the artifact from the preceding role. The authoritative ownership table — including §§ 10-11, Decisions, and each role's counterpart in the installed agent council — lives in `.skills/spec-and-traceability/SKILL.md`. These files are documented contracts, not loadable agents: the directory is `.agents/`, not `.claude/agents/`.

## Lessons and Bug Fixes

When a bug fix reveals a specification gap, a lesson document in the Feature's `.spec/lessons/` records symptom, root cause, spec delta, and the relevant claim ID in the same pull request. Process-related lessons also update the corresponding skill. Specifications never stay silent about fixed bugs — silence perpetuates the same issue later.

## Delivery Workflow

- **Work is tracked locally.** Every change begins with a `.issues/<id>-<slug>.yml` item, which stands in for a GitHub issue. There are no issues, labels or milestones in this workflow; the schema and status enum are in `.skills/spec-and-traceability/SKILL.md`
- `status: in-progress` is the only signal an item is taken — set it before the worktree, the branch, or the first edit, and never pick up an item that already carries it
- A closed item stays, with `status: done` and a `closed:` date
- Rebase onto fresh `origin/main` before committing (not just before pushing)
- PR bodies name the item id and the `B-00n` claims delivered; there is no issue for `Closes` to close
- Titles must be squash-ready
- No `Co-Authored-By` trailers
- Code still pushes to GitHub as branches and pull requests; this repository has no remote configured yet, so those conventions apply from the first push onward

## Code Conventions

- Types and non-field members use PascalCase; interfaces are `I`-prefixed; private fields use `_camelCase` (see `.editorconfig`)
- Explicit modifier order, `async` last; `CA2007` async-void methods are excluded
- Methods do not carry an `Async` suffix; return types indicate asynchronicity
- Feature code lives under `src/Transponder/Features/<FeatureName>/{ViewModels,Actors}` — follow this layout for new features
- Use xUnit (`test/UnitTests`) with `GivenX_WhenY_ThenZ` test names, AwesomeAssertions for assertions, and NSubstitute for test doubles. AwesomeAssertions is not yet in `Directory.Packages.props`; the first issue that writes a test adds it
- Use Mermaid for diagrams, synthetic data in tests
- Never hand-edit generated files (`format.json`, the Nuke-generated `.github/workflows/ci.yml`). The `[GitHubActions]` attribute currently sets `AutoGenerate = false`, so the workflow is regenerated deliberately (`nuke --generate-configuration GitHubActions_ci --host GitHubActions`), never by a build
- Build via Nuke (`.build/Build.cs`): `Clean → Restore → Format → Compile`, invoked through `build.cmd`/`build.sh`/`build.ps1`. **There is no `Test` target yet** — a green build does not mean the tests ran
- Package versions are centrally managed in `Directory.Packages.props`; never pin a version in a `.csproj`

## Documentation Structure

Every tracked markdown file opens with YAML frontmatter (`title`, `description`, `type`). Types include `adr`, `spec`, `guide`, `readme`, `lesson`, `instructions`, or `template`. Skills and agents declare `name` and `description` only; their type is path-derived. Runtime prompts are exempt from frontmatter requirements.

## Knowledge Graph Integration

The `graphify` tool maintains a codebase knowledge graph. Queries, path analysis, and concept explanations surface relevant architecture before full browsing. The specification reaches the graph through each Feature's `.spec/README.md` — which carries the claims in § 3 and their coverage in § 9 — rather than from `.feature` files, since graphify ingests markdown, not Gherkin.

## Skills Reference

**Project skills live in `.skills/`** and win over a global skill of the same name where they differ:

| Skill | Covers |
| --- | --- |
| `transponder-domain-model` | `TransportVehicle` and its subclasses, units, optional values |
| `api-contract` | the snapshot seam, the OpenSky client, credits, credentials |
| `ais-stream` | the AISStream vessel feed (stretch goal) |
| `api-mock` | record, replay, and simulated sources |
| `dynamic-data-pipeline` | `EditDiff` through `Bind`, staleness and expiry |
| `hot-swap-source` | swapping the live source mid-demo |
| `akka-actor` | actor shape, registration, supervision |
| `mapping` | Mapperly at the wire boundary |
| `language-ext-usage` | `Option`/`Either`, and where they stop |
| `build-maui-ui` | C# markup, binding surface, the swap test |
| `mvvm` | thin view models: user input in, actor message out, projection back |
| `coding-conventions` | naming, layout, central packages |
| `test-from-scenarios` | xUnit, injected schedulers, synthetic fixtures |
| `spec-and-traceability` | where a specification lives, `B-00n` claims |
| `deliver-change` | issue → worktree → PR → checks |
| `clarify-requirements` | when to ask versus decide |
| `nuke-build` | targets, CI, generated files |
| `run-the-demo` | stage-day runbook |

Relevant global skills: `dynamic-data`, `specification`, `riok-mapperly`, `language-ext`, `nuke`, `xunit`, `dotnet-build`, `domain-driven-design`, `logging`. Consult the project skill extending each role agent before acting in that role.
