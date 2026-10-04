---
name: deliver-change
description: Deliver a change through a work item, a worktree, the specification, the build, and a pull request. Use when picking up work, filing an item, committing, or opening and watching a pull request.
---

# Deliver a change

**Project rules.** A project may extend this skill with a companion skill that
names this one; its agent instructions (`AGENTS.md`) list it. Read both. The
companion holds the tracker, the item schema, the commands, the branch and
report shapes, and the paths for each step here, and wins where they differ.

## Start

**Which way in depends on what this is.**

- **A new Feature starts with its specification**, authored with no work item —
  the specification is the agreement, and items are cut from its claims
  afterwards ([`spec-and-traceability`](../spec-and-traceability/SKILL.md)).
  Skip to "Author a specification".
- **Everything else starts with an item**: a bug, a spike, a chore, or
  delivering a claim a specification already carries.

### Author a specification

- No item, so nothing to claim and nothing to label. The branch is named for
  the Feature, and the rest of this skill's worktree, commit, rebase and publish
  rules apply unchanged.
- Copy the project's Feature template and write the agreement sections. The
  role that owns them is named in
  [`spec-and-traceability`](../spec-and-traceability/SKILL.md).
- The pull request body cites the **Feature and the claim ids it introduces**,
  since there is no item id to name.
- Items come next, each citing the claims it delivers — "File a new item".

### Claim the item

- Work from one focused item.
- **Never pick up an item already marked in progress** — someone has claimed
  it, even if it looks stalled or is the obvious next piece. Find work that is
  ready with every prerequisite already done. Asked for an in-progress item by
  id? Stop and ask the person.
- **Mark it in progress the moment you pick it up, before anything else** —
  before the worktree, the branch, or the first edit.
    - No exceptions: not for a one-line fix, an item you just filed, or a
      resumed session. That mark is the only thing telling the next agent it is
      taken.
    - Stop without a pull request? Put it back.
    - It moves to in-review when the pull request opens, and to done when the
      work lands. **A done item stays** — the record is permanent history.

### Settle the requirement before building

- **Put every open question to the person, and record the answers in the item,
  before writing any code.** A gap, an ambiguity, or a choice between two
  plausible readings is a question, not a decision for you to make.
  [`clarify-requirements`](../clarify-requirements/SKILL.md) draws the line
  between what to ask and what to decide. Code built on a guess is thrown away
  when the guess is wrong.
- **A sub-agent brief says to stop and ask.** Any brief handed to another agent
  tells it to report a question rather than build on a guess, and the briefing
  agent has put its own open questions to the person first.
- **Sequence dependent items.** Items touching the same seam run one after
  another, never side by side: the second starts from the first's merged result.
  Record the order as a dependency when the items are filed, not when the
  conflict shows up.

### File a new item

Write the item from the project's item template, with every required field; the
template is the schema, so copy it rather than re-deriving it from an example.
Claim the id after rebasing (see "Commit, rebase, claim identifiers").

**A feature item is cut from a specification that already exists**: it names
that specification and the claims it delivers, and those claim ids are already
written there. An item that would need a claim invented for it is not ready to
be filed — the specification changes first. A bug, spike or chore names no
specification.

Give it a summary that is user-story shaped for a feature, and acceptance
criteria that **cite claim ids** rather than restating them.

#### Relationships

**Wire relationships when the item is created, not later.** Check every new item
against the open ones and against the others filed in the same pass. A link
known at filing and left unset is lost.

Filing several at once — plan the graph before writing any: which is the parent,
which are carved out of it, which must land first. Write the parent first, then
the children, then set every relation in one pass.

The relations, strongest first. **Use the strongest that is true, never a
stronger one** — that judgment is the whole point:

- **Depends on** — a hard prerequisite only: this cannot be implemented or
  verified until that one lands. Say why in the summary. No entry for "touches
  similar code"; that is no relation at all.
- **Parent and child** — only when the item is carved out of a larger one being
  split, so the parent's scope shrinks. An independently scoped prerequisite is
  a dependency; a false hierarchy makes the rollup lie.
