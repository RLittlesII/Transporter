---
title: "Lesson 0006: A constraint nobody claims is a rule nothing builds"
description: "§ 4 row 12 said credentials come from user secrets or environment variables, no claim made anything read them, and the day the person held a credential the application had nowhere to take it from."
type: lesson
---

# Lesson 0006: A constraint nobody claims is a rule nothing builds

**Date:** 2026-10-09
**Kind:** process

## Symptom

The person registered the OpenSky account, created the API client, and asked
what to do with the credentials. There was no answer in the repository. The
head built its configuration from the packaged `appsettings.json` alone, which
carries no credential by `0047`'s decision, so the live source could only ever
answer `401` — on every machine, including the presenter's.

Nothing had failed before that day. Every § 9 row a credential touches read
`Verified`: B-026's refresh, B-027's log, B-029's startup failure. Each is
proven against a configuration a test writes.

## Root cause

**The rule was written as a constraint, and a constraint obliges nobody.**
§ 4 row 12 has said since 2026-10-04 that credentials come from user secrets
or environment variables. A § 4 row carries a source and an impact — what it
rules out — and row 12's impact is three things the code must not do. That the
application must _read_ a store is an obligation, and no row in § 3 claimed it,
so no scenario described it, no § 9 row could be `Missing` for it, and no item
was ever cut for it.

Two exclusions then pointed past it. § 5 row 15 excludes "provisioning the
credentials" as an operational task, which is true of obtaining one and was
read as covering the code that takes one in. And `0047`'s `out_of_scope`
says "supplying credentials, and where a developer keeps them — nothing here
reads one", naming the gap exactly and routing it to no item. The item
template already asks an exclusion to say who owns the thing; this one had
nobody to name, and that was the signal.

## Spec delta

B-058 claims that the application's configuration layers the developer's
user-secrets store over the packaged settings, and is the packaged settings
unchanged when there is no store. § 5 row 19 draws the boundary the mechanism
needs: a bundle built beside a store contains the secret, and is never
distributed. § 6 and § 7 say why the store is packaged at build rather than
read at run. § 4 row 12 stands as written, and now has a claim under it.

## Claim

- B-058 —
  `TransporterConfigurationTests.GivenAUserSecretsStoreHoldingTheCredentials_WhenTheConfigurationIsComposed_ThenTheCredentialsAreTheStoresAndNoOtherSettingChanges`
  `TransporterConfigurationTests.GivenAUserSecretsStoreNamingASettingThePackagedSettingsName_WhenTheConfigurationIsComposed_ThenTheSettingIsTheStores`
  and
  `TransporterConfigurationTests.GivenNoUserSecretsStore_WhenTheConfigurationIsComposed_ThenItIsThePackagedSettingsAndNoCredentialIsConfigured`.
  The packaging half is a review, recorded on B-058's § 9 row, which reads
  `Missing` until the head is launched beside a store.

## Skill

[`spec-and-traceability`](../../../../../../.skills/spec-and-traceability/SKILL.md)
§ "Claims and traceability" gains: a constraint that obliges the code to do
something has a claim under it, and an exclusion that names a gap and routes it
to nobody is a claim not yet written.
