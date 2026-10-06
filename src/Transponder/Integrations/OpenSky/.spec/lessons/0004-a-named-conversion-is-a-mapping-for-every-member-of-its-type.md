---
title: "Lesson 0004: A named conversion is a mapping for every member of its type"
description: "ToKey was written for one member and Mapperly applied it to every string on the snapshot, so the first generated projection lowercased the origin country — a wrong answer that renders as plausible data and that the key's own test passes anyway."
type: lesson
---

# Lesson 0004: A named conversion is a mapping for every member of its type

**Date:** 2026-10-05
**Kind:** implementation

## Symptom

`AircraftSnapshotMapper` declared `ToKey(string)` because B-037 needs the
`icao24` lowercased and `mapping` § "Conversions are explicit" requires the
conversion to be a named method rather than something buried in a generated
member mapping. The first generated projection read:

```csharp
target.OriginCountry = ToKey(snapshot.OriginCountry);
```

"United Kingdom" reached the domain as "united kingdom". Nothing failed: the
build was clean, `RequiredMappingStrategy.Both` was satisfied — every member
_was_ mapped — and the test B-037 names asserts the key and only the key, so it
passed over the defect. The projection was read before the tests were written,
which is the only reason it was caught at all.

## Root cause

**A user-implemented mapping method is a rule about a type pair, not about the
member it was written for.** Mapperly matches `string` to `string` and uses the
method wherever that pair occurs, so a conversion written for one member becomes
the conversion for every member of its type. `ToPosition` and
`ToPositionSource` were safe by accident — a whole-snapshot source and an `int`
to `Option<PositionSource>` pair occur once each — and `ToKey` was the one whose
type pair is the most common on the snapshot.

The class of defect is the one B-036 and B-017 are about, arriving from a
direction neither claim points at: a value that is wrong rather than absent, and
plausible enough to render. A lowercased country sorts and groups differently
and never looks like a bug.

## Spec delta

§ 7's projection paragraph now carries the scoping rule and names the attribute:
`[UserMapping(Default = false)]` on `ToKey` and on the non-optional `ToInstant`,
both of which exist for one call site each. The same paragraph records the
`[ObjectFactory]` the constructor ADR-0005 item 3 requires forces, and the
re-keying of the changeset onto the vehicle's own key. No claim changed: B-035
through B-037 are the claims, and they are proven by the tests § 9 already named.

The tests gained one case that would have caught it —
`AircraftSnapshotMapperTests.GivenAReportedOriginCountry_WhenProjected_ThenOnlyTheKeyWasLowercased`
— because a test naming only the member a conversion was written for cannot tell
the difference between a scoped conversion and an unscoped one.

## Process delta

[`mapping`](../../../../../../.skills/mapping/SKILL.md) § "Conversions are explicit,
never implicit" now says that a named conversion whose type pair is not unique
on the source is scoped out of the default mappings, and that the generated
output is read once per mapper rather than trusted because the build is green.
The skill already required the named method; what it did not say is that naming
it is half the work.

## Claim

- B-037, the key's lowercasing, and B-035, the values that reach the vehicle
  unconverted. Neither changed; the mapper did.
