---
name: spec-and-traceability
description: The specification model — a specification starts before its work items, one record holds content and another holds delivery state, and every claim traces through a scenario to a test. Use when authoring, amending, or reviewing a specification, a claim, or a work item.
---

# Specification and traceability

**Project rules.** A project may extend this skill with a companion skill that
names this one; its agent instructions (`AGENTS.md`) list it. Read both. The
companion holds the paths, the section list, the claim-id and item-id schemes,
the templates, and the role that owns each section, and wins where they differ.

## Where a specification starts

**A Feature starts with its specification, not with a work item.** The
specification is the design authority, so it cannot be made to wait on the
tracker that delivers it.

- **The trigger is a decided need** — the project-wide specification plus a
  conversation that settled something. Not an item; there is no item yet.
- **The agreement sections come first**: the business goal, the user needs, the
  claims, the constraints, what is out of scope. That is the agreement.
- **The design sections follow**: how concerns are separated, and the technical
  design.
- **Items are cut from the claims afterwards**, each citing the claims it
  delivers. A feature item cannot exist before its claims do, because it
  references them.

So: **a feature item names its specification; a specification never names an
item.** One direction, nothing to keep in sync. The specification lists the item
ids cut from it, and "none yet" is the honest state while the agreement is still
being reached.

**Features only.** A **bug, spike or chore starts with an item** — its trigger
is an observation, not an agreement — and names no specification. A bug that
reveals a specification gap then produces a delta: a lesson, and the claim that
proves the delta held.

Put the other way round: the specification is where _agreement_ starts; the item
is where _delivery_ starts. They are different questions, answered in that
order.

## Two records, one authority each

| Record            | Authority over                                                                  | Never holds                      | Exists first?                  |
| ----------------- | ------------------------------------------------------------------------------- | -------------------------------- | ------------------------------ |
| The specification | **content** — requirements, constraints, design, testing strategy, traceability | the live status of the work      | **yes, for a Feature**         |
| The work item     | **delivery state** — status, priority, value, risk, rank, dependencies          | requirements, design, test plans | yes, for a bug, spike or chore |

- A specification carries its own **document** maturity and nothing about
  delivery. The work's status and scoring live in the item, and only there. Two
  copies of a status is two answers to one question.
- The specification lists item **ids**, not restated titles.
- **One claim lives in one place**: the acceptance-criteria section. An item
  _references_ claim ids; it never restates the claim text. Those ids are always
  already there — the specification was written first.
- Derived fields are recomputed, never hand-edited. The inverse of a dependency
  is not a claim of its own.
- **A closed item stays**, with the date it closed. The record is permanent
  history, like the specification itself.
- **A risk rationale names the hazard, not the shape of the work.** The number is
  read later to decide how hard to test something, so "crosses two boundaries"
  or "no existing coverage" gives a test writer nothing to aim at. Name the
  specific wrong answer and the claim it breaks, and the rationale becomes an
  instruction instead of a restatement of the score.

## Filling the blank

A specification is written by copying a template, and **a copy of a template is
not a document until the template's instructions come out of it.** The blank
carries guidance per section — who owns it, what belongs in it, what the rows
mean. That guidance belongs to the blank. Left in the copy it becomes a second
statement of a rule the blank already owns, and the copies reword themselves
over time until nothing says which one governs.

Reduce it to a pointer back at the template, or remove it. Either way the
document is content, and the instruction is read where it is maintained.

**A placeholder is not content.** An unreplaced example row reads as an answer —
a filled table, a populated matrix — and a reader cannot tell it apart from one.
This is worse than an empty section, because an empty section asks a question
and a leftover placeholder answers it wrongly.

So: **an unwritten section says so in words, and names the role that owes it.**
Not deleted, because deleting it destroys the signal that the question was
asked. Not left as a template row, because that signals the opposite of the
truth. "Unwritten; `<role>` owns it; what is left to settle is X" is the honest
form, and it is also the useful one — it tells the next reader what is missing
rather than making them diff against the blank to find out.

**An unwritten section that gates something says that too.** Where a section is
the coverage gate, its emptiness _is_ the gate holding, and the document should
say which downstream stage is therefore blocked. A gate that looks satisfied is
worse than no gate: it is the one failure mode nothing downstream can detect,
because every reader takes it at its word.

