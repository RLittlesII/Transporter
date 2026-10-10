---
title: "Decision 0001: The observed instant clears the refresh indicator"
description: "A refresh indicator set by the gesture is cleared by the next observed instant — every applied poll reports one, identical data included — with a three-second cap for the press that was refused."
type: decision
---

# Decision 0001: The observed instant clears the refresh indicator

**Status:** decided

**Date:** 2026-10-07
**Made by:** the person

## The call

The refresh indicator B-028 carries is set by the gesture and cleared by the
next **observed instant** — the value `aircraft-source` B-003 reports to the
clock on every applied poll, including a poll whose data was identical. A
three-second cap clears it when no poll arrives, which is the refused press.
`fleet-pipeline` B-031 publishes the instant on `IFleetTracker`, built by
[`0059`](../../../../Tracking/.issue/0059-observed-instant-on-the-tracker.yml),
and the view model exposes the result as a derived value rather than a field it
sets.

## Why

§ 11 row 6 had two options and both lied somewhere. A fixed duration means "we
asked", so on a slow poll it clears before the data lands and reads as a failed
refresh. The next notice (`fleet-pipeline` B-025) is raised only when a changeset
changed something, and a refresh returning identical data is the most ordinary
outcome there is — `EditDiff` emits nothing for an unchanged snapshot
(`aircraft-source` B-011), so the indicator would spin for ever in the common
case.

The observed instant is the signal neither option had: the client reports the
envelope's time on **every** applied poll (`aircraft-source` B-003), and that
value differs per poll whether or not any aircraft moved. So "a poll landed" is
observable without an `Ask` and without the fleet changing, which is exactly
what B-028 asks for and what ADR-0012 § Consequences warned could not come from
the fleet.

Two things make it near-exact rather than approximate. `aircraft-source` `0057`
made the cadence yield to a demanded poll, so after a press the next poll to
land **is** the demanded one — the scheduled one is a full interval away. And
the clock seam already exists (ADR-0010), so the work is one member on the
published seam rather than a new mechanism.

The cap covers the one case with no poll at all: a press refused inside the
interval. Three seconds, because a demanded poll's instant arrives in well
under one on any network a talk is given on, and because a refusal should read
as "nothing new" rather than as fifteen seconds of spinner. It is the number to
revisit after the first rehearsal, and the only one in this record chosen by
feel.

## Rejected

**A fixed duration alone** — the § 11 option. Cheapest, no claim in another
Feature, and `0058` could have started the same day. Rejected because the
indicator then means "we asked" and nothing else: a poll slower than the
duration clears early and reads as a failure, and the gesture's whole purpose is
to say that data arrived.

**The next notice, with a duration as fallback.** Rejected because the fallback
would carry the common path rather than the exception — identical data raises no
notice — so it is the fixed duration wearing a second subscription.

**Letting the actor reply.** Exact, and excluded by B-028 as written: a reply is
an `Ask`, which puts a timeout and a waiting view model back in (B-017).

**A poll-status stream published end to end** — started, finished, refused,
with the window's remaining time. The only option that can tell the user a
press was refused and when it reopens, and still a `Tell`. Rejected as more
mechanism than the gesture is worth: a new status type and a seam crossing
`aircraft-source` and `fleet-pipeline`, claims in both, and an amendment to
B-028's "driven by the gesture". Available later if the stage shows the cap
reading as a lie.

## Affects

- B-028 — unamended. The indicator is still driven by the gesture and not by the
  fleet changing; this record says what ends it.
- § 7's `FleetViewModel.IsRefreshing` cell — now states what clears it, the cap,
  and that it is a derived value, where it previously deferred to this row.
- § 11 row 6 — answered and closed.
- `fleet-pipeline` B-031 — new, the instant on the published seam, built by
  `0059`.
- [`0058`](../../.issue/0058-refresh-control.yml) — no longer blocked; it waits
  on `0059` instead.

## Reversal

Not applicable — this record has not been reversed.
