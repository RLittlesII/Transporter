---
title: "Lesson 0001: The head must run its startup report"
description: "A sandbox-inaccessible recording went unreported because the MAUI head never started the report's hosted-service adapter."
type: lesson
---

# Lesson 0001: The head must run its startup report

**Date:** 2026-10-14
**Kind:** product

## Symptom

A recording could be replayed by a generic host while the Mac Catalyst head
showed an empty fleet and no recording diagnostic. Pointing the head at the
repository directory could pass an existence check and fail on opening the
file. Copying the recording into its container and committing no absolute
path was a manual workaround, not a working default.

## Root cause

The library's relative root did not account for the head's sandbox. The report
was registered as `IHostedService`, which a generic host starts and a MAUI
service provider does not. Invoking the logger alone would still be silent in
a head with no enabled output provider. File-open failure and no readable
recorded line also shared one diagnosis, hiding the sandbox's signature.

## Spec delta

The approved B-025 amendment gives the head an application-data root beneath
packaged settings and user secrets; both recording and replay read it.
B-026 explicitly covers the MAUI startup route, distinct failure diagnoses,
continued startup and the console output the presenter can inspect. The
implementation reuses one reporter and its checked rows, with a focused head
call rather than starting unrelated hosted services. Read and rewind failures
release the rejected stream and become a reported defect.

## Claim

- B-025 — `RecordingRootTests.GivenAPlatformRootAndOptionalOverrides_WhenConfigurationIsComposed_ThenTheHighestPriorityRootIsShared`,
  with the head's platform-directory expression reviewed in § 9.
- B-026 — `ReplayHeadStartupTests.GivenANamedRecording_WhenTheHeadReportsStartup_ThenItsDistinctDiagnosisIsLoggedWithoutStartingServices`
  and `GivenAStreamThatFailsInspection_WhenReplayIsRegisteredAndReported_ThenItIsUnreadableAndReleased`,
  with the head's call, console sink and runbook/export checks reviewed in § 9.
