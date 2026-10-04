---
name: transponder-conventions
description: Everything specific to this repository — paths, layout, naming, the local tracker, the build and test commands, the specification scheme and its section owners — extending the five method skills. Use for any change, alongside the method skill the work belongs to.
---

# Transponder conventions

Extends [`coding-conventions`](../coding-conventions/SKILL.md),
[`deliver-change`](../deliver-change/SKILL.md),
[`test-from-scenarios`](../test-from-scenarios/SKILL.md),
[`clarify-requirements`](../clarify-requirements/SKILL.md) and
[`spec-and-traceability`](../spec-and-traceability/SKILL.md); read the one your
work belongs to first. This skill holds only what is specific to this
repository, grouped under those skills and their section names.

---

## `coding-conventions`

### Before implementing

- Code graph, when one exists: `graphify query` / `graphify explain`
  (`AGENTS.md` § "Knowledge Graph Integration").
- Specification: [`README.md`](../../README.md) project-wide, and a Feature's
  `features/<slug>/.spec/README.md` where one covers the subject.
- Lessons: [`.spec/lessons/`](../../.spec/lessons/) repository-wide, and a
  Feature's own `.spec/lessons/`.

### Naming and layout

[`.editorconfig`](../../.editorconfig) is the authority and the compiler reads
it. The traps:

- **Types and non-field members** — PascalCase, at every accessibility.
- **Interfaces** — `I`-prefixed. An abstract base class is not an interface and
  takes no prefix.
- **Private fields** — `_camelCase`, at warning severity.
- **No `Async` suffix on a method** (`AGENTS.md`). The return type says whether
  it is asynchronous. This one catches everyone arriving from another .NET
  codebase: `Refresh()`, not `RefreshAsync()`.
- `csharp_preferred_modifier_order` is explicit and ends with `async`.
- Braces on their own line, and before `else`, `catch`, `finally`. `System`
  using directives sort first.
- Private fields sit at the **bottom** of the type in existing code. Follow it
  for consistency; it is a style choice, not a rule the build checks.
- `CA2007` is excluded for async-void methods — read `AGENTS.md` before adding a
  suppression of your own.

### Project structure

- `src/Transponder` — the model, the feature logic and the integrations;
  `src/Gui` — the MAUI host, pages and container wiring; `test/UnitTests` — the
  test project, root namespace `Transponder.UnitTests`.
- Feature code lives under
  `src/Transponder/Features/<FeatureName>/{ViewModels,Actors}`.
- **Integration code does not live under `Features/`.** A provider's API
  contract, its implementation, and the client and cache above it belong to that
  provider, not to one feature, and two features can want the same provider.
  They go under `src/Transponder/Integrations/<Provider>/`, split `Contracts/`
  (the interface callers name), `Http/` (the implementation) and `Container/`
  (its registration). Nothing under `Features/` holds a contract, a wire type or
  a cache.
- Container wiring extends the existing builder blocks in `src/Gui/Container/`,
  which use C# `extension(MauiAppBuilder)` members, rather than piling
  registrations into [`MauiProgram`](../../src/Gui/MauiProgram.cs).

### Dependencies

All versions live in
[`Directory.Packages.props`](../../Directory.Packages.props) with
`ManagePackageVersionsCentrally` and transitive pinning on. A `.csproj` carries
`<PackageReference Include="..." />` with **no `Version` attribute**.
`global.json` pins the SDK; `dotnet-tools.json` holds the local tool manifest.

### Generated files and guards

- [`format.json`](../../format.json) and
  [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) are generated —
  see [`nuke-build`](../nuke-build/SKILL.md) for how the workflow is
  regenerated. Mapperly's mappers are generated too
  ([`mapping`](../mapping/SKILL.md)).
- `obj/` and `bin/` are build output and never appear in a diff.

### Documentation and diagrams

- Every tracked markdown file opens with YAML frontmatter `title`,
  `description`, `type`, where `type` is one of `adr`, `spec`, `decision`,
  `guide`, `readme`, `lesson`, `instructions`, `template` (`AGENTS.md`).
- **Skills and agent files declare `name` and `description` only**, with the
  type derived from the path. No `permalink` and no `metadata` block — a skill
  carrying either was copied from another repository.
- `.skills/` and `.agents/` files are edited directly; there is no install
  manifest or generated copy here.
- Diagrams are Mermaid, inline in the markdown that needs them.

---

## `deliver-change`

### The tracker

**Work is tracked locally.** A `<id>-<slug>.yml` item stands in for a GitHub
issue, and there are **no issues, labels or milestones** in this workflow. Code
still pushes to GitHub, so branches, pull requests and CI stay — though no
remote is configured yet, so those conventions apply from the first push
onward. The schema and the status enum are below under
`spec-and-traceability`.