- **Never two items for the same scope.** Extend the existing one.
- **A derived relation is recomputed, never hand-authored.** The inverse of a
  dependency is not a claim of its own.
- Remove a stale dependency when a design change kills it, and recompute what it
  implied.

### Worktree and branch

- **Never work on a branch in the primary checkout.** Create a worktree from the
  fresh tip of the trunk, named for the item. Several agents share the
  repository; a worktree per item means none switches a branch out from under
  another.
- **Keep the primary checkout's trunk current, and change nothing in it.**
  Fast-forward it, and that fast-forward is the only thing ever done there: no
  edits, commits, resets, merges or generator runs. If it would not
  fast-forward, stop and tell the person rather than repairing it.
- **Check at the first edit, every time.** Before the first edit for an item,
  ask git which branch you are on. If it is the trunk, stop and create the
  worktree. A resumed session or a plain "continue" does not look like starting
  an item, which is exactly when this gets skipped. Having done it right on an
  earlier item proves nothing.
- **Push the branch at once**, from the worktree, so the work is visible and
  survives the worktree.
- Open every final report with the item and pull request ids, in the shape the
  companion gives, even a one-line report.
- Do all work in the worktree. It comes down once the pull request is open.
- **Never use a bare `git stash` or `git stash pop` in a worktree.** There is
  one stash per clone, shared by every worktree and session: a bare stash takes
  another session's files, and a pop applies whatever is on top.
    - Park work in a WIP commit, and amend or reset it later.
    - A stash that cannot be a commit is tagged and applied by SHA:
      `git stash push -m <tag>`, note `git rev-parse stash@{0}`, then
      `git stash apply <sha>`. Never `pop`, `drop`, or `clear`.
    - Nothing enforces this; git has no pre-stash hook. It is a written rule,
      and agents and people follow it by reading it.

### Commit, rebase, claim identifiers

- **Commit and push each unit of work as soon as it is done** — a scenario, a
  passing test, a document. Never hold work for the pull request; local-only
  work is invisible to others and lost with the worktree.
- **Rebase onto the fresh trunk before every commit**, not only before a push.
  Resolve conflicts locally; if the rebase brought commits in, re-run the
  affected checks before pushing. Rewrote commits already pushed? Force with
  lease, never plain force.
- **Claim a shared identifier after that rebase** — a sequence id, a decision
  record number, a claim id, a slug. It is claimed the moment someone else
  merges it. Lost the race? Take the new number and rewrite every reference;
  renaming is cheap.
- **Commit messages**: concise, imperative, squash-ready, and no co-author
  trailer.

### Before editing

- Read the affected specification. Update it first if the target behavior
  changes.
- Preserve unrelated work in a dirty tree.

## Document

### Specification

- **A specification is not documentation of the change; it is the thing the
  change serves.** It was written before this item existed, and it outlives the
  item. Amending it is editing an agreement, so the edit is deliberate and the
  pull request says what moved.
- Write only the sections your role owns
  ([`spec-and-traceability`](../spec-and-traceability/SKILL.md)).
- Every relative link in tracked markdown resolves. Moving a file rewrites every
  reference to it in the same commit.
- **Size the documentation to the change.** Update only what the change actually
  alters. A one-line change does not touch a dozen files.

### Scenarios

- Every user-facing requirement has a scenario, tagged with the claim it proves.
  A change that adds or changes behavior adds or updates it **in the same pull
  request**.
- Write the scenario first; when the implementation is wrong, correct the
  scenario, not the conversation.
- The pull request body names what changed upstream — scenario, specification
  section, boundary — because it becomes the squash commit message.
- A claim may merge ahead of its implementation, marked unbuilt and naming the
  item that will build it; that item does not go done while the claim is still
  unbuilt. An obsolete scenario is deleted, not parked — a repository that
  states something false is worse than one that is silent, and history keeps the
  text.
- The specification records what **not** to build. A change that draws a new
  boundary writes it there, not only in the pull request.

### Exemptions from scenario coverage

