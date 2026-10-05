# Verifying and publishing

Keeping the item true, the verify steps, and the continuous-integration traps.

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
