---
title: "Decision 0004: The credentials live in the user-secrets store, packaged at build"
description: "The OpenSky client id and secret are kept in the developer's user-secrets store and layered over the packaged settings, which reverses part of an earlier call that nothing machine-local is layered over them."
type: decision
---

# Decision 0004: The credentials live in the user-secrets store, packaged at build

**Status:** decided

**Date:** 2026-10-09
**Made by:** the person

## The call

The OpenSky client id and client secret are kept in the `dotnet user-secrets`
store, under the id the head project declares. The head project packages the
store beside its settings when it is built, and the application's configuration
is the packaged settings with the store layered over them. B-058 claims it.

## Why

The account was registered and the API client created on 2026-10-09, and the
application had nowhere to read the pair from: § 4 row 12 had always said where
credentials come from and no claim made anything read them. Asked where they
should live, the person chose the store.

The store is one file in the home directory, so every worktree builds with the
same pair and no file holding it sits where git would stage it. It is packaged
at build rather than read at run because a sandboxed head cannot open the home
directory.

## Rejected

**A git-ignored `appsettings.Development.json` beside the packaged settings.**
The smaller change: the name is already ignored and the folder is already
packaged whole. Rejected because it needs a copy per worktree, keeps the secret
in the source tree one ignore rule away from a commit, and rewords § 4 row 12.

**Environment variables.** A head launched from Finder or a simulator never
sees the shell's environment, so they would have to be written into an asset at
build anyway — the same packaging, with a less durable place to keep the value.

## Affects

- B-058 — new.
- § 4 row 12 — unchanged, and now has a claim under it.
- § 5 row 19 — new: a bundle built beside the store holds the secret and is
  never handed to anyone.
- `0047`'s second decision, 2026-10-06 — **reversed in part**. It decided that
  configuration ships as an asset carrying no credential, and rejected
  credentials layered over that asset because "a developer's own machine" would
  become "the only place the application behaves one way". That objection
  applies to the store exactly as it did to the environment, and this call
  accepts it: a machine holding the store polls a live provider and one without
  it is refused. What stands is that the packaged settings carry no credential
  and a clone with no store starts as it did.

## Reversal

Not applicable — this record has not been reversed.
