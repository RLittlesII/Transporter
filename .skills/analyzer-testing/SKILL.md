---
name: analyzer-testing
description: Testing this repository's Roslyn analyzers, code fixes and generators with Rocket.Surgery.Extensions.Testing.SourceGenerators — the builder, the assertions, the API that is not what the general skill says it is, and the rule that a diagnostic ships a fix or records why it cannot. Use when writing or reviewing a test for a TRN diagnostic.
---

# Analyzer testing

Extends the `source-generators` skill — not in this repository; it is the
agent-level one — for authoring and packaging, and
[`test-from-scenarios`](../test-from-scenarios/SKILL.md) for how a test is
named and shaped. This skill holds only what is specific to testing
[`Transponder.Analyzers`](../../src/Transponder.Analyzers/).

**Do not hand-roll a harness.**
`Rocket.Surgery.Extensions.Testing.SourceGenerators` compiles source text, runs
analyzers, code fixes and refactorings over it, and hands back the diagnostics.
Airframe tests the `RSA` rules this repository already runs with it, and
`features/boundary-analyzer/.spec/README.md` B-015 is why the test project is
the only one that references the analyzer as an assembly.

## The builder

```csharp
var results = await GeneratorTestContextBuilder
   .Create()
   .WithAnalyzer<BoundaryAnalyzer>()
   .WithDiagnosticSeverity(DiagnosticSeverity.Error)
   .AddSource("ViewModel.cs", source)
   .GenerateAsync();

results.TryGetAnalyzerResult<BoundaryAnalyzer>(out var analyzed);
analyzed.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("TRN0001");
```

[`BoundaryAnalyzerContext`](../../test/UnitTests/Analyzers/BoundaryAnalyzerContext.cs)
wraps those four lines for the boundary rules. Use it rather than repeating
them; a test that builds its own context is a test that can disagree about
severity.

| Need | API |
|---|---|
| Name a file, so `*.g.cs` makes a tree generated code | `AddSource(name, source)` |
| Several sources | `AddSources(params string[])`, or `AddSource` per file when names matter |
| An `.editorconfig` severity or option | `AddGlobalOption(key, value)`, `AddOption(path, key, value)` |
| Documentation comments visible to the analyzer | `WithDocumentationMode(DocumentationMode.Parse)` |
| A code fix | `WithCodeFix<TFix>()`, then `results.CodeFixResults` |
| A refactoring | `WithCodeRefactoring<T>()` |
| A generator | `WithGenerator<T>()`, then `results.Results` |
| Language version, preprocessor symbols, references | `WithLanguageVersion`, `AddPreprocessorSymbol`, `AddReferences` |

## Traps, each one paid for

- **`AssertCompilationWasSuccessful()` is unusable here.** Every `TRN` rule
  defaults to `error` (B-006), so that assertion fails on exactly the diagnostic
  the test came to see. Guard on the compiler's own output instead —
  `results.InputDiagnostics` filtered to `Severity == Error` and an id starting
  `CS` — which is what `BoundaryAnalyzerContext` does.
- **The `source-generators` skill's `references/testing.md` was wrong**, and
  was corrected on 2026-10-04. It documented
  `GeneratorTestContext.Create()`, `.WithSource()`, `result.VerifyAsync()` and
  `VerifySourceGenerators.Initialize()`. None exist. The real entry point is
  `GeneratorTestContextBuilder`. Read the package's own XML documentation before
  believing any example, this one included.
- **`AddMarkup` and `MarkedLocations` are not for diagnostic spans.** A
  `MarkedLocation` carries a completion `Trigger`; it is for refactorings and
  completion, not for "the diagnostic should land here". Assert a location by
  reading the text the span covers — see `TextAt` in `BoundaryAnalyzerContext` —
  which also reads better than a line number.
- **`Verify` arrives whether or not it is used.** The package depends on
  `Verify.SourceGenerators`. This repository asserts with **AwesomeAssertions**
  and commits no snapshots: `transponder-conventions` § `test-from-scenarios`
  fixes the stack, and a snapshot nobody reviews is a test that passes because
  it was regenerated.
- **The default reference set is not the platform.** `System.ObjectModel` is
  absent, and `ObservableCollection<T>` is forwarded there — so a source that
  binds a collection fails to compile and the rule under test reports nothing.
  `AddReferences(typeof(ObservableCollection<>))` fixes it;
  `BoundaryAnalyzerContext` carries it for every run. A test that suddenly
  reports nothing is a compile failure before it is an analyzer bug, which is
  what the guard above exists to say out loud.
- **Several diagnostics in one document cross-attribute.** The builder can pair
  a resolved fix with code actions from a different diagnostic
  (`RocketSurgeonsGuild/Airframe#359`), so a fix test asserts the diagnostic
  count and the final text — not which rule each pass resolved.
- **A fix that only works in isolation is the common defect.** Apply one action
  per pass and re-analyze, the way an IDE does; Airframe's
  `AllDesignRulesFixedTests` is the worked example.

## A diagnostic ships a fix, or says why not

B-020. A rule whose compliant form is *determined* by the claim gets a
`CodeFixProvider` in the same item that builds the rule. A rule whose compliant
form is a design decision gets a row in § 7 of the specification saying so.
Deleting a member, renaming a public type, or introducing a seam that does not
exist yet are decisions, not fixes.

A fix changes only what the claim requires, and applying it twice offers no
second change.

## Never add

- A second harness, or a test that constructs `CSharpCompilation` itself.
- A `Verified`/snapshot directory, or `Verify` in a test's `using`s.
- A test name for a claim another specification's § 9 already named. That
  matrix fixes the name (B-016); renaming it to suit the implementation breaks
  the cite.
