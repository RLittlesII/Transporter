# What a change documents

The specification, the scenarios, coverage exemptions, lessons, decision records, and markdown rules.

## Document

### Specification

- **A specification is not documentation of the change; it is the thing the
  change serves.** It was written before this item existed, and it outlives the
  item. Amending it is editing an agreement, so the edit is deliberate and the
  pull request says what moved.
- Write only the sections your role owns
  ([`spec-and-traceability`](../../spec-and-traceability/SKILL.md)).
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
- **Argue from the constraint, not from the repository's state.** A record
  whose reasoning rests on what the tree happens to hold — a target that does
  not exist, a package not referenced, a suite nothing runs — carries an
  argument that expires the day someone changes it, and nothing reports the
  expiry. Say what is durable; where the state is the point, say what about it
  is durable. Follow every cite as you write it, too: a link to a file that was
  never created reads exactly like a link to one that was.
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