**An item sits beside the specification it was cut from**:
`features/<slug>/.issue/`, a sibling of that Feature's `.spec/`. An item that
belongs to no Feature — a bug, a spike, a chore — goes in the repository-root
`.issue/`, the same blast-radius split `.spec/adr/` and `.spec/lessons/` use.
**Ids stay repository-wide** from one `.issue/.sequence`, so a bare id in
`depends_on` or `parent` resolves from any Feature; find one with
`**/.issue/<id>-*.yml`.

- `status: in-progress` is the mark that an item is taken; `in-review` once the
  pull request opens; `done` with a `closed:` date when it lands. Bump
  `updated:` on every edit.
- A pull request body says `Delivers <id>` — the bare id, since one id has one
  item wherever it lives — or `Specifies features/<slug>` when authoring a
  specification. There is no issue for `Closes` to close.

### Worktree and branch

- Branch: `<id>/<short-description>` from the item id, or
  `spec/<feature-slug>` when authoring a specification, which has no item to
  take an id from.
- Worktree path: `.claude/worktrees/<branch>`.
- Report prefix: `[<id> · PR #<pr>]`, or `[<id>]` before the pull request
  exists; a specification uses the Feature slug in place of an id.

### Verify and publish

Under the method skill's step numbers:

1. `./build.sh` (`./build.cmd` on Windows, and what CI invokes), then
   `dotnet test test/UnitTests` for what you changed. Read
   [`nuke-build`](../nuke-build/SKILL.md) for what `Default` covers before
   treating a green build as a green test run, and note that `Format` proceeds
   after failure, so read its output rather than the exit code. CI skips a
   markdown-only pull request, so run it locally for a documentation change too.
6. The user-visible surface is the MAUI app ([`maui-ui`](../maui-ui/SKILL.md)).
7. Launch against a recorded or simulated source
   ([`api-contract`](../api-contract/SKILL.md)), never a live provider.
9. CI runs `./build.cmd` on pull requests to `main`. A markdown-only pull
   request shows no check at all, by design — see
   [`nuke-build`](../nuke-build/SKILL.md).

### Exemptions from scenario coverage

**Nothing automates this check here.** There is no coverage gate and no pull
request template to pick a category from, so the exemption is an honesty rule
enforced by review: say which claim ids survive, in the pull request body.

---

## `test-from-scenarios`

