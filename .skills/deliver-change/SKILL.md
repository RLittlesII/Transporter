---
name: deliver-change
description: Deliver a change through an issue, worktree, specification, build, and pull request. Use when picking up work, filing an issue, committing, or opening and watching a PR.
---

# Deliver a change

**Where this repository stands.** There is no GitHub remote configured yet
(`AGENTS.md`). Everything below about issues, pull requests and checks applies
**from the first push onward**; until then, the parts that still hold are the
specification discipline, the commit and rebase rules, and the build gate. Do
not invent a tracker to satisfy a step.

The adjacent skills carry the detail this one refers to:
[`spec-and-traceability`](../spec-and-traceability/SKILL.md) for where a
specification lives, [`coding-conventions`](../coding-conventions/SKILL.md),
[`test-from-scenarios`](../test-from-scenarios/SKILL.md), and
[`nuke-build`](../nuke-build/SKILL.md) for the build.

## Start

### Claim the issue

- Work from one focused issue.
- **Never pick up an issue labelled `in progress`** — another agent has claimed
  it, even if it looks stalled or is the obvious next piece. Find work with
  `gh issue list --state open --search '-label:"in progress"'`. Asked for a
  labelled issue by number? Stop and ask the person.
- **Label it `in progress` the moment you pick it up, before anything else** —
  before the worktree, branch, or first edit:
  `gh issue edit <number> --add-label "in progress"`.
    - No exceptions: not for a one-line fix, an issue you just filed, or a
      resumed session. The label is the only thing telling the next agent it is
      taken.
    - Stop without a pull request? Remove it.
    - It stays while a pull request is open; closing the issue takes it off the
      board.

### Settle the requirement before building

- **Put every open question to the person, and record the answers in the issue,
  before writing any code.** A gap, an ambiguity, or a choice between two
  plausible readings is a question, not a decision for you to make — though in
  a demo repository where little is adopted, most implementation shapes *are*
  yours to choose. [`clarify-requirements`](../clarify-requirements/SKILL.md)
  draws that line.
- **A sub-agent brief says to stop and ask.** Any brief handed to another agent
  tells it to report a question rather than build on a guess, and the briefing
  agent has put its own open questions to the person first.
- **Sequence dependent issues.** Issues that touch the same seam — the source
  interface, the domain base class, the cache wiring — run one after another,
  never side by side. Record the order as a blocked-by relation when the issues
  are filed.

### File a new issue

**Milestone and labels are mandatory.** Pass both on `gh issue create`, never
as a follow-up:

- **Milestone**: exactly one open milestone
  (`gh api repos/{owner}/{repo}/milestones --jq '.[].title'`). An issue carved
  out of a parent takes the parent's milestone.
- **Labels**: every relevant one from `gh label list` — one type, plus every
  area and phase label the change matches.
- **Verify** with `gh issue view <number> --json milestone,labels` before
  linking or picking the issue up.

#### Relationships

**Wire relationships when the issue is created, not later.** Check every new
issue against open issues and against the others filed in the same pass, and
set each relationship that genuinely exists with GitHub's native relations
(`gh api graphql`; there is no `blocked` label). A link known at filing and
left unset is lost.

Filing several at once — plan the graph before creating any: which is the
parent, which are carved out of it, which must land first, which only relate.
Create the parent, then the children, then wire every relation in one pass.

The relations, strongest first. Use the strongest that is true, never a
stronger one:

- **Blocked by** — a hard prerequisite only: it cannot be implemented or
  verified until another issue lands. `addBlockedBy`, plus
  `Blocked by #N — <why>` in the body's first paragraph. No relation for
  "touches similar code".
- **Sub-issue** — only when the new issue is carved out of a larger one being
  split, so the parent's scope shrinks (`addSubIssue`). An independently scoped
  prerequisite is `blocked by`; a false hierarchy skews the completion rollup.
- **Duplicate** — never file a second issue for the same scope. Extend the
  existing one, or mark the new one `duplicateOf`.
- **Relates to** — a soft link: a follow-up, a sibling, the issue whose work
  surfaced this one. No API; set it in the sidebar.
- Remove a stale relation (`removeBlockedBy`) when a design change kills the
  dependency.

### Worktree and branch

- **Never work on a branch in the primary checkout.** Create a worktree off
  fresh `origin/main`:
  `git fetch origin main && git worktree add -b issue-<number>/<short-description> .claude/worktrees/issue-<number>/<short-description> origin/main`.
  Several agents share the repository; a worktree per issue means none switches
  a branch out from under another.
- **Keep the primary checkout's `main` current, and change nothing in it.**
  `git -C <primary checkout> pull --ff-only origin main`, and that
  fast-forward is the only thing ever done there: no edits, commits, resets,
  merges or generator runs. If a pull would not fast-forward, stop and tell the
  person rather than repairing it.
- **Check at the first edit, every time.** Before the first `Edit` or `Write`
  for an issue, run `git branch --show-current`. If it says `main`, stop and
  create the worktree. A resumed session or a plain "continue" does not look
  like starting an issue, which is exactly when this gets skipped.
- **Push the branch at once**, from the worktree:
  `git push -u origin issue-<number>/<short-description>`.
- Open every final report with `[#<number> · PR #<pr>]` (just `[#<number>]`
  before the pull request exists), even a one-line report.
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
- **Claim a shared identifier after that rebase** — an ADR number, a claim ID,
  a slug. It is claimed the moment someone else merges it. Lost the race? Take
  the new number and rewrite every reference; renaming is cheap.