## One answer, one section

Within a single specification, **no section stores what another section
derives.** Two sections holding one answer is the same defect as a specification
and an item holding one answer, and it fails the same way — by hand, in the copy
nobody recomputed.

- A per-claim build state derives from the coverage matrix. The claim rows do
  not store it.
- An overall verdict derives from the per-owner sign-offs and the matrix. The
  document's maturity field is that verdict, not a mirror of a row that repeats
  it.
- A score's rationale is the specification's; the score itself is the item's.
  Carry the reasoning and cite the number.

**Writing the dependency down is not the same as removing it.** A note saying
"this field mirrors that row" documents a mirror and leaves it in place, which
reads as managed and behaves as duplicated. The remedy is one store, not a
better comment.

**A cite names something that exists.** A reference to a section is as
load-bearing as a link to a file, and nothing checks either: a cite to a heading
that was renamed, abbreviated or never written fails silently and survives every
review that reads the sentence rather than following it. When a section moves or
is renamed, the cites to it are part of the change.

## A declaration belongs to the file that compiles

A specification designs types before they exist, and writing the declaration
out is how that design gets stated — there is nowhere else for it to live yet.
That stops being true the moment the file exists. From then on the compiler is
the authority on the signature, and the copy in the document is a second store
with no build to keep it honest.

- **Replace the declaration with a row**: the type, the file, and what the
  declaration was there to make visible. The reasoning stays — why that name,
  why that shape, which claims it answers — because that lives nowhere else and
  is the specification's own.
- **A diagram is not a declaration.** Naming a type to show a relationship is
  not a second statement of its signature. Leave diagrams alone.
- **A record is not a living document.** A decision record argues from the
  declaration as it stood on its date, and an accepted one is immutable
  regardless. Only the documents that claim to describe the present track the
  code.

The change that creates the file is where this happens, not a tidy-up
afterwards.

**This is not a hand-kept index of files**, which § "Never add" rules out and
which would fall behind. It lists what this section designed and where each
one landed — a set the section already owns and already has to keep complete —
and every row is a link, so the link check reports a move. An index of the
tree would be neither.

**Deleting a declaration is a move, and the destination has to hold
everything** ([lesson 0003](../../.spec/lessons/0003-a-dedupe-is-a-move-and-a-move-has-a-destination.md)).
A pasted declaration often carries design that is not built yet — a
constructor parameter a later item adds, a member nothing calls. The file does
not hold that, so the prose has to, or it leaves with the paste.

## Claims and traceability

- Claims are numbered rows in the acceptance-criteria section. Each scenario
  carries the matching claim tag, and **the traceability matrix is the gate**:
  every claim appears there exactly once, and a missing row blocks ship.
- **A claim id is never renumbered and never reused.** A withdrawn claim is
  marked withdrawn, not deleted — the matrix and the scenarios are anchored to
  it.
- **Know what identifies a claim.** Where ids are scoped to one specification,
  two specifications may both number from the start and neither is renumbered
  for the other; what identifies the claim is then the pair of specification and
  id, and an id is only ever read against a named section. Per-scope numbering
  is what lets two Features be specified at once without colliding.
- Where scenarios are documentation rather than executable, **a scenario alone
  never satisfies a claim** — the matrix row does, and it points at a test
  ([`test-from-scenarios`](../test-from-scenarios/SKILL.md)). An unbuilt claim
  is marked once, in its matrix row — the row that names no test. The claim
  itself carries no build state, or there are two stores for one answer. A
  _withdrawn_ claim is the exception, marked on the claim, because the matrix
  has no row for a claim that was never going to be built. Nothing else, and
  nothing implying the scenarios execute.
- **A mechanism classifies by what the claim names, not by where today's one
  instance lives.** A folder, a namespace or a name suffix is a proxy that holds
  until a second instance arrives, and the mechanism then reports work the claim
  permits — usually printing the claim's own wording in the message, which is
  where the proxy should have been read from. Where the claim says "the class
  implementing X", the mechanism asks what a class implements.

## An imported rule is a decision, not a constraint

