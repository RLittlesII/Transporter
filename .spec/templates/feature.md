---
title: "Specification: {{feature_title}}"
description: "{{one_line_summary}}"
type: spec
spec_status: draft
---

<!-- Copy this file to features/<feature-slug>/.spec/README.md and fill it in.

     THIS FILE COMES FIRST. A Feature specification stands on its own: it needs
     no work item to exist, because it is the agreement that work items are
     later cut from. `## Tasks` below lists the items derived from § 3.

     A FEATURE ITEM NAMES ITS SPEC; A SPEC NEVER NAMES AN ITEM. One direction,
     so there is nothing to keep in sync.

     `spec_status` is DOCUMENT maturity: draft | in-review | approved | superseded.

     Delivery state does NOT live here. `status`, `priority`, `value`, `risk`
     and `rank` belong to the .issue/ item, which is the single authority for
     them — see .skills/spec-and-traceability/SKILL.md.

     Keep the section order. Replace the example rows. DO NOT DELETE A SECTION
     BECAUSE IT IS EMPTY — an empty section is a decision ("out of scope", "no
     open questions") and says so in words. Deleting it destroys the signal
     that the question was asked. An unwritten section says so IN WORDS and
     names the role that owes it; it never keeps a {{placeholder}} row, which
     reads as content and is not.

     THE GUIDANCE BELOW BELONGS TO THIS FILE, NOT TO THE COPY. Each section
     carries a comment saying how to fill it. On copy those are REDUCED TO A
     ONE-LINE POINTER, the way {{placeholders}} are replaced:

         ## 3. Acceptance Criteria

         <!-- Rules: ../../../.spec/templates/feature.md § 3 -->

     The pointer names no owner. Section ownership is written in exactly one
     place, .skills/transponder-conventions/SKILL.md § "Section ownership",
     and a copy that restated it would be one more place to keep in step.
     A role writes only its own sections; filling in someone else's is a
     boundary violation, not a favour. -->

# Specification: {{feature_title}}

## 1. Business Goal

<!-- Owner: spec-author. One paragraph. The outcome, not the implementation.
     Name the failure state being removed. -->

{{business_goal}}

## 2. User Needs

<!-- Owner: spec-author. The audience for this project is specific — see
     README.md § "Audience" — so do not write a generic persona. -->

| #   | Persona     | Need     | Pain point today |
| --- | ----------- | -------- | ---------------- |
| 1   | {{persona}} | {{need}} | {{pain}}         |

## 3. Acceptance Criteria

<!-- Owner: spec-author. Numbered, falsifiable, SHALL / SHALL NOT. One claim
     per row. These ids are what § 9 and the .feature file are anchored to, so
     they are permanent: never renumbered, never reused. A withdrawn claim is
     marked Withdrawn, not deleted. -->

| ID    | Claim     | Source     |
| ----- | --------- | ---------- |
| B-001 | {{claim}} | {{source}} |

<!-- A claim's build state is NOT written here. § 9 is the only store for it:
     a row naming a test is coverage, a row naming none is Missing. A
     WITHDRAWN claim is the exception — § 9 has no row for a claim that was
     never going to be built — so it is marked on the claim itself, which
     opens `**Withdrawn** —`. -->

## 4. Constraints

<!-- Owner: spec-author. Impact states what the constraint rules out, so § 7
     has something concrete to satisfy. Many of this project's constraints are
     the data provider's and are not ours to simplify — credit budgets, token
     expiry, blocked hyperscaler IPs (README.md). -->

| #   | Constraint     | Source     | Impact     |
| --- | -------------- | ---------- | ---------- |
| 1   | {{constraint}} | {{source}} | {{impact}} |

## 5. Out of Scope

<!-- Owner: spec-author. The section nobody writes. Without it, a demo grows a
     feature nobody asked for. -->

| #   | Item     | Exclusion reason |
| --- | -------- | ---------------- |
| 1   | {{item}} | {{reason}}       |

## 6. Concern Separation

<!-- Owner: implementer. Classifies each item Business, Technical, or Both —
     the mechanism that stops business and technical judgment collapsing into
     one undifferentiated paragraph. -->

| Item     | Classification | Notes     |
| -------- | -------------- | --------- |
| {{item}} | Business       | {{notes}} |

## 7. Technical Design

<!-- Owner: implementer. One owner by design, and a compromise: a decision
     bigger than this item — a new seam, a changed boundary, a technology
     choice — is a question for the person, not something to settle here. -->

**Domain model**

| Field     | Type     | Notes     |
| --------- | -------- | --------- |
| {{field}} | {{type}} | {{notes}} |

**Diagrams**

<!-- Mermaid (AGENTS.md). Declare every type; one that does not apply says
     `Not applicable — <reason>` rather than being silently dropped. Never
     summarize a diagram into prose and delete the diagram. -->

```mermaid
{{diagram}}
```

**Interface changes**

<!-- Write a declaration out only while its file does not exist. Once it does,
     it becomes a row below and the prose around it stays
     (spec-and-traceability § "A declaration belongs to the file that
     compiles"). Anything the declaration carried that the file does not —
     a parameter a later item adds, a member nothing calls yet — moves into
     the prose rather than leaving with it. -->

| Type     | File     | Claims it makes visible |
| -------- | -------- | ----------------------- |
| {{type}} | {{file}} | {{claims}}              |

{{interface_changes}}

**Decision required**

> | Option | Summary     | Tradeoff     |
> | ------ | ----------- | ------------ |
> | A.     | {{summary}} | {{tradeoff}} |
>
> **Recommendation:** {{recommendation}}
> **Awaiting:** {{decision_owner}}

<!-- Omit the block when nothing is open, but say so: "No open decisions." -->

## 8. Testing Strategy

<!-- Owner: test-writer. -->

**Testability assessment**

| Dimension          | Verdict | Finding     | Recommendation |
| ------------------ | ------- | ----------- | -------------- |
| DI seams           | Pass    | {{finding}} | —              |
| Behavior isolation | Pass    | {{finding}} | —              |
| Coverage potential | Pass    | {{finding}} | —              |

**Scenarios**

<!-- Full Gherkin lives in <feature-slug>.feature beside this file, not
     inlined here. Each scenario carries the @B-00n tag of the claim it
     proves. Scenarios are documentation; the xUnit tests execute. -->

- Happy path → {{claim_ids}}
- Failure mode → {{claim_ids}}
- Validation failure → {{claim_ids}}
- Data-driven → {{claim_ids}}

## 9. Traceability Matrix

<!-- Owner: test-writer. The gate. A `Missing` row blocks ship — it is not a
     note, it is a stop sign. Every id in § 3 appears here exactly once,
     anchored to the scenario's @B-00n TAG rather than to the title in the
     Scenario column. -->

| Claim ID | Scenario     | Test     | Status   |
| -------- | ------------ | -------- | -------- |
| B-001    | {{scenario}} | {{test}} | Verified |

<!-- Status: Verified | Missing. -->

## 10. Lessons / Spec Deltas

<!-- Owner: whichever role closed the bug. Index only — one file per lesson in
     lessons/ beside this file, from .spec/templates/lesson.md. Append only. A
     delta that adds behavior also needs a § 3 row, which is spec-author's to
     write. "None yet." is a valid body. -->

- [{{NNNN}} — {{lesson_title}}](lessons/{{NNNN}}-{{lesson-slug}}.md) — {{date}}

## 11. Open Questions

<!-- Owner: whoever is blocked. "None." is a valid body. -->

| #   | Question     | Owner     | Target date |
| --- | ------------ | --------- | ----------- |
| 1   | {{question}} | {{owner}} | {{date}}    |

## 12. Sign-off

<!-- Owner: spec-reviewer. 🟡 Draft | 🟢 Approved | 🔴 Blocked — state the
     reason on a Blocked row. One row per owner, named by section number:
     the mapping from number to owner is written in
     .skills/transponder-conventions/SKILL.md, not restated here.

     There is NO Overall row. The frontmatter's `spec_status` is the single
     store for document maturity. -->

| Sections   | Owner          | Status   |
| ---------- | -------------- | -------- |
| §§ 1-5     | spec-author    | 🟡 Draft |
| §§ 6-7     | implementer    | 🟡 Draft |
| §§ 8-9     | test-writer    | 🟡 Draft |

Overall is the frontmatter's `spec_status`, not a row here. `spec-reviewer`
flips it to `approved` when every row above is 🟢 and § 9 has no `Missing`
row.

## Decisions

<!-- Owner: the role that made or reversed the call. Index only — one file per
     decision in decisions/ beside this file, from
     .spec/templates/decision.md. A product or scope call goes there; a
     durable technical choice goes to adr/ instead. "None yet." is valid. -->

- [{{NNNN}} — {{decision_title}}](decisions/{{NNNN}}-{{decision-slug}}.md) — {{status}}

## Tasks

<!-- Owner: spec-author. The items cut from § 3 once the claims exist. Ids
     and claim ids only — never a restated title or claim text, or the two
     records disagree. "None yet." is valid while the spec is still being
     agreed. The items are in .issue/ beside this file, so link each id to
     its own file. -->

| Item                            | Claims              |
| ------------------------------- | ------------------- |
| [`0002`](../.issue/{{item_file}}) | {{claim_ids}}     |

## Scoring

<!-- Owner: spec-author, recording WHY an item's value or risk is where it
     is, and why it moved. The numbers themselves live in the .issue/ item,
     which is authoritative for them — this table carries rationale only, so
     the two cannot disagree. -->

| Date     | Item       | Field | Rationale     |
| -------- | ---------- | ----- | ------------- |
| {{date}} | `{{item}}` | value | {{rationale}} |
