---
title: "Decision 0003: A silent swap clears its indicator"
description: "Bound the swap indicator's presentation window without cancelling the selected feed or preventing recovery through the same selector."
type: decision
---

# Decision 0003: A silent swap clears its indicator

**Status:** decided

**Date:** 2026-10-14
**Made by:** the person

## The call

Use the ten-second swap-indicator cap proposed in § 11 row 9. Permit a
one-shot deadline on the injected scheduler when its only effect is closing
a refresh or swap indicator window. B-016 and B-017 carry the behavior and
the exception; no polling or repeating timer is permitted by this call.

## Why

A provider can refuse, a source can stay silent, and the current filter can
hide every incoming item. None should leave the presentation in a permanent
busy state. The deadline changes only presentation state, so the presenter
can choose replay through the same selector while the selected source's
subscription remains the actor and decorator's responsibility.

The first visible addition remains the normal completion signal chosen in
[aircraft-source decision 0002](../../../../Integrations/OpenSky/.spec/decisions/0002-busy-indicator-on-swap.md).
Ten seconds is a presentation choice to check in rehearsal, rather than a
provider timeout or a guarantee that a source succeeded.

## Rejected

An indefinite busy indicator leaves a silent or fully filtered source
looking busy forever. An automatic fallback, a source acknowledgement and
a second replay control add behavior outside the selected-source boundary;
this call introduces none of them. A repeating view-model timer is not
needed to close one presentation window.

## Affects

- B-016 and B-017, their tagged scenarios, and §§ 7-9.
- § 11 row 9 and the agreement review required by § 12 finding 14.
- Work item `0039`; this decision grants no exemption from implementation evidence.
