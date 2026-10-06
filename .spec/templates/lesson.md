---
title: "Lesson {{NNNN}}: {{lesson_title}}"
description: "{{one_line_summary}}"
type: lesson
---

<!-- Copy to <home>/.spec/lessons/{{NNNN}}-{{lesson-slug}}.md, beside that
     Feature's specification, and add a row to § 10 of that Feature's
     README.md.

     A lesson that binds every Feature goes in the ROOT .spec/lessons/ instead,
     numbered repo-wide from 0001 — the same blast-radius split .spec/adr/ uses.
     It has no § 10 row to add, because it belongs to no one Feature.

     A bug fix that reveals a specification gap ships its lesson IN THE SAME
     PULL REQUEST as the fix (AGENTS.md). A fix that reveals nothing writes
     none. Specifications never stay silent about fixed bugs — silence
     perpetuates the same bug later.

     Numbered per Feature, next sequential after the highest file already in
     lessons/, starting at 0001. Append only. -->

# Lesson {{NNNN}}: {{lesson_title}}

**Date:** {{date}}
**Kind:** product

<!-- product — the remedy is a claim and a scenario. The spec delta names the
     claim. No skill change: restating product behavior in a skill creates a
     second place to drift from the specification.

     process — tooling, the build, conventions, how the work is done. ALSO
     update the skill that would have prevented it, in the same pull request,
     and name it under ## Skill below. The skill holds the general rule; this
     file keeps the incident. Readers reach for skills, not the lessons index.

     incident — something failed while running the demo. Symptom and root
     cause are what it owes. -->

## Symptom

<!-- What was observed, in the terms it was observed in. Not the diagnosis. -->

{{symptom}}

## Root cause

<!-- What actually caused it. If the honest answer is "the specification never
     said", that is the finding, and the spec delta below is the fix. -->

{{root_cause}}

## Spec delta

<!-- What the specification now says differently. A delta that adds behavior
     also adds a § 3 claim row, which is spec-author's to write — name it
     here. "No delta: the claim already covered this and the code was wrong"
     is a legitimate answer. -->

{{spec_delta}}

## Claim

<!-- The B-00n claim that now proves the delta held, and the test that cites
     it. Without this row the lesson is a story rather than a guarantee. -->

- {{claim_id}} — {{test}}

## Skill

<!-- Process lessons only: the skill updated so this cannot recur, and the one
     line that changed in it. Omit the section for a product lesson; never
     invent a rule just to have something to put here. -->

{{skill}}
