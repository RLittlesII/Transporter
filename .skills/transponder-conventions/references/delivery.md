# Delivering a change

The local tracker, branch and worktree shapes, how to verify and publish.

## The tracker

**Work is tracked locally.** A `<id>-<slug>.yml` item stands in for a GitHub
issue, and there are **no issues, labels or milestones** in this workflow. Code
still pushes to GitHub, so branches, pull requests and CI stay. The schema and
the status enum are below under `spec-and-traceability`.

**An item sits beside the specification it was cut from**:
`features/<slug>/.issue/`, a sibling of that Feature's `.spec/`. An item that
belongs to no Feature — a bug, a spike, a chore — goes in the repository-root
`.issue/`, the same blast-radius split `.spec/adr/` and `.spec/lessons/` use.
**Ids stay repository-wide** from one `.issue/.sequence`, so a bare id in
`depends_on` or `parent` resolves from any Feature; find one with
`**/.issue/<id>-*.yml`.

- `status: in-progress` is the mark that an item is taken, and `in-review` once
  the pull request opens. Bump `updated:` on every edit.
- **`done` and the `closed:` date are written by the pull request that delivers
  the item, before it is opened.** Not after the merge: a squash merge ends the
  branch, the worktree comes down, and nobody is present to flip a field — so
  "`done` when it lands" is a step with no owner, and the item sits at
  `in-review` forever while its work is on the trunk. Date it the day the pull
  request opens; that is the last moment anyone is there to know. A reviewer who
  sends the work back changes the status with the rest of the change
  ([lesson 0010](../../../.spec/lessons/0010-a-status-nobody-is-present-to-change-does-not-change.md)).
  **A pull request closes every item its work finishes**, not only the one it
  was cut for: a parent whose last child lands, and a sibling held `blocked` on
  a claim this work proves, are both `done` in the same change.
- **An item does not move to `in-progress` while its specification's § 12 rows
  are 🟡.** Unsigned design sections mean there is nothing agreed to implement
  against, and the next reader cannot tell which parts were decided and which
  were invented on the way. The person may waive it for a **named item** — not
  for a Feature, and not standing — and the waiver is a row in that item's
  `decisions`. What `approved` requires is
  [`feature.md`](../../../.spec/templates/feature.md) § 12, and a `Missing` row in
  § 9 is not part of it: that is the ship gate, and it blocks `done`
  ([lesson 0007](../../../.spec/lessons/0007-a-gate-that-waits-on-what-it-gates-never-closes.md)).
- **Before reporting that an item cannot close, perform the reviews its rows
  name.** A review is a mechanism beside a test and a diagnostic, for a claim
  constraining code the repository does not contain — there is no value to
  compute and no declaration to analyze — and it is performed by a reader, not
  by a run. A `Missing` row naming one is a review nobody has done; a performed
  one reads `Verified` and records in the row **what was looked at, on which
  item, and what change re-does it**, the form `boundary-analyzer` § 9 uses. Two
  rows held `0002` for a day as rows whose status could never move, and one of
  them took a `git log` and a diff. Where a clause of the review names something
  the repository does not contain, the row belongs to the item that builds that
  thing — the same move a claim makes when its subject is another item's
  ([lesson 0011](../../../.spec/lessons/0011-a-review-nobody-performed-is-not-a-review-nobody-can-perform.md)).
  Adding a § 9 status is not the remedy: every table parser in the test project
  reads that vocabulary.
- **One item `in-progress` at a time, per Feature.** Two at once is the
  exception and says so in the item's `decisions`. An item that cannot be
  finished, demonstrated and reviewed without a sibling is not a small item; it
  is a decomposition defect, and the remedy is to merge it with the sibling or
  re-cut both — not to carry both
  ([lesson 0008](../../../.spec/lessons/0008-an-item-that-cannot-be-worked-alone-is-a-decomposition-defect.md)).
- A pull request body says `Delivers <id>` — the bare id, since one id has one
  item wherever it lives — or `Specifies features/<slug>` when authoring a
  specification. There is no issue for `Closes` to close.

## Worktree and branch

- Branch: `<id>/<short-description>` from the item id, or
  `spec/<feature-slug>` when authoring a specification, which has no item to
  take an id from.
- Worktree path: `.claude/worktrees/<branch>`.
- Report prefix: `[<id> · PR #<pr>]`, or `[<id>]` before the pull request
  exists; a specification uses the Feature slug in place of an id.

## Commit and pull request messages