- **Commit messages**: concise, imperative, squash-ready. **No
  `Co-Authored-By` trailers** (`AGENTS.md`).

### Before editing

- Read the affected specification. Update it first if the target behavior
  changes.
- Preserve unrelated work in a dirty tree.

## Document

### Specification

- [`README.md`](../../README.md) is the specification today; a Feature's
  specification goes in its `.spec/README.md` when one is authored. Claim IDs
  are `B-00n`. See
  [`spec-and-traceability`](../spec-and-traceability/SKILL.md) — including the
  `AGENTS.md` drift it resolves.
- Every relative link in tracked markdown resolves. Moving a file rewrites
  every reference to it in the same commit.
- **Size the documentation to the change.** Update only what the change
  actually alters. A one-line change does not touch a dozen files.

### Scenarios

- Every user-facing requirement has a scenario in the Feature's `.feature`
  file, tagged with the claim it proves. A change that adds or changes behavior
  adds or updates it **in the same pull request** (`AGENTS.md`).
- Write the scenario first; when the implementation is wrong, correct the
  scenario, not the conversation.
- The PR body names what changed upstream — scenario, specification section,
  boundary — because it becomes the squash commit message.
- A scenario may merge ahead of its implementation, marked not built and naming
  the open issue that will build it; that issue does not close while the
  scenario is unbuilt. An obsolete scenario is deleted, not parked behind
  `@ignore`.
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

- One ADR per real architecture decision, in the Feature's `.spec/adr/`, in the
  same pull request. A real decision is a new rule about the system, a reversed
  one, or a technology choice with a rejected alternative. UI polish and
  routine details need none.
- **This repository has no ADRs yet.** The technology list in README §
  "Technology Decisions" is a list, not a record, and no skill links to an ADR
  that does not exist. The open decisions worth a record are enumerated in
  [`spec-and-traceability`](../spec-and-traceability/SKILL.md).
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
  `readme`, `lesson`, `instructions`, `template` (`AGENTS.md`).
- **Skills and agents are the exception**: `name` and `description` only, with
  the type derived from the path. No `permalink`, no `metadata` block — a
  skill carrying either was copied from another repository.
- `.skills/` and `.agents/` files are edited directly here; there is no
  install manifest or generated copy in this repository.
- Diagrams are Mermaid. Test and example data is synthetic; never real user
  content.

## Keep the issue true while you work

The issue is the first hop of the specification chain: **correct the artifact,
not the chat.** A decision made after pickup is not recorded until it is in the
issue. The next agent, the reviewer, and the squash commit read the issue, not
this conversation.

- **Edit the issue as soon as a decision lands, before building on it.** Keep
  the original need readable, and add or update **Decisions** (each decision,
  who made it, the date, the rejected option where there was one),
  **Acceptance criteria**, and **Out of scope**. Strike text the decisions made
  false.
- **Open a new issue** when added scope could ship on its own: independently
  deliverable, in a different area, or roughly doubling the pull request. Wire
  it with a native relation and link it from the original's Decisions. Scope
  that only makes the original need work stays.
- **Scope shrank?** Say so in the issue, and file what was dropped if it is
  still wanted.
- **Re-read the issue before opening the pull request.** Its acceptance
  criteria, the scenarios, and the PR body describe the same change; if not,
  fix the issue or the specification first.

## Verify and publish

1. **Build and test.** `./build.sh` (`Clean → Restore → Format → Compile`), and
   run the tests for the code you changed. **The build has no `Test` target
   yet** ([`nuke-build`](../nuke-build/SKILL.md)), so a green build is not a
   green test run — run `dotnet test test/UnitTests` yourself and say what it
   said. `Format` runs with `ProceedAfterFailure`, so read its output rather
   than trusting the exit code.
2. **Inspect** `git diff --check`, every relative link, generated artifacts,
   and `git status`. No `obj/`, `bin/`, or hand-edited generated file in the
   diff.
3. **Commit the last unit** — imperative message, no co-author trailer — rebase
   onto fresh `origin/main`, and push. Never open a pull request from a branch
   behind `origin/main`.
4. **Open the pull request** with a squash-ready title.
    - **Never merge directly, bypass protections, or enqueue explicitly.**
      Someone with the authority to merge does that by hand.
    - Keep working until the required checks are green.
5. **PR body**: `Closes #<number>` on its own line (`AGENTS.md`), plus the
   scenarios it satisfies and what changed upstream. Built something the
   specification does not describe? Either fix the specification or the change
   exceeded its scope.
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
    - Finish only when checks are green on a current branch and no worktree
      remains.

## CI traps worth knowing

- **A path filter lists every input a job reads**, not only the directory its
  code lives in. A missing input skips the job, and GitHub counts a skipped job
  as passing. Changing an input a skipped job would have read means running
  that suite locally before the pull request.
- **A job that calls an API authenticates.** An anonymous quota is shared with
  whoever else is on the runner's address, so a required check that spends it
  fails at random. This repository's CI calls no provider — and must not start:
  no test or build step reaches OpenSky or AISStream
  ([`api-mock`](../api-mock/SKILL.md)).
- **Put a concurrency group on the job that must not overlap**, not on the
  workflow. A workflow-level group is held by every job in the run, and a run
  left waiting cancels the ones after it — along with the checks they would
  have reported.