A rule that arrives from outside the repository — a pattern, a skill, a house
style, a reviewer's standard — is **a choice this repository made**, and it is
recorded the way choices are: a decision record or an architecture decision
record, with what it costs here and who accepted that cost.

A constraint row is the wrong store for it. A constraint is something the
project does not control — a provider's wire format, a credit budget, a library
that cannot do a thing — and its row carries a source and an impact because
there is nothing to argue. Put an imported rule there and it reads as settled
while recording no reasoning, so nothing can answer "why" later.

**It does not stay a single row.** A rule with no recorded reason accumulates
one: every document that cites it has to say something about it, so a
justification gets written in a scoring rationale, a test name, a diagnostic
message. Each is written to explain a rule already in place, and together they
read exactly like a decision someone reached — which is what makes the absence
hard to notice, and what makes the rule expensive to withdraw once a claim, a
test and an analyzer rule all point at it.

Two signals, both cheap to check:

- **A claim whose source names only an external document.** Nothing in this
  repository is cited, so nothing in it decided.
- **A claim whose only test is a test of a test double, a fixture or a
  declaration.** A rule that costs a test with no product behaviour under it is
  a rule to put to the person before it spreads.

The question to ask is not whether the rule is good. It is whether anyone here
chose it, and what it costs
([lesson 0002](../../src/Transponder/Integrations/OpenSky/.spec/lessons/0002-an-inherited-rule-is-not-a-decision.md)).

## Numbers from independent sequences collide

Several schemes number from the start independently, and a per-scope scheme
starts again in every scope, so a bare number is ambiguous on sight unless its
prefix identifies it.

**In prose, write the scheme with the number.** A number may appear bare only
where its field says what it is — a parent id in an item, or a table column that
names the scheme.

This is a writing rule, not a renumbering one. Per-scheme numbering is
deliberate: it is what lets a scope carry its own records without reserving
numbers from a shared pool. The collision is only ever in how a number is
_referred to_.

## Section ownership

**Every section has exactly one owning role.** The mapping is written in exactly
one place — the companion — and each role file links there rather than
restating its own sections.

- **A role writes only its own sections.** Filling in a missing claim because it
  was in the way is a boundary violation, not a favour; escalate to the role
  that owns it. A lesson that adds behavior also needs a claim, which belongs to
  the specification author.
- Where a section has no natural owner because the project has no such role, it
  sits with the nearest role and **the compromise is written down**. A decision
  bigger than the item in hand then stops and goes to the person, with the
  options and the tradeoff named, rather than being settled inside an
  implementation pull request.

## Records beside a specification

Three kinds, each with one job:

- **decisions** — product and scope calls that were decided, reneged, or
  redirected.
- **architecture decision records** — durable technical choices.
- **lessons** — one file each: symptom, root cause, specification delta, and the
  claim proving the delta held.

**Decision records and lessons split by blast radius**: one that binds every
area lives in the repository-wide directory, numbered repository-wide; one
scoped to a single area lives beside that area's specification. A library, a
transport or a layout rule binds everything; a choice about one area's internals
does not.

## Identifiers are claimed after rebasing

An id, a number or a slug is claimed the moment someone else merges it. Take it
after rebasing onto the fresh trunk, never from the tree as you started
([`deliver-change`](../deliver-change/SKILL.md)). Lost the race? Take the new
number and rewrite every reference; renaming is cheap.

## Never add

- A specification file outside the specification directory, or a second place
  claims live.
- Delivery status on a specification that disagrees with its item, or a claim
  restated in an item instead of referenced.
- A renumbered or reused claim id, or a withdrawn claim deleted rather than
  marked.
- A tracker-shaped record the project's workflow does not use.
- **The template's authoring guidance, carried into the document it produced.**
- **A placeholder row left where content belongs**, or a deleted section where
  an empty one would have recorded the question.
- **A section storing what another section derives**, or a note explaining the
  mirror instead of removing it.
- **A gate reporting that it passed when nothing has been verified.** An
  unwritten gate reports that it is unwritten.
- A cite to a file, a record or a section that does not exist.
- A hand-kept index of files that a tool could generate. It falls behind.
- **A rule imported from outside the repository, stored as a constraint.** It is
  a decision and it names what it costs here.
