---
title: "Lesson 0026: A step the reader must perform is documented as steps"
description: "0086 made the application read its credentials from a store on the developer's machine and said how to fill it in one bullet under the provider's description; PR #89's review asked where the documentation was."
type: lesson
---

# Lesson 0026: A step the reader must perform is documented as steps

**Date:** 2026-10-10
**Kind:** process

## Symptom

`0086` made the application read the OpenSky credentials from the developer's
user-secrets store. Until someone fills that store the change does nothing, so
filling it is the one thing the change asks of the person who merges it. Pull
request #89 was reviewed with a single question: "Documentation on how I get
secrets _into_ the store?"

There was documentation. It was two commands in the last bullet of `README.md`
§ "Authentication", a subsection of the provider's description, three headings
above § "Build" — which is where a reader goes to run the application, and
which said nothing. The specification, a decision record, a lesson and a
runbook line all explained why the store is used, and none said what to type.

One of the two sentences that did was wrong. It said the tool prints the value
it saves; on the pinned SDK it does not, and the command that does print both
values, `list`, was not mentioned at all. Nobody had run the commands to write
them down.

## Root cause

The change was documented as what it is rather than as what it asks. Every
record the delivery method names was written — claim, scenario, decision,
lesson — and each of those is addressed to someone reading the system. None is
addressed to someone who has to act, and the method had no step that asks
whether a change leaves the reader with something to do.

So the instruction was fitted into the nearest existing paragraph about the
subject, sized as a fact about the provider. It had no check, no way back, and
no account of what goes wrong, because a bullet has room for none of them.

## Spec delta

No delta to a claim. `docs/credentials.md` is the steps — get the pair, put it
in the store, check it without printing it, build, rotate or remove, and what
to look at when the fleet stays empty. `README.md` § "Build" links it where a
reader runs the application, § "Authentication" links it and no longer says the
tool prints the value, and the runbook links it.

## Claim

- None. The instruction is documentation of a build-machine action, and B-058's
  § 9 row already names the launch that would show the steps worked. Every
  command in the guide that does not touch the provider or launch the head was
  run against a throwaway store holding invented values.

## Skill

[`deliver-change`](../../.skills/deliver-change/references/document.md)
§ "Steps a reader must perform": a change that works only after someone does
something on their own machine ships those steps as steps, linked from where
that person starts, each command run before it is written down.
