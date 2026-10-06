---
title: "Lesson 0018: A synchronous signature decides for every caller"
description: "The recorder's seam declared a void write over a file, so every caller was committed to blocking a thread before any implementation existed; review caught it, and the rule it rests on was nowhere written."
type: lesson
---

# Lesson 0018: A synchronous signature decides for every caller

## Symptom

`0009` shipped `IRecordingWriter.Write` returning `void` over what is, in
production, a `StreamWriter` on a file. The implementation wrote six times per
line and called `Flush()` on each one. Review asked one question —
"This is writing to disk should it make use of the TPL?" — and the answer was
yes, which meant the seam was wrong rather than the implementation.

Nothing failed. Every test passed, the build was green, and CI was green,
because a `StringWriter` in a test never blocks on anything.

## Root cause

**The seam decided for every caller, and it decided before any caller
existed.** A `void` member over real input or output leaves an implementation
no way to be asynchronous, so the one production caller — a poll already
running in an `async` method — had to block a thread it did not know it was
blocking. The asymmetry is what makes this worth a lesson: declaring the member
asynchronous costs nothing where there is nothing to await, because a null
object returns a completed task, while declaring it synchronous cannot be
undone from the far side.

The second half surfaced with it. Writing a line a field at a time is a line
another writer can split down the middle, and the whole-line write is what
makes interleaving impossible rather than merely unlikely. Neither defect could
show in a test: one needs a real file, the other needs two writers.

**No rule in this repository said either thing.** Three came close and none
reached it. [`coding-conventions`](../../.skills/coding-conventions/SKILL.md)
banned "a blocking wait on an asynchronous call" — but the block here was on a
_synchronous_ API, with no asynchronous call to wait on, no `.Result` and no
`.Wait()`. [`transponder-conventions`](../../.skills/transponder-conventions/references/coding.md)
covered the `Async` suffix — how to name an asynchronous method — without
saying when a method has to be one. And [`akka-actor`](../../.skills/akka-actor/SKILL.md)
forbade blocking inside `Receive`, which is actor-scoped and again about
sync-over-async.

**No analyzer could have caught it either, and the reason is the abstraction.**
`CA1849` reports synchronous I/O called from an asynchronous method, and the
call site was `_recorder.Write(...)` through `IRecordingWriter` — so all the
compiler could see was a `void` member on an interface. A sealed class calling
`StreamWriter.Write` inside an `async` method is the shape a diagnostic can
find; a seam that hides the file is not. `CA2007` was configured and had
nothing to say, because the method had no `await` to carry a
`ConfigureAwait`.

## Spec delta

None. No claim moved: B-001 through B-004 say what the line holds, what it
costs and what it must not perturb, and all four are as true of an awaited
write as of a blocking one. This was a design defect under the claims rather
than a gap in them.

## Rule

[`coding-conventions`](../../.skills/coding-conventions/SKILL.md) § Design
gained both halves:

- A member that touches a file, a socket or a database is asynchronous, and its
  seam says so.
- A record written a field at a time is a record another writer can split.

## What a reader should take

**Review found this, and nothing else could have.** The build cannot see it,
the tests cannot see it, and the analyzer has no rule for it — the only gate
was a person reading a signature and asking what it would do on a disk. That is
[lesson 0017](0017-a-written-convention-with-no-gate-is-a-preference.md)'s
shape once more, with the twist that here there was not even a convention to be
ungated: the rule did not exist until the review created it.

The same review's second comment was the opposite case. "AutoFixture?" pointed
at a subject built by a constructor call in the test, which
[`transponder-conventions`](../../.skills/transponder-conventions/references/testing.md)
already forbids in writing. One comment created a rule; the other enforced one
nothing enforces. Both arrived in the same pass, and only one needed a skill
edit.
