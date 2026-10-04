---
title: "Decision {{NNNN}}: {{decision_title}}"
description: "{{one_line_summary}}"
type: decision
---

<!-- Copy to
     features/<feature-slug>/.spec/decisions/{{NNNN}}-{{decision-slug}}.md and
     add a row to the ## Decisions index of that Feature's README.md.

     A decisions/ record is a PRODUCT OR SCOPE call that was decided, reneged,
     or redirected — what we will and will not build, and why. A durable
     technical or architecture choice goes to adr/ instead
     (.spec/templates/adr.md).

     The test: if it changes what the demo does or does not do, it is a
     decision. If it changes how the code is shaped, it is an ADR.

     Numbered per Feature, next sequential after the highest file already in
     decisions/, starting at 0001. -->

# Decision {{NNNN}}: {{decision_title}}

**Status:** decided

<!-- decided | reneged (we committed, then did not do it) | redirected (the
     need was met another way). A reversal does not overwrite this file — set
     the status, add the Reversal section, and keep the original reasoning
     readable. -->

**Date:** {{date}}
**Made by:** {{who}}

## The call

<!-- One or two sentences. What was decided, in the plainest available words. -->

{{the_call}}

## Why

<!-- The reasoning as it stood at the time. Do not retrofit it later to look
     better than it was. -->

{{why}}

## Rejected

<!-- What was turned down, and what it would have cost. "Nothing — there was
     no live alternative" is a valid answer, and worth saying so the next
     reader does not go looking for one. -->

{{rejected}}

## Affects

<!-- The claims, sections, or items this changes. A decision that narrows
     scope usually adds a § 5 Out of Scope row — name it. -->

- {{claim_or_section}}

## Reversal

<!-- Only when the status is reneged or redirected: the date, who, and what
     changed in the world to justify it. Omit the section otherwise. -->

{{reversal}}