- Refactoring that preserves behavior may skip new scenarios **only by citing
  the specific claim ids it leaves standing**. The exemption cannot be claimed
  because scenario-writing feels slow.
- Cite claims of the areas the changed code actually serves. A whitespace,
  comment, or unrelated-area edit covers nothing.
- **Know whether anything checks this.** Where a coverage gate exists, run it
  locally rather than only in continuous integration; where none does, the
  exemption is an honesty rule enforced by review — say which claims survive, in
  the pull request body, and let the reviewer check them.

### Lessons

- A bug fix that reveals a specification gap writes a lesson in the same pull
  request: symptom, root cause, specification delta, and the claim that now
  proves it. A fix that reveals nothing writes none.
- **Every lesson declares its kind** — product, process, or incident — and owes
  what that kind owes.
- **Product lesson**: its remedy is a claim and a scenario; the delta names the
  claim. No skill change — restating product behavior in a skill creates a
  second place to drift from the specification.
- **Process lesson** (tooling, the build, conventions, delivery, how agents
  work): also update the skill or convention that would have prevented it, in
  the same pull request, and name it in the lesson. The skill holds the general
  rule; the lesson keeps the incident. Agents read skills, not the lessons
  index.
- **Incident**: something failed while the system was running. Symptom and root
  cause are what it owes. It may also name a skill it changed; never invent a
  rule just to give it one.
- A lesson keeps the project's sections and no others; extra detail is a
  subsection of one of them.

### Decision records

- One record per real architecture decision, in the same pull request. A real
  decision is a new rule about the system, a reversed one, a privacy or data
  boundary, or a technology choice with a rejected alternative. Interface polish
  and a routine detail with no alternative worth naming need none.
- **Not a decision record**: a process, tooling or agent-workflow rule is a
  convention, edited in place in the skill that owns it; interface detail is a
  scenario.
- **Write it from the project's template**: status, context, considered options,
  decision, consequences. Considered options is required; a decision with no
  alternative worth naming says so there.
- **Blast radius decides where it lives.** A decision binding every area goes in
  the repository-wide directory; one scoped to a single area goes in that area's.
  Lessons split the same way.
- **An accepted record is immutable.** Only its status changes, and a link's
  target when the file it points at moves.
    - A change to the decision is a **new** record. The old one becomes
      superseded, its status line linking the new one.
    - A new record that changes part of an older one still supersedes the whole
      record, and lists what of it still holds — by link or claim id — never
      restating it, so each rule keeps one source.
    - Never append an amendment, rewrite the body, or delete a record. A typo in
      an accepted record stays.
- **Statuses**: proposed, accepted, rejected, deprecated, superseded. An
  accepted record moves only to superseded or deprecated, linking the record
  that replaced or retired it; the other three are terminal. There is no partial
  supersession.
- Number it after rebasing. Keep the filename and the heading in step.
- Keep rationale and requirements apart: never restate acceptance criteria in a
  decision record, and never argue a technology choice in a scenario file.
- A record that changes a technology choice, or how the system is broken up,
  updates the root `README.md` in the same pull request — the fact and the link,
  not the rationale.

### Markdown and agent files

- Every tracked markdown file opens with the frontmatter the companion
  specifies. Copy the matching template rather than reconstructing its shape.
- **Skills and agent files are the exception**, declaring only what the
  companion says they do. A skill carrying a key from another repository's
  manifest was copied, not written.
- Diagrams are drawn in the project's one notation. Test and example data is
  synthetic; never real user content.

## Keep the item true while you work

The item is the first hop of the specification chain: **correct the artifact,
not the chat.** A decision made after pickup is not recorded until it is in the
item. The next agent, the reviewer, and the squash commit read the item, not
this conversation.

- **Edit the item as soon as a decision lands, before building on it.** Keep the
  original need readable, and add or update **decisions** (each decision, who
  made it, the date, the rejected option where there was one), **acceptance
  criteria**, and **out of scope**. Strike text the decisions made false.
- **A decision that changes behavior belongs in the specification too.** The
  item records that the call was made; the specification records what is now
  true, and a claim is the specification author's to write.
