---
title: "Lesson 0001: Binary frames carry JSON"
description: "The receive adapter dropped AISStream's binary JSON messages while string-based tests bypassed the provider's framing."
type: lesson
---

# Lesson 0001: Binary frames carry JSON

**Date:** 2026-10-17
**Kind:** product

## Symptom

Independent review found that the production receive adapter discarded binary
WebSocket messages. A subscription confirmation denying compression could
therefore produce no warning even though the connection stayed open.

## Root cause

The adapter accepted text messages only, while AISStream sends binary messages
containing UTF-8 JSON. The specification described the JSON envelope but omitted
its WebSocket framing. Client tests supplied strings directly and bypassed the
adapter, so they could not expose that mismatch.

## Spec delta

§ 4 row 15 now records the provider's binary UTF-8 framing, sourced to the
[AISStream documentation](https://aisstream.io/documentation). A B-007 scenario
requires the denial confirmation to remain readable across receive fragments,
producing a warning while leaving the stream open. B-007 already requires that
warning; its wording and the required behavior do not change.

## Claim

- B-007 — the binary compression-confirmation scenario in
  [vessel-source.feature](../vessel-source.feature); its executing test evidence
  belongs in the [specification's § 9](../README.md#9-traceability-matrix).
