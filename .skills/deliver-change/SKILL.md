---
name: deliver-change
description: Deliver a change through a local .issues/ item, worktree, specification, build, and pull request. Use when picking up work, filing an item, committing, or opening and watching a PR.
---

# Deliver a change

**Work is tracked locally.** `.issues/<id>-<slug>.yml` stands in for a GitHub
issue; there are **no issues, labels or milestones** in this workflow. Code
still pushes to GitHub, so branches, pull requests and CI stay. The item
schema, the status enum, and the `.issues/`-versus-`.spec/` authority split
live in [`spec-and-traceability`](../spec-and-traceability/SKILL.md).

The other adjacent skills carry the rest of the detail:
[`coding-conventions`](../coding-conventions/SKILL.md),
[`test-from-scenarios`](../test-from-scenarios/SKILL.md), and
[`nuke-build`](../nuke-build/SKILL.md) for the build.

## Start

### Claim the item

- Work from one focused `.issues/` item.
- **Never pick up an item whose `status` is `in-progress`** — someone has
  claimed it, even if it looks stalled or is the obvious next piece. Find work
  by reading the directory: an item that is `ready` (or
  `ready-for-implementation`) with every `depends_on` already `done`. Asked for
  an `in-progress` item by id? Stop and ask the person.
- **Set `status: in-progress` the moment you pick it up, before anything
  else** — before the worktree, the branch, or the first edit. Update
  `updated:` in the same write.
    - No exceptions: not for a one-line fix, an item you just filed, or a
      resumed session. That field is the only thing telling the next agent it
      is taken.
    - Stop without a pull request? Put it back to `ready`.
    - It becomes `in-review` when the pull request opens, and `done` with a
      `closed:` date when the work lands. **A done item stays** — the file is
      permanent history.

### Settle the requirement before building

- **Put every open question to the person, and record the answers in the item,
  before writing any code.** A gap, an ambiguity, or a choice between two
  plausible readings is a question, not a decision for you to make — though in
  a demo repository where little is adopted, most implementation shapes *are*
  yours to choose. [`clarify-requirements`](../clarify-requirements/SKILL.md)
  draws that line.
- **A sub-agent brief says to stop and ask.** Any brief handed to another agent
  tells it to report a question rather than build on a guess, and the briefing
  agent has put its own open questions to the person first.
- **Sequence dependent items.** Items touching the same seam — the source
  interface, the domain base class, the cache wiring — run one after another,
  never side by side: the second starts from the first's merged result. Record
  the order in `depends_on` when the items are filed, not when the conflict
  shows up.

### File a new item