- **File a new item** when added scope could ship on its own: independently
  deliverable, in a different area or layer, or roughly doubling the pull
  request. Give it its need, decisions and acceptance criteria, wire it with a
  relation, and link it from the original's decisions. Scope that only makes the
  original need work stays.
- **Scope shrank?** Say so in the item, and file what was dropped if it is still
  wanted.
- **Re-read the item before opening the pull request.** Its acceptance criteria,
  the claims, and the pull request body describe the same change; if not, fix the
  item or the specification first.

## Verify and publish

The step numbers are stable; the companion adds its commands under the same
numbers.

1. **Build and test.** Run the project's build, then the tests for the code you
   changed — the focused subset, not the whole suite. **Know what the build does
   not cover**: a green build is not a green test run unless the build runs the
   tests, and a step that proceeds after failure has to be read rather than
   trusted. **Run it locally even for a documentation-only change**, where
   continuous integration skips such a change: the local run is then the only
   run.
2. **Inspect** the diff for whitespace errors, every relative link, generated
   artifacts, and the status output. No build output and no hand-edited
   generated file in the diff.
3. **Commit the last unit** — imperative message, no co-author trailer — rebase
   onto the fresh trunk, and push. Never open a pull request from a branch
   behind the trunk.
4. **Open the pull request** with a squash-ready title, and move the item to
   in-review.
    - **Never merge directly, bypass protections, or enqueue explicitly.**
      Someone with the authority to merge does that by hand.
    - Keep working until the required checks are green.
5. **Pull request body**: the item it delivers on its own line, the claims it
   satisfies, and what changed upstream. Built something the specification does
   not describe? Either fix the specification or the change exceeded its scope.
6. **Show a user-visible change.** Capture the running application — not a
   mockup — and commit the image, referenced from the body by a URL pinned to
   the adding commit; a relative path or a repository-browser URL renders broken.
   A change with nothing visible says so in the body instead.
7. **Prove it runs.** Launch the application from the worktree and confirm it
   starts and shows data, against a recorded or simulated source rather than a
   live provider, so the check costs nothing and cannot fail on someone else's
   network.
8. **Tear down at once**: stop what step 7 started, then remove the worktree.
   Processes belong to the checkout that started them; removing the worktree
   first leaves them holding the ports. Never leave a worktree behind — open,
   failing, or merged. Watch checks and read logs from the primary checkout.
9. **Watch required checks** from the primary checkout.
    - A check fails: recreate the worktree on the same branch, fix, commit,
      rebase, push, and repeat steps 7 and 8.
    - Green: confirm the branch is not behind the trunk — the trunk moves while
      checks run. Behind? Rebase, push, watch again.
    - **A change that every path filter excludes shows no check at all**, and a
      skipped job is reported as passing. Confirm the skip is why it is green
      rather than assuming a run happened.
    - Finish only when checks are green, or legitimately skipped, on a current
      branch and no worktree remains.
10. **Close the item**: done, with the date. The record stays — it is the
    history of the work. If a claim this item owns is still marked unbuilt, it is
    not done.

## Continuous-integration traps worth knowing

- **A path filter lists every input a job reads**, not only the directory its
  code lives in. A missing input skips the job, and a skipped job is counted as
  passing. Where a project uses that deliberately, ask before adding a path to
  the exclusion list whether a code change could ever match it. If it could, the
  filter will hide a broken build behind a green tick.
- **A job that calls an external service authenticates.** An anonymous quota is
  shared with whoever else is on the runner's address, so a required check that
  spends it fails at random. Better still, a check that reaches no external
  service cannot fail that way at all.
- **Put a concurrency group on the job that must not overlap**, not on the
  workflow. A workflow-level group is held by every job in the run, including one
  waiting on a reviewer, and a run left waiting cancels the ones after it —
  along with the checks they would have reported.
- **A workflow that pushes** replays its commit on top rather than racing with a
  bare push, and checks out without persisting credentials when it authenticates
  through the remote URL.