- **xUnit**, in [`test/UnitTests`](../../test/UnitTests); `Xunit` is a global
  using in the project file. For xUnit's own attributes and fixtures, see
  [xunit.net](https://xunit.net).
- **`GivenX_WhenY_ThenZ` method names** (`AGENTS.md`), with
  `// Given` / `// When` / `// Then` comments separating the phases inside.
- **AwesomeAssertions** for assertions, **NSubstitute** for test doubles,
  `Akka.TestKit` for actors, and Flurl's `HttpTest` for anything HTTP
  ([`flurl-http-client`](../flurl-http-client/SKILL.md) has the interception
  trap).
- `coverlet.collector` is referenced, so coverage is collectible; no threshold
  is enforced.
- **Scenarios here are documentation.** A Feature's `.feature` file is the
  readable specification and the xUnit tests execute; there is no Gherkin
  runner, no bindings and no step definitions, so no `@ignore` tag and nothing
  else implying the scenarios run.
- Fixtures are synthetic: invented callsigns, MMSIs, positions and countries
  (`AGENTS.md`), committed as JSON beside the tests that use them.

---

## `clarify-requirements`

[`README.md`](../../README.md) is the project-wide specification; a Feature's
`.spec/README.md` is narrower and more recent over its own subject. Most
questions are answered in one of the two.

The choices that are material here, and worth one question:

- what a viewer of the running app sees, or whether the app survives a dead
  network;
- the data shape, or the domain model's public surface
  ([`domain-model`](../domain-model/SKILL.md));
- credential handling, a provider's terms, or its metered budget;
- whether the live source swap still works
  ([`hot-swap-source`](../hot-swap-source/SKILL.md)).

Everything else — the shape of a class, the name of a folder — is yours to
choose; [`.agents/README.md`](../../.agents/README.md) says why. Open questions
already known are listed in [`README.md`](../../README.md) § "Open items"; check
there before asking.

---

## `spec-and-traceability`

### Where things live

| Path | Holds |
|---|---|
| [`README.md`](../../README.md) | the project-wide specification |
| `features/<slug>/.spec/README.md` | one Feature's specification, twelve sections in order — permanent, never archived |
| `features/<slug>/.spec/<slug>.feature` | that Feature's scenarios |
| `features/<slug>/.spec/{decisions,lessons,adr}/` | that Feature's own records |
| [`.spec/adr/`](../../.spec/adr/) | **cross-cutting** technical decisions, numbered repository-wide from `0001` |
| [`.spec/lessons/`](../../.spec/lessons/) | **cross-cutting** lessons, numbered repository-wide from `0001` |
| [`.spec/templates/`](../../.spec/templates/) | the blanks |
| `features/<slug>/.issue/<id>-<slug>.yml` | one work item cut from that Feature's specification |
| [`.issue/`](../../.issue/) | **cross-cutting** work items — a bug, spike or chore with no specification; `.issue/.sequence` is the last id handed out, repository-wide |

There is no epic directory: an epic is an item of `type: epic` whose `children`
list the Features under it.

### The templates

| Template | Produces |
|---|---|
| [`feature.md`](../../.spec/templates/feature.md) | a Feature's `.spec/README.md` |
| [`adr.md`](../../.spec/templates/adr.md) | one `adr/` record |
| [`decision.md`](../../.spec/templates/decision.md) | one `decisions/` record |
| [`lesson.md`](../../.spec/templates/lesson.md) | one `lessons/` record |
| [`item.yml`](../../.spec/templates/item.yml) | one `.issue/` work item |

**[`item.yml`](../../.spec/templates/item.yml) is the item schema**, commented
field by field — `type`, `status`, `risk` and `title` are never omitted, and
`risk` is never inherited. Copy it; do not re-derive it from an example.

**Status enum**: `needs-decomposition`, `ready-for-architecture`,
`ready-for-implementation`, `ready`, `in-progress`, `in-review`, `blocked`,
`done`. `priority`, `rank` and `blocks` are derived, and
[`item.yml`](../../.spec/templates/item.yml) carries the derivation beside the
fields it applies to.

### Claim ids are `B-00n`

Claims are numbered rows in § 3 Acceptance Criteria; scenarios carry the
matching `@B-00n` tag; **§ 9 Traceability Matrix is the gate**, and a `Missing`
row blocks ship.

**Ids are scoped to their Feature, not to the repository.** Every specification
numbers from `B-001`, so one Feature's `B-007` and another's are different
claims and neither is renumbered for the other. What identifies a claim is the
pair: an item carries `claims:` alongside the `spec:` those claims live in.
Within one specification an id is never renumbered and never reused. Claims need
no sequence file, which is what lets two Features be specified at once.

An unbuilt claim is marked once, in § 9 — a row naming no test. § 3 carries no
build state, because it would be a second store for what § 9 already decides.
A **withdrawn** claim is the exception: § 9 has no row for a claim that was
never going to be built, so it is marked on the § 3 claim itself, which opens
`**Withdrawn** —`.

### Four-digit ids collide — always write the scheme

Claims carry a `B-` prefix and identify themselves. Nothing else here does:
three schemes number from `0001` independently, and the per-Feature schemes
start again in every Feature.

| Written in full | Scheme |
|---|---|
| `features/aircraft-source/.issue/0001-aircraft-source.yml` | work item, repository-wide sequence |
| `ADR-0001` | root ADR, repository-wide |
| `lesson 0001` | root lesson, repository-wide |
| `features/<slug>/.spec/decisions/0001` | that Feature's decisions |
| `features/<slug>/.spec/adr/0001` | that Feature's own ADRs |

**In prose, write the scheme with the number.** A number may appear bare only
where its field says what it is — `parent: "0001"` in an item, or a `## Tasks`
table whose column reads *Item*.

### Section ownership

Four roles own the specification chain, declared under
[`.agents/`](../../.agents/README.md). **This is the only place the mapping is
written**; each role file names its own sections and links here.

| Section | Owner |
|---|---|
| 1 Business Goal | `spec-author` |
| 2 User Needs | `spec-author` |
| 3 Acceptance Criteria | `spec-author` |
| 4 Constraints | `spec-author` |
| 5 Out of Scope | `spec-author` |
| 6 Concern Separation | `implementer` |
| 7 Technical Design | `implementer` |
| 8 Testing Strategy | `test-writer` |
| 9 Traceability Matrix | `test-writer` |
| 10 Lessons / Spec Deltas | the role that closed the bug |
| 11 Open Questions | any blocked role |
| 12 Sign-off | `spec-reviewer` |
| Decisions (index) | the role that made or reversed the call |
| `## Tasks` | `spec-author`, from that Feature's `.issue/` items |
| `## Scoring` | `spec-author`, from the item's value and risk |

**`implementer` owning § 7 is a compromise.** There is no separate architect
role here and § 7 must have exactly one owner, so it sits with the role that
writes the code. A design decision bigger than the item in hand stops and goes
to the person, with the options and the tradeoff named, rather than being
settled inside an implementation pull request.

### Never add

- A GitHub issue, label or milestone as part of this workflow. An `.issue/` is the
  tracker; a remote is for code.
- An `@ignore` tag, a step definition, or anything else implying the scenarios
  execute.