Write `.issues/<id>-<slug>.yml` with the full frontmatter from
[`spec-and-traceability`](../spec-and-traceability/SKILL.md) — **`type`,
`status`, `risk` and `title` are never omitted**, and `risk` is never
inherited. Take the id from `.issues/.sequence`, after rebasing (see "Commit,
rebase, claim identifiers"), and bump it in the same commit.

Body: a `## Summary` that is user-story shaped for a feature (`As a … I want …
so that …`), and acceptance criteria that **cite `B-00n` claim ids** rather
than restating them. An item with no spec yet carries `spec: null`.

#### Relationships

**Wire relationships when the item is created, not later.** Check every new
item against the open ones and against the others filed in the same pass. A
link known at filing and left unset is lost.

Filing several at once — plan the graph before writing any: which is the
parent, which are carved out of it, which must land first. Write the parent
first, then the children, then set every field in one pass.

The relations, strongest first. **Use the strongest that is true, never a
stronger one** — that judgment is the whole point, and it does not change
because the field is now YAML:

- **`depends_on`** — a hard prerequisite only: this cannot be implemented or
  verified until that one lands (a seam, a type, a mapper it builds on). Say
  why in the body's first paragraph. No entry for "touches similar code".
- **`parent` / `children`** — only when the item is carved out of a larger one
  being split, so the parent's scope shrinks. An independently scoped
  prerequisite is `depends_on`; a false hierarchy makes the rollup lie.
- **Never two items for the same scope.** Extend the existing one.
- **`blocks` is derived** — it is the inverse of other items' `depends_on`.
  Recompute it; never hand-author it as a claim of its own.
- Remove a stale `depends_on` when a design change kills the dependency, and
  recompute the `blocks` it implied.

### Worktree and branch

- **Never work on a branch in the primary checkout.** Create a worktree off
  fresh `origin/main`:
  `git fetch origin main && git worktree add -b <id>/<short-description> .claude/worktrees/<id>/<short-description> origin/main`,
  where `<id>` is the `.issues/` item id (`0001/poll-opensky`).
  Several agents share the repository; a worktree per item means none switches
  a branch out from under another.
- **Keep the primary checkout's `main` current, and change nothing in it.**
  `git -C <primary checkout> pull --ff-only origin main`, and that
  fast-forward is the only thing ever done there: no edits, commits, resets,
  merges or generator runs. If a pull would not fast-forward, stop and tell the
  person rather than repairing it.
- **Check at the first edit, every time.** Before the first `Edit` or `Write`
  for an item, run `git branch --show-current`. If it says `main`, stop and
  create the worktree. A resumed session or a plain "continue" does not look
  like starting an item, which is exactly when this gets skipped.
- **Push the branch at once**, from the worktree:
  `git push -u origin <id>/<short-description>`.
- Open every final report with `[<id> · PR #<pr>]` (just `[<id>]` before the
  pull request exists), even a one-line report.
- Do all work in the worktree. It comes down once the pull request is open.
- **Never use a bare `git stash` or `git stash pop` in a worktree.** There is
  one stash per clone, shared by every worktree and session: a bare stash takes
  another session's files, and a pop applies whatever is on top.
    - Park work in a WIP commit (`git commit -m "wip"`), amend or reset later.
    - A stash that cannot be a commit is tagged and applied by SHA:
      `git stash push -m <tag>`, note `git rev-parse stash@{0}`, then
      `git stash apply <sha>`. Never `pop`, `drop`, or `clear`.
    - Nothing enforces this; git has no pre-stash hook. It is a written rule.

### Commit, rebase, claim identifiers

- **Commit and push each unit of work as soon as it is done** — a scenario, a
  passing test, a document. Never hold work for the pull request.
- **Rebase onto fresh `origin/main` before every commit**, not only before a
  push (`AGENTS.md`): `git fetch origin main && git rebase origin/main`.
  Resolve conflicts locally; if the rebase brought commits in, re-run the
  affected checks before pushing. Rewrote commits already on `origin`?
  `git push --force-with-lease`, never plain `--force`.
- **Claim a shared identifier after that rebase** — an `.issues/.sequence` id,
  an ADR number, a claim id, a slug. It is claimed the moment someone else
  merges it. Lost the race? Take the new number and rewrite every reference;
  renaming is cheap.
- **Commit messages**: concise, imperative, squash-ready. **No
  `Co-Authored-By` trailers** (`AGENTS.md`).

### Before editing

- Read the affected specification. Update it first if the target behavior
  changes.
- Preserve unrelated work in a dirty tree.

## Document

### Specification

- [`README.md`](../../README.md) is the specification today; a Feature's
  specification goes in `features/<slug>/.spec/README.md` when one is authored.
  Claim ids are `B-00n`, and each section has exactly one owning role — see
  [`spec-and-traceability`](../spec-and-traceability/SKILL.md). Write only the
  sections your role owns.
- Every relative link in tracked markdown resolves. Moving a file rewrites
  every reference to it in the same commit.
- **Size the documentation to the change.** Update only what the change
  actually alters. A one-line change does not touch a dozen files.

### Scenarios

- Every user-facing requirement has a scenario in the Feature's `.feature`
  file, tagged with the `@B-00n` claim it proves. A change that adds or changes
  behavior adds or updates it **in the same pull request** (`AGENTS.md`).
- **Scenarios are documentation; the xUnit tests execute.** There is no Gherkin
  runner here, so a scenario alone never satisfies a claim — the § 9 row does,
  and it points at a test.
- Write the scenario first; when the implementation is wrong, correct the
  scenario, not the conversation.
- The PR body names what changed upstream — scenario, specification section,
  boundary — because it becomes the squash commit message.
- A claim may merge ahead of its implementation: its § 3 `Status` says so and
  its § 9 row reads `Missing`, with the `.issues/` item that will build it. That
  item does not go `done` while the row is still `Missing`. An obsolete scenario
  is deleted, not parked.
- The specification records what **not** to build. A change that draws a new
  boundary writes it there, not only in the pull request.

### Exemptions from scenario coverage

- Refactoring that preserves behavior may skip new scenarios **only by citing
  the specific claim IDs it leaves standing** (`AGENTS.md`). The exemption
  cannot be claimed because scenario-writing feels slow.
- **Nothing automates this check in this repository.** There is no coverage
  gate and no pull request template to pick a category from, so the exemption
  is an honesty rule enforced by review — say which claims survive, in the PR
  body, and let the reviewer check them.

### Lessons

- A bug fix that reveals a specification gap writes a lesson in the same pull
  request: symptom, root cause, spec delta, and the claim that now proves it
  (`AGENTS.md`). A fix that reveals nothing writes none. Lessons live in the
  Feature's `.spec/lessons/`.
- **Product lesson**: its remedy is a claim and a scenario. No skill change —
  restating product behavior in a skill creates a second place to drift from.
- **Process lesson** (tooling, build, conventions, how agents work): also
  update the skill that would have prevented it, in the same pull request, and
  name it in the lesson. The skill holds the general rule; the lesson keeps the
  incident. Agents read skills, not the lessons index.

### ADRs

- One ADR per real architecture decision, in the same pull request. A real
  decision is a new rule about the system, a reversed one, or a technology
  choice with a rejected alternative. UI polish and routine details need none.
- **Which `adr/`** depends on blast radius: `.spec/adr/` at the root for a
  decision that binds every Feature, a Feature's own `.spec/adr/` for one
  scoped to it. [`spec-and-traceability`](../spec-and-traceability/SKILL.md)
  has the rule; [ADR-0001](../../.spec/adr/0001-flurl-for-http.md) is the
  worked example, and `.spec/templates/adr.md` the blank.
- The technology list in README § "Technology Decisions" is a list, not a
  record — an entry there that carries a rejected alternative owes an ADR too.
- **Not an ADR**: a process or tooling rule is a convention, edited in place in
  the skill that owns it; interface detail is a scenario.
- **An accepted ADR is immutable.** Only its status changes. A change to the
  decision is a **new** ADR; the old one becomes `superseded` with a link.
  Never append an amendment, rewrite the body, or delete a record — a typo in
  an accepted ADR stays.
- **Statuses**: `proposed`, `accepted`, `rejected`, `deprecated`, `superseded`.
  An accepted record moves only to `superseded` or `deprecated`, linking its
  replacement. There is no partial supersession.
- Number it after rebasing. Keep the filename and the heading in step.
- Keep rationale and requirements apart: never restate acceptance criteria in
  an ADR, and never argue a technology choice in a `.feature` file.
- An ADR that changes a technology choice or how the system is broken up
  updates [`README.md`](../../README.md) in the same pull request — the fact
  and the link, not the rationale.

### Markdown and agent files

- Every tracked markdown file opens with YAML frontmatter (`title`,
  `description`, `type`) — `type` being one of `adr`, `spec`, `guide`,
  `decision`, `readme`, `lesson`, `instructions`, `template` (`AGENTS.md`).
  The blanks for each live in
  [`.spec/templates/`](../../.spec/templates/) — copy one rather than
  reconstructing its shape.
- **Skills and agents are the exception**: `name` and `description` only, with
  the type derived from the path. No `permalink`, no `metadata` block — a
  skill carrying either was copied from another repository.
- `.skills/` and `.agents/` files are edited directly here; there is no
  install manifest or generated copy in this repository.
- Diagrams are Mermaid. Test and example data is synthetic; never real user
  content.

## Keep the item true while you work

The item is the first hop of the specification chain: **correct the artifact,
not the chat.** A decision made after pickup is not recorded until it is in the
item. The next agent, the reviewer, and the squash commit read the item, not
this conversation.

- **Edit the item as soon as a decision lands, before building on it.** Keep
  the original need readable, and add or update **Decisions** (each decision,
  who made it, the date, the rejected option where there was one),
  **Acceptance criteria**, and **Out of scope**, bumping `updated:`. Strike
  text the decisions made false.
- **A decision that changes behavior belongs in the specification too** — a
  § 3 claim is `spec-author`'s to write
  ([`spec-and-traceability`](../spec-and-traceability/SKILL.md)). The item
  records that the call was made; the spec records what is now true.
- **File a new item** when added scope could ship on its own: independently
  deliverable, in a different area, or roughly doubling the pull request. Set
  `depends_on` or `parent` as the relation rules say, and link it from the
  original's Decisions. Scope that only makes the original need work stays.
- **Scope shrank?** Say so in the item, and file what was dropped if it is
  still wanted.
- **Re-read the item before opening the pull request.** Its acceptance
  criteria, the claims, and the PR body describe the same change; if not, fix
  the item or the specification first.

## Verify and publish

1. **Build and test.** `./build.sh` (`Clean → Restore → Format → Compile`), and
   run the tests for the code you changed. **The build has no `Test` target
   yet** ([`nuke-build`](../nuke-build/SKILL.md)), so a green build is not a
   green test run — run `dotnet test test/UnitTests` yourself and say what it
   said. `Format` runs with `ProceedAfterFailure`, so read its output rather
   than trusting the exit code. **Run it locally even for a documentation-only
   change**: CI skips markdown-only pull requests
   ([`nuke-build`](../nuke-build/SKILL.md)), so the local run is the only run.
2. **Inspect** `git diff --check`, every relative link, generated artifacts,
   and `git status`. No `obj/`, `bin/`, or hand-edited generated file in the
   diff.
3. **Commit the last unit** — imperative message, no co-author trailer — rebase
   onto fresh `origin/main`, and push. Never open a pull request from a branch
   behind `origin/main`.
4. **Open the pull request** with a squash-ready title, and set the item to
   `status: in-review`.
    - **Never merge directly, bypass protections, or enqueue explicitly.**
      Someone with the authority to merge does that by hand.
    - Keep working until the required checks are green.
5. **PR body**: the item id on its own line (`Delivers .issues/0001` — there is
   no issue for `Closes` to close), the `B-00n` claims it satisfies, and what
   changed upstream. Built something the specification does not describe?
   Either fix the specification or the change exceeded its scope.
6. **Show the UI change.** For a visible change to the MAUI app, capture the
   running app — not a mockup — and commit the image, referenced from the body
   by a `raw.githubusercontent.com` URL pinned to the adding commit (a relative
   path or `blob/` URL renders broken). A change with nothing visible says so
   in the body instead.
7. **Prove it runs.** Launch the app from the worktree and confirm it starts
   and shows data — against a replay source, never against a live provider, so
   the check costs no credits ([`api-mock`](../api-mock/SKILL.md)).
8. **Tear down at once**: stop what step 7 started, then `git worktree remove`.
   Never leave a worktree behind — open, failing, or merged. Watch checks and
   read logs from the primary checkout via `gh`.
9. **Watch required checks** from the primary checkout. CI runs `./build.cmd`
   on pull requests to `main`
   ([`.github/workflows/ci.yml`](../../.github/workflows/ci.yml)).
    - A check fails: recreate the worktree on the same branch (no `-b`), fix,
      commit, rebase, push, and repeat steps 7 and 8.
    - Green: confirm `gh pr view <pr> --json mergeStateStatus` is not
      `BEHIND` — `main` moves while checks run. Behind? Rebase, push, watch
      again.
    - **A markdown-only pull request shows no check at all**, because the
      workflow's path filter skips it and GitHub reports a skipped job as
      passing. That is intended; confirm the skip is why it is green rather
      than assuming a run happened.
    - Finish only when checks are green (or legitimately skipped) on a current
      branch and no worktree remains.
10. **Close the item**: `status: done`, a `closed:` date, `updated:` bumped.
    The file stays — it is the history of the work. If the § 9 matrix still has
    a `Missing` row for a claim this item owns, it is not done.

## CI traps worth knowing

- **A path filter lists every input a job reads**, not only the directory its
  code lives in. A missing input skips the job, and GitHub counts a skipped job
  as passing. This repository deliberately uses that to skip markdown-only
  changes ([`nuke-build`](../nuke-build/SKILL.md)) — so when adding a path to
  the exclusion list, ask whether a code change could ever match it. If it
  could, the filter will hide a broken build behind a green tick.
- **A job that calls an API authenticates.** An anonymous quota is shared with
  whoever else is on the runner's address, so a required check that spends it
  fails at random. This repository's CI calls no provider — and must not start:
  no test or build step reaches OpenSky or AISStream
  ([`api-mock`](../api-mock/SKILL.md)).
- **Put a concurrency group on the job that must not overlap**, not on the
  workflow. A workflow-level group is held by every job in the run, and a run
  left waiting cancels the ones after it — along with the checks they would
  have reported.