What a message must contain is
[`deliver-change`](../../deliver-change/references/publish.md) step 3. This is
how it is spelled here.

```
<type>(<item id | feature-slug>): <what is now true>

Delivers <id>            # or: Specifies features/<slug>; omitted when neither applies

<a paragraph per decision>

<how it was verified, and what is still not true>
```

- **`feature`, spelled out.** Every item commit on `main` reads
  `feature(0023):`, `feature(0005):` — not `feat:`. `docs` is a specification,
  an ADR, a lesson or an item edit; `chore` is the tracker, a hook or build
  wiring; `refactor`, `fix` and `test` as usual.
- **The scope names what the commit belongs to**, in one of two spellings.
  **An item** is its four-digit id — repository-wide, so `feature(0007):`
  resolves from any Feature, and work that delivers an item always has one to
  cite. **A Feature** is its slug, for a change that belongs to a Feature and to
  no item: `docs(fleet-pipeline): answer every open question …`, which is what
  specification authoring is. The slug is not checked against a directory,
  because a Feature's specification need not live under `features/` —
  `aircraft-source`'s is under its integration. A change that belongs to
  **neither** carries no scope at all: `chore: gate the commit subject with a
commit-msg hook`, a repository-wide convention with no Feature and no item
  behind it.
- **The subject names the outcome, not the activity.** "report a declaration
  that is the wrong shape, on the declaration" says what the build now does;
  "add seven analyzer rules" says what the author did. Lowercase after the
  colon, no trailing period.
- **The pull request title is the trunk's commit subject**, because the squash
  merge appends `(#N)` to it and discards the branch. So it follows every rule
  above, and the branch's own subjects are read only in the squashed body.
- **The body opens with the item line when there is an item** — `Delivers <id>`,
  or `Specifies features/<slug>` when authoring a specification. Same line the
  pull request body opens with, above. A repository-wide chore cites neither and
  opens with its first paragraph: there is nothing for a reader to resolve.
- **A decision is a paragraph, not a bullet.** It names the claim ids and the
  sections it moves, and the option rejected where there was one — the same
  content as the item's `decisions` row, which is what the squash commit is read
  beside. Reserve a list for a set of independent mechanical edits; the file
  list itself is in the diff.
- **The last paragraph says how it was verified and what is still not true**:
  which § 9 rows stay `Missing`, which half of a claim another item owns, and
  what the ship gate therefore still blocks.
- **No co-author trailer, no generated-by line, no emoji.** Nothing on `main`
  carries one.
- **A `commit-msg` hook checks the subject**: the type vocabulary, the scope's
  shape in either spelling, the lowercase start, a trailing period, and the two
  trailers. Which spelling is the right one is this rule's business, not the
  hook's — a pattern cannot tell whether the change has an item. It skips what git composes itself — a merge, a revert, and
  `fixup!`/`squash!`/`amend!` — because rejecting those would break `git merge`,
  `git revert` and every interactive rebase. It does **not** read the body: a
  paragraph per decision, the item line and the verification paragraph are
  content a pattern cannot judge, and they are reviewed rather than gated. Nor
  does it check for emoji — BSD `grep` has no `-P`, and the character-class
  alternatives reject the § and the em dash every specification cite here uses.
  Replayed over `main`, it accepts 102 of 104 subjects; the two it rejects are
  the bootstrap commits `nuke: build successful` and
  `init: add initial transponder application`, whose types the convention no
  longer uses.

## Verify and publish

Under the method skill's step numbers:

1. `./build.sh` (`./build.cmd` on Windows, and what CI invokes), then
   `dotnet test test/UnitTests` for what you changed. Read
   [`nuke-build`](../../nuke-build/SKILL.md) for what `Default` covers before
   treating a green build as a green test run, and note that `Format` proceeds
   after failure, so read its output rather than the exit code. CI skips a
   markdown-only pull request, so run it locally for a documentation change too.
2. The user-visible surface is the MAUI app ([`maui-ui`](../../maui-ui/SKILL.md)).
3. Launch against a recorded or simulated source
   ([`api-contract`](../../api-contract/SKILL.md)), never a live provider.
4. CI runs `./build.cmd` on pull requests to `main`. A markdown-only pull
   request shows no check at all, by design — see
   [`nuke-build`](../../nuke-build/SKILL.md).

## Exemptions from scenario coverage

**Nothing automates this check here.** There is no coverage gate and no pull
request template to pick a category from, so the exemption is an honesty rule
enforced by review: say which claim ids survive, in the pull request body.

---
