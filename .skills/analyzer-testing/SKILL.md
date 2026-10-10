---
name: analyzer-testing
description: Testing this repository's Roslyn analyzers, code fixes and generators with Rocket.Surgery.Extensions.Testing.SourceGenerators — the builder, the assertions, the API that is not what the general skill says it is, and the rule that a diagnostic ships a fix or records why it cannot. Use when writing or reviewing a test for a TRN diagnostic.
---

# Analyzer testing

Extends the `source-generators` skill — not in this repository; it is the
agent-level one — for authoring and packaging, and
[`test-from-scenarios`](../test-from-scenarios/SKILL.md) for how a test is
named and shaped. This skill holds only what is specific to testing
[`Transporter.Analyzers`](../../src/Transporter.Analyzers/).

**Do not hand-roll a harness.**
`Rocket.Surgery.Extensions.Testing.SourceGenerators` compiles source text, runs
analyzers, code fixes and refactorings over it, and hands back the diagnostics.
Airframe tests the `RSA` rules this repository already runs with it, and
`src/Transporter.Analyzers/.spec/README.md` B-015 is why the test project is
the only one that references the analyzer as an assembly.

## The builder

```csharp
var results = await GeneratorTestContextBuilder
   .Create()
   .WithAnalyzer<BoundaryAnalyzer>()
   .WithDiagnosticSeverity(DiagnosticSeverity.Error)
   .AddSource("ViewModel.cs", source)
   .GenerateAsync();

results.AnalyzerResults[typeof(BoundaryAnalyzer)]
   .Diagnostics.Should().ContainSingle().Which.Id.Should().Be("TRN0001");
```

Every test builds its own context, the way Airframe's `Rsa####Tests` do. Pin
`WithDiagnosticSeverity(DiagnosticSeverity.Error)` in each: it is what B-006
makes the default and what a build sees. Sources live in
[`BoundaryTestData`](../../test/UnitTests/Analyzers/BoundaryTestData.cs), one
constant per layer.

| Need                                                 | API                                                                      |
| ---------------------------------------------------- | ------------------------------------------------------------------------ |
| Name a file, so `*.g.cs` makes a tree generated code | `AddSource(name, source)`                                                |
| Several sources                                      | `AddSources(params string[])`, or `AddSource` per file when names matter |
| An `.editorconfig` severity or option                | `AddGlobalOption(key, value)`, `AddOption(path, key, value)`             |
| Documentation comments visible to the analyzer       | `WithDocumentationMode(DocumentationMode.Parse)`                         |
| A code fix                                           | `WithCodeFix<TFix>()`, then `results.CodeFixResults`                     |
| A refactoring                                        | `WithCodeRefactoring<T>()`                                               |
| A generator                                          | `WithGenerator<T>()`, then `results.Results`                             |
| Language version, preprocessor symbols, references   | `WithLanguageVersion`, `AddPreprocessorSymbol`, `AddReferences`          |

## Traps, each one paid for

- **`AssertCompilationWasSuccessful()` is unusable here.** Every `TRN` rule
  defaults to `error` (B-006), so that assertion fails on exactly the diagnostic
  the test came to see. Guard on the compiler's own output instead —
  `results.InputDiagnostics` filtered to `Severity == Error` and an id starting
  `CS` — `NothingFailedToCompile` in the test class does that.
- **The `source-generators` skill's `references/testing.md` was wrong**, and
  was corrected on 2026-10-04. It documented
  `GeneratorTestContext.Create()`, `.WithSource()`, `result.VerifyAsync()` and
  `VerifySourceGenerators.Initialize()`. None exist. The real entry point is
  `GeneratorTestContextBuilder`. Read the package's own XML documentation before
  believing any example, this one included.
- **`AddMarkup` and `MarkedLocations` are not for diagnostic spans.** A
  `MarkedLocation` carries a completion `Trigger`; it is for refactorings and
  completion, not for "the diagnostic should land here". Assert a location by
  reading the text the span covers — `TextAt` in the test class —
  which also reads better than a line number.
- **`Verify` arrives whether or not it is used.** The package depends on
  `Verify.SourceGenerators`. This repository asserts with **AwesomeAssertions**
  and commits no snapshots: `transporter-conventions` references/testing.md
  fixes the stack, and a snapshot nobody reviews is a test that passes because
  it was regenerated.
- **The default reference set is not the platform.** `System.ObjectModel` is
  absent, and `ObservableCollection<T>` is forwarded there — so a source that
  binds a collection fails to compile and the rule under test reports nothing.
  `AddReferences(typeof(ObservableCollection<>))` fixes it, per test. A test that
  suddenly reports nothing is a compile failure before it is an analyzer bug,
  which is what the guard above exists to say out loud.
- **Several diagnostics in one document cross-attribute.** The builder can pair
  a resolved fix with code actions from a different diagnostic
  (`RocketSurgeonsGuild/Airframe#359`), so a fix test asserts the diagnostic
  count and the final text — not which rule each pass resolved.
- **Read a fix's result by applying the action, not off `Changes`.** A
  `ResolvedCodeFixTestResult` carries `CodeActions` _and_ a `Changes`, and with
  three diagnostics on one document all three `Changes` reported the same edit —
  the same cross-attribution as above. `TargetDocument` is the document the
  action was offered on and still reads as it did before. What is true is
  `action.CodeAction.GetOperationsAsync(...)`, whose `ApplyChangesOperation`
  holds that action's own solution.
- **An analyzer reports nothing while a declaration error stands.** Seeding a
  violation in the application to prove a rule in a real build only works if the
  seed compiles: reordering a contract's parameters reported `CS0535` and
  `CS0539` and no `TRN` at all, and the rule appeared the moment the
  implementation was reordered to match. A seed that breaks the build proves
  nothing about the rule.
- **A fix that only works in isolation is the common defect.** Apply one action
  per pass and re-analyze, the way an IDE does; Airframe's
  `AllDesignRulesFixedTests` is the worked example.

## A diagnostic ships a fix, or says why not

B-020. A rule whose compliant form is _determined_ by the claim gets a
`CodeFixProvider` in the same item that builds the rule. A rule whose compliant
form is a design decision gets a row in § 7 of the specification saying so.
Deleting a member, renaming a public type, or introducing a seam that does not
exist yet are decisions, not fixes.

A fix changes only what the claim requires, and applying it twice offers no
second change.

**The fixes are a project of their own**
([`src/Transporter.CodeFixes`](../../src/Transporter.CodeFixes/)), because a
`CodeFixProvider` needs `Microsoft.CodeAnalysis.Workspaces` and an analyzer must
not carry that into the compiler's load path. It takes **no project reference to
the analyzer** either — both sit in that load path from different directories, so
a reference between them is resolved by the host rather than by the build. The
diagnostic ids are literals there, and
`BoundaryCodeFixTests.GivenTheShippedFixes_WhenTheirFixableIdsAreRead_ThenEachIsOneTheSpecificationMarksFixable`
holds them to § 7's table rather than to hope.

**One diagnostic id can report several clauses**, so a fix decides whether to
offer from the syntax rather than from the message. `TRN0009` reports four
clauses of B-005 and only the misplaced token is mechanical; `TRN0011` reports
five clauses of B-007 and the second implementation per transport is not. A
provider that offers on the id alone offers a fix for a claim that has none.

## Never add

- A harness of your own, or a test that constructs `CSharpCompilation` itself.
- A `Verified`/snapshot directory, or `Verify` in a test's `using`s.
- A test name for a claim another specification's § 9 already named. That
  matrix fixes the name (B-016); renaming it to suit the implementation breaks
  the cite.
