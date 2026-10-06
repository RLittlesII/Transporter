# Transponder Agent Instructions

This document establishes development practices for the Transponder repository, emphasizing specification-driven development for a demo codebase (DynamicData + .NET MAUI "Fleet Tracking Dashboard") built to teach reactive patterns to line-of-business .NET developers.

## Core Principles

**Design Authority**: `README.md` is the canonical specification today — audience, core idea, the app, the operators, the data sources and their limits, demo resilience, the closing act, and the open items. When a Feature gets its own specification it lives in that Feature's `.spec/README.md` — [`spec-and-traceability`](.skills/spec-and-traceability/SKILL.md) holds the model, [`transponder-conventions`](.skills/transponder-conventions/SKILL.md) this repository's layout, and [`.spec/templates/feature.md`](.spec/templates/feature.md) the blank. When conflicts arise between implementation and specification, the specification takes precedence and must be updated alongside code changes.

**Specification-Driven Chain**: Development follows an artifact progression from specification (§§ 1-5 of the Feature's `.spec/README.md`) → claim (§ 3) → scenario (its `.feature` file) → work item (`<id>.yml`, beside that Feature's specification) → xUnit test → implementation. **The specification comes first and stands alone** — it needs no work item to exist, because it is the agreement that items are later cut from. The item sits where _delivery_ begins, not where the thinking begins; a bug, spike or chore starts there instead, and may produce a spec delta afterwards. Every stage must be traceable through § 9 Traceability Matrix, and none can be skipped without documented exemption. Scenarios are documentation — there is no Gherkin runner in this repository; the tests in `test/UnitTests` are what execute.

**Four Mandatory Rules**:

1. Author or amend scenarios before implementing changes
2. Correct specifications rather than relying on conversation for clarification
3. Pull requests document what changed upstream through their body text
4. Specifications explicitly state boundaries — what not to build — as clearly as what to build

**Exemption Requirements**: Refactoring that preserves behavior may skip new scenarios only by citing specific preserved claim IDs. The exemption cannot be claimed merely because scenario-writing feels slow; the contributor must demonstrate which existing claims remain valid. Nothing automates this check — review enforces it.

## Traceability and Identifiers

Claims are numbered rows in § 3 Acceptance Criteria of a Feature's `.spec/README.md`, using `B-00n` identifiers that never change or get reused. Each scenario in the companion `.feature` file carries the matching `@B-00n` tag, and § 9 Traceability Matrix is the gate: every claim appears there exactly once, and a `Missing` row blocks ship. The blank is `.spec/templates/feature.md`. Nothing generates or checks these — there is no `docs/traceability.md` and no build-time ID check, so a duplicate, malformed or dangling id is caught by reading or not at all.

## Roles and Workflow

Four roles own the specification chain — `spec-author`, `test-writer`, `implementer`, `spec-reviewer` — each declared under [`.agents/`](.agents/README.md), each owning named sections of a Feature's `.spec/README.md` and writing no others. Which role owns which section is written in exactly one place: [`transponder-conventions`](.skills/transponder-conventions/SKILL.md) § "Section ownership". Each role trusts only the artifact from the preceding role. These files are documented contracts for whoever takes the role, a person or an agent; this repository ships no loadable agents, since the directory is `.agents/`, not `.claude/agents/`.

## Lessons and Bug Fixes

When a bug fix reveals a specification gap, a lesson document in the Feature's `.spec/lessons/` records symptom, root cause, spec delta, and the relevant claim ID in the same pull request. Process-related lessons also update the corresponding skill. Specifications never stay silent about fixed bugs — silence perpetuates the same issue later.

## Delivery Workflow

**A Feature begins with its specification**, authored with no work item; items are cut from its § 3 claims afterwards. A bug, spike or chore begins with an item outright.

Everything else about delivery — the local tracker, the item schema and status enum, branch and pull-request conventions, what a closed item keeps — is written in [`transponder-conventions`](.skills/transponder-conventions/SKILL.md) § `deliver-change`, under the method [`deliver-change`](.skills/deliver-change/SKILL.md) describes.

One rule belongs here, because it is what stops two people taking the same work: **`status: in-progress` is the only signal an item is taken.** Set it before the worktree, the branch, or the first edit, and never pick up an item that already carries it.

## Code Conventions

Each class of rule has one home. Read the owner, not a copy of it:

| What                                                                                | Where it is written                                                   |
| ----------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| Naming, modifier order, braces, analyzer suppressions                               | [`.editorconfig`](.editorconfig) — the compiler reads it              |
| Project layout, where integrations go, testing conventions, build and test commands | [`transponder-conventions`](.skills/transponder-conventions/SKILL.md) |
| Package versions                                                                    | [`Directory.Packages.props`](Directory.Packages.props)                |
| Build targets, CI, regenerating the workflow                                        | [`nuke-build`](.skills/nuke-build/SKILL.md)                           |

Four traps are worth carrying here, because each costs real damage when missed
and none of them changes:

- **No `Async` suffix on a method** — the return type says whether it is
  asynchronous. PascalCase for types and non-field members, `I`-prefixed
  interfaces, `_camelCase` private fields. `.editorconfig` is the authority.
