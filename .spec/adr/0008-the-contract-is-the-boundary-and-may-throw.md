---
title: "ADR-0008: The API contract is the I/O boundary, and a boundary throws"
description: "A provider contract returns the successful payload and signals every failure by throwing a contract-owned exception; the Failure/Result track begins on the application side of that call, not inside the contract's return type."
type: adr
---

# ADR-0008: The API contract is the I/O boundary, and a boundary throws

**Status:** proposed

## Context

[ADR-0002](0002-contract-client-strategy-tracker.md) places a typed contract
between a provider and the domain and settles what each layer above it is for.
It is silent on one thing: what the contract does when the call does not come
back with a payload. Nothing else answered it either, so
[`src/Transporter/Integrations/OpenSky`](../../src/Transporter/Integrations/OpenSky/.spec/README.md)
§ 7 answered it per-Feature, returning
`Either<OpenSkyThrottled, OpenSkyStatesResponse>` so that a throttle response
arrived as a value.

Four things were wrong with that, and they are general rather than particular
to OpenSky:

- **The failure channel ended up being `catch` regardless.** A throttle was one
  of several ways a poll can fail to produce data; the rest — a server error, a
  timeout, a body that will not parse — stayed exceptions. A caller asking the
  single question "did this call produce data?" had to write a match _and_ a
  `try`/`catch`, and the one thing in the `Left` was the one outcome that was
  not a failure.
- **`Left` reads as the error arm.** A reader who knows the type expects the
  failures there and finds a non-failure; a reader who does not know the type
  has to go and learn it to read a method signature. This repository is a
  conference talk's takeaway for developers who mostly consume REST endpoints,
  so a signature that has to be explained before it can be read is a cost the
  artefact pays twice.
- **It is not structurally a two-case union.** `LanguageExt`'s `Either` carries
  a third, bottom state for the default-constructed value, which matches
  neither arm and throws when matched without a handler for it. A union of two
  outcomes should have two states.
- **A second transport could not produce the case.** [ADR-0002](0002-contract-client-strategy-tracker.md)
  is implemented once per transport, and a transport reading a recording has no
  provider to be throttled by. It would have had to declare an arm it can never
  return.

## Decision drivers

- One mechanism per question. A caller deciding whether it has data should look
  in one place.
- A contract declares what every one of its transports can produce, and nothing
  else.
- Nothing above the integration names the transport library.
- The signature is readable by someone who has not read this repository.

## Considered options

| Option                                                                          | Summary                                                                                                                                   | Why not                                                                                                                                                                                                                                                                                                                          |
| ------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| The payload, and a contract-owned exception per actionable failure **(chosen)** | The method returns what the provider sent. A failure the caller must act on is a typed exception carrying what it must act on.            | —                                                                                                                                                                                                                                                                                                                                |
| The payload, and the transport library's own exception                          | One type fewer: let the HTTP library throw and have the caller read the detail off its exception object.                                  | The caller is then written against the transport library, which is the coupling the contract exists to stop — and the detail it needs arrives as a loosely-typed bag on an exception shaped for a different purpose. A recording transport throws nothing of the kind, so the caller's handling would be transport-specific too. |
| A discriminated union of the outcomes                                           | An abstract record with one sealed case per outcome, so both arms are plainly valid answers and there is no error connotation to misread. | Honest, and it survives the first objection — but it keeps the caller's two mechanisms, because the failures that stay exceptions are still exceptions. It also adds an idiom beside the one the repository already has, and C# cannot prove the switch exhaustive, so every consumer carries an unreachable arm.                |
| Keep the `Either`                                                               | No change; the library is already referenced and `Match` is total over its two named arms.                                                | The four objections in Context.                                                                                                                                                                                                                                                                                                  |

## Decision

**A provider contract returns the provider's successful payload, and signals
every failure by throwing.** The Failure/Result track begins on the application
side of that call.

1. **The contract is where I/O happens, and I/O in .NET fails by throwing.**
   Modelling failure in the return type at that boundary does not remove the
   exceptions — the transport library, the socket and the serializer all still
   throw — it adds a second channel beside them.
2. **The railway starts at the first component inside the application.** That
   component makes one decision about a failed call and expresses the result as
   a value from there upward, which is where `Option` and `Either` earn their
   place ([`language-ext-usage`](../../.skills/language-ext-usage/SKILL.md)).
3. **A failure the caller must act on gets its own exception type, declared
   with the contract**, carrying the value the caller acts on. Catching it is
   part of the contract's surface, so the type lives beside the interface. A
   caller then writes a specific `catch`, not a test on a status code it had to
   dig out of someone else's exception.
4. **A failure the caller cannot act on gets no type.** It propagates as
   whatever threw it and the application's one failure decision absorbs it.
   Inventing a type per status code is a taxonomy nobody reads.
5. **The contract declares only what every transport can produce.** A
   transport that cannot fail a given way simply never throws that exception,
   which costs it no declaration at all — unlike an arm in a return type.

## Consequences

- **`LanguageExt` leaves the contract layer and keeps its job above it.** That
  is the shape its own rule already asks for: a wrapper appears where it is
  carrying weight, and a signature that has to be learned before it is read is
  not carrying any.
- **A caller has one mechanism.** The component that absorbs a failed call
  catches, and every way the call can fail arrives the same way.
- **An expected outcome is signalled by an exception**, which is the cost of
  this. On a metered provider a throttle is routine rather than exceptional,
  and exceptions are not free. It is affordable because a poll is seconds
  apart, not because the objection is wrong — a boundary called in a tight loop
  would deserve a different answer, and this record does not give it one.
- **A `catch` written too broadly swallows a defect.** The specific exception
  type is what lets the handler be specific; a handler that catches everything
  to reach the throttle has silently taken the bugs with it.
- **It binds every provider contract**, not the one that prompted it — the
  recording transport, the vessel feed if it ever grows one, and any provider
  added after the talk.
- **One per-Feature answer is withdrawn.**
  [`src/Transporter/Integrations/OpenSky`](../../src/Transporter/Integrations/OpenSky/.spec/README.md)
  § 7 chose the `Either`; it now cites this record instead. The claim that put
  the question on the table, B-028, bans an exception without saying where, and
  that wording is § 11 question 2's to settle.
