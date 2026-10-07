---
title: "Lesson 0002: A hand-rolled command, and a default projected down the pipeline"
description: "PR #52's review on the refresh control: the repository already had RxCommand, which is its own observable, so the gesture needed neither a command class nor a subject; AsValue takes an initial value and a scheduler rather than StartWith and ObserveOn; a binder is disposed with the view model; and a test never passes ImmediateScheduler."
type: lesson
---

# Lesson 0002: A hand-rolled command, and a default projected down the pipeline

**Item:** [`0058`](../../.issue/0058-refresh-control.yml) · **Claim:** B-028 ·
**Found by:** the person, reviewing pull request #52 on 2026-10-07

## What happened

`0058` built the refresh gesture and shipped five findings' worth of avoidable
code. The review named each:

1. **`GestureCommand` should not exist.** "This whole class was generated for no
   reason." The repository's own ecosystem has `RxCommand`
   (`ReactiveMarbles.Command`), and the point is not that it saves a file: an
   `RxCommand` **is** an `IObservable` of its own executions. The hand-rolled
   `ICommand` forced a `Subject<Unit>` beside it so the indicator's stream had
   something to listen to — a subject the view model's own remarks claimed it did
   not hold.
2. **`StartWith(false)` projects a value down the pipeline** to answer a question
   `AsValue` already answers: it takes a `Func<T>` for the initial value, and an
   `IScheduler` for where the value surfaces, so the `ObserveOn` above it went too.
3. **The binder is disposed with the view model**, `DisposeWith(_garbage)` like
   every other subscription here, rather than by a hand-written `Dispose` that
   type-tested the interface it was handed back as.
4. **A test never passes `ImmediateScheduler.Instance`.** Three composition tests
   did, from `0047`; they pass a `TestScheduler` now.

## Why it slipped

Nothing in the specification or the skills said which command type to use,
because nothing in this repository had a command before. `mvvm` said "the
commands the view invokes" and showed `registry.Get<SourceActor>().Tell(...)`,
which is the _body_ of a command and not its type — so the first implementation
invented one, and the subject followed from the invention rather than from the
design. The `AsValue` overloads were read as far as the one that compiled.

## The rule now written down

`mvvm` § "Talking to an actor" and § "Projecting state back" carry all of it: a
command is an `RxCommand` and is its own observable, so a view model needs no
subject to watch its own gestures; its output scheduler is passed, because an
unset one is read from ReactiveMarbles' process-wide locator and the composition
root is what writes that locator; `AsValue` takes the initial value and the
scheduler; and the binder is disposed with the view model.
`test-from-scenarios` carries the fourth: the only scheduler a test passes is a
`TestScheduler`, because an immediate one hides the marshalling a claim is about.

## What it cost

One class deleted, one subject deleted, four lines of stream replaced by three,
and three test arrangements changed. No claim moved and no behaviour changed —
every one of B-028's five tests passed before and after, which is the part worth
noticing: a green suite says the gesture works, and says nothing about whether
the repository already had the thing you wrote.