- **Integration code does not live under `Features/`.** A provider's contract,
  its implementation, and the client and cache above it belong to the provider,
  not to one feature.
- **Never hand-edit a generated file** — the Nuke-generated
  `.github/workflows/ci.yml`. The workflow is regenerated deliberately, never by
  a build.
- **Never commit a generated report.** `format.json` is `dotnet format`'s report:
  it is gitignored, because every entry carries the absolute `FilePath` of the
  machine that produced it.
- **Never pin a package version in a `.csproj`.** Versions are central.

Which packages are referenced and which build targets exist is repository
state, not a rule: read `Directory.Packages.props` and `.build/Build.cs`. A
green build proves only that the targets it declares ran.

## Documentation Structure

Every tracked markdown file opens with YAML frontmatter (`title`, `description`, `type`); skills and agent files declare `name` and `description` only. The type enum and the rest of the rule are in [`transponder-conventions`](.skills/transponder-conventions/SKILL.md) § "Documentation and diagrams". The blanks in `.spec/templates/` carry the type of the file they produce, so a copy needs no frontmatter edit beyond its title and description. Runtime prompts are exempt.

## Knowledge Graph Integration

A knowledge graph can live at `graphify-out/`. **When `graphify-out/graph.json` exists, query it before browsing the tree**: `graphify query "<question>"` for a scoped subgraph, `path` for how two concepts relate, `explain` for a single concept.

- If you keep a graph, run `graphify update .` after changing code, or the next query answers from a stale tree.
- A changed `.graphifyignore` needs a full rebuild (`graphify . --force`): an existing graph keeps a newly ignored path, because `update` never prunes it.
- **The output stays local and is never committed** — `.gitignore` excludes `graphify-out/`. Nothing in this repository depends on a graph existing, so a clone without one loses nothing.
- The specification reaches the graph as the markdown it already is — each Feature's `.spec/README.md`, with its claims in § 3 and their coverage in § 9 — not from `.feature` files, since graphify does not ingest Gherkin. No fragment or export script exists, and none is needed.

## Skills Reference

**Project skills live in `.skills/`**, one directory per skill, each a `SKILL.md` declaring `name` and `description`. A skill holds the rule, the trap and the `Never add` list; facts live where they are authoritative and the skill links them (`coding-conventions` § "Skills", [lesson 0001](.spec/lessons/0001-a-skill-holds-the-rule-not-the-facts.md)).

Skills come in three kinds, and a skill is never a mixture of them.

**Method** — portable to another repository; names no path, command or provider. Each one's **Project rules** preamble points at the companion:

| Skill                   | Covers                                                                                       |
| ----------------------- | -------------------------------------------------------------------------------------------- |
| `deliver-change`        | item → worktree → specification → pull request → checks                                      |
| `coding-conventions`    | orient, stop on a gap, keep the design direct, put a rule where it runs, what a skill is for |
| `test-from-scenarios`   | a claim first, an injected clock, synthetic fixtures, an assertion that can fail             |
| `clarify-requirements`  | when to ask versus decide, and writing the answer back                                       |
| `spec-and-traceability` | the specification model: two records, one authority each; claims and blast radius            |

**Companion** — the only skill that names this repository:

| Skill                     | Covers                                                                                                                                                                |
| ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `transponder-conventions` | paths, layout, `.editorconfig` traps, central packages, the `.issue/` tracker, the build and test commands, the `B-00n` scheme, the twelve sections and who owns each |

**Technology** — about a library or tool, and silent about the product:

| Skill                   | Covers                                                                                |
| ----------------------- | ------------------------------------------------------------------------------------- |
| `flurl-http-client`     | the client cache, URL building, keeping the response, 429 as data, tokens, `HttpTest` |
| `dynamic-data-pipeline` | `EditDiff` in the client through `Bind`, staleness and expiry                         |
| `akka-actor`            | actor shape, registration, supervision, what an actor is for                          |
| `mapping`               | Mapperly at one boundary; conversions and derived values stay explicit                |
| `language-ext-usage`    | `Option`/`Either`, and where they stop                                                |
| `mvvm`                  | thin view models: user input in, actor message out, projection back                   |
| `maui-ui`               | C# markup, the binding surface, the swap test                                         |
| `nuke-build`            | targets, the CI path-filter trap, regenerating the workflow                           |

**Architecture** — this system's shape, with the reasoning in an ADR:

| Skill             | Covers                                                                                                                                                |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `api-contract`    | the components between a provider and the domain, and which responsibility each owns ([ADR-0002](.spec/adr/0002-contract-client-strategy-tracker.md)) |
| `hot-swap-source` | the swap decorator, disposal discipline, what must not be rebuilt ([ADR-0003](.spec/adr/0003-scrutor-for-decorator-registration.md))                  |
| `domain-model`    | the abstract base, units, optional values ([ADR-0005](.spec/adr/0005-an-abstract-base-carries-the-tracked-item.md))                                   |

Read the method skill your work belongs to **and** `transponder-conventions`, plus the skills a role names before acting in that role. The stage-day runbook is [`docs/runbook.md`](docs/runbook.md), not a skill — it is operational, not a rule.
