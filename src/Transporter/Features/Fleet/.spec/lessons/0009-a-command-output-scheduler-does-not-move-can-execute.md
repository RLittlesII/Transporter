---
title: "Lesson 0009: A command output scheduler does not move can-execute"
description: "A vessel swap changed command availability on an actor thread, leaving the native Refresh button visibly enabled until can-execute notifications moved to the UI scheduler."
type: lesson
---

# Lesson 0009: A command output scheduler does not move can-execute

**Date:** 2026-10-21
**Kind:** product
**Item:** `0102`
**Claim:** B-028

## Symptom

The running Mac Catalyst app switched to the vessel description, and the view
model and command both reported that Refresh was unavailable, but the native
button remained blue and accessible as enabled. The command's actor message
guard prevented a poll; the presentation still contradicted B-028.

## Root cause

The source selector published capability from the swap actor thread.
`RxCommand.Create` received that stream directly. Its output scheduler did not
schedule its can-execute input or `CanExecuteChanged` notification, so MAUI
changed the managed button state off the UI thread without redrawing the native
button. Binding `IsEnabled` to a second projected property alone did not repair
that event path. The same review found that a disabled direct `ICommand.Execute`
could leave a queued command result that reopened the indicator after a swap.

## Spec delta

Fleet dashboard B-028's design now requires the can-execute stream and the
bound capability property to notify on the injected UI scheduler. A separate
synchronous capability guard rejects a disabled direct press before it tells
the actor. Only an accepted action emits the indicator gesture; queued command
output cannot reopen a window after a source round trip.

## Claim and evidence

B-028 is exercised by `FleetRefreshTests` for the UI-scheduled
`CanExecuteChanged`, rejected and accepted queued presses, and aircraft's
existing no-reply gesture. On the running Mac Catalyst app, Vessels displayed
Refresh disabled, live aircraft enabled it on return, and recorded aircraft
displayed it disabled beside twelve synthetic replay cards. No provider was
contacted in that check.
