# Starting a change

Which way in, claiming the item, settling the requirement, filing one, the worktree and branch, and what to read before editing.

## Start

**Which way in depends on what this is.**

- **A new Feature starts with its specification**, authored with no work item —
  the specification is the agreement, and items are cut from its claims
  afterwards ([`spec-and-traceability`](../../spec-and-traceability/SKILL.md)).
  Skip to "Author a specification".
- **Everything else starts with an item**: a bug, a spike, a chore, or
  delivering a claim a specification already carries.

### Author a specification

- No item, so nothing to claim and nothing to label. The branch is named for
  the Feature, and the rest of this skill's worktree, commit, rebase and publish
  rules apply unchanged.
- Copy the project's Feature template and write the agreement sections. The
  role that owns them is named in
  [`spec-and-traceability`](../../spec-and-traceability/SKILL.md).
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
  [`clarify-requirements`](../../clarify-requirements/SKILL.md) draws the line
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
