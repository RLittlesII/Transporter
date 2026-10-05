---
title: "ADR-{{NNNN}}: {{decision_title}}"
description: "{{one_line_summary}}"
type: adr
---

<!-- WHICH adr/ DIRECTORY:
       - .spec/adr/ at the repository root, for a CROSS-CUTTING decision that
         binds every Feature — a library, a transport, a layout rule. Numbered
         repo-wide, starting at 0001.
       - features/<feature-slug>/.spec/adr/, for a decision scoped to that one
         Feature. Numbered per Feature, starting at 0001.
     Pick by blast radius, not by who happened to make it.

     An ADR records a durable technical or architecture decision: a new rule
     about the system, a reversed one, or a technology choice with a rejected
     alternative. UI polish, a bug fix, and a routine detail with no
     alternative worth naming need none.

     NOT an ADR:
       - a process, tooling or workflow rule — that is a convention, edited in
         place in the skill that owns it;
       - an interface detail (wording, a field's behavior) — that is a claim in
         § 3;
       - a product or scope call — that is a decisions/ record.

     AN ACCEPTED ADR IS IMMUTABLE. Only its status changes. A change to the
     decision is a NEW ADR; this one becomes `superseded` with a link to it.
     Never append an amendment, rewrite the body, or delete a record — a typo
     in an accepted ADR stays.

     Number it after rebasing, and keep the filename and the heading in step. -->

# ADR-{{NNNN}}: {{decision_title}}

**Status:** proposed

<!-- proposed (may change freely) | accepted | rejected | deprecated |
     superseded. An accepted record moves only to superseded or deprecated,
     and its status line then links the record that replaced or retired it;
     the other three are terminal. There is no partial supersession: a new ADR
     that changes part of an older one supersedes the whole record and lists
     what of it still holds, by link or claim id, never by restating it. -->

## Context

<!-- What is true that forces a decision. Facts, not preferences. Name the
     constraint or the claim that put this on the table. -->

{{context}}

## Decision drivers

<!-- Optional. What actually decides it, in priority order. -->

- {{driver}}

## Considered options

<!-- REQUIRED. A decision with no alternative worth naming says so here, in
     words, rather than leaving the section out. -->

| Option     | Summary     | Why not     |
| ---------- | ----------- | ----------- |
| {{option}} | {{summary}} | {{why_not}} |

## Decision

{{decision}}

## Consequences

<!-- Both directions. What this buys, and what it costs or forecloses. -->

- {{consequence}}
