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

- `status: in-progress` is the mark that an item is taken; `in-review` once the
  pull request opens; `done` with a `closed:` date when it lands. Bump
  `updated:` on every edit.
- **An item does not move to `in-progress` while its specification's § 12 rows
  are 🟡.** Unsigned design sections mean there is nothing agreed to implement
  against, and the next reader cannot tell which parts were decided and which
  were invented on the way. The person may waive it for a **named item** — not
  for a Feature, and not standing — and the waiver is a row in that item's
  `decisions`. What `approved` requires is
  [`feature.md`](../../../.spec/templates/feature.md) § 12, and a `Missing` row in
  § 9 is not part of it: that is the ship gate, and it blocks `done`
  ([lesson 0007](../../../.spec/lessons/0007-a-gate-that-waits-on-what-it-gates-never-closes.md)).
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
