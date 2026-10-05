using Microsoft.CodeAnalysis;
using Rocket.Surgery.Extensions.Testing.SourceGenerators;
using Transponder.Analyzers;

namespace Transponder.UnitTests.Analyzers;

/// <summary>
/// Runs <see cref="BoundaryAnalyzer"/> over named sources with
/// <c>Rocket.Surgery.Extensions.Testing.SourceGenerators</c>, the harness Airframe tests its own
/// RSA rules with.
/// <para>
/// A source's name is its file name, so a source named <c>*.g.cs</c> is generated code as far as
/// Roslyn is concerned — which is how B-005 is arranged rather than asserted. Severity is pinned
/// to <see cref="DiagnosticSeverity.Error"/> because that is what B-006 makes the default and what
/// a build would see.
/// </para>
/// </summary>
internal static class BoundaryAnalyzerContext
{
    /// <summary>
    /// Analyzes the given sources and returns what the boundary analyzer reported.
    /// </summary>
    /// <param name="sources">Each source, by file name.</param>
    /// <returns>The analyzer's diagnostics.</returns>
    internal static async Task<IReadOnlyList<Diagnostic>> Analyze(params (string FileName, string Source)[] sources)
    {
        var builder = sources.Aggregate(
            GeneratorTestContextBuilder.Create().WithAnalyzer<BoundaryAnalyzer>().WithDiagnosticSeverity(DiagnosticSeverity.Error),
            static (current, source) => current.AddSource(source.FileName, source.Source));

        var results = await builder.GenerateAsync();

        // A diagnostic reported over source that does not compile proves nothing. The guard reads
        // the compiler's own diagnostics rather than calling AssertCompilationWasSuccessful: every
        // TRN rule defaults to Error (B-006), so that assertion fails on exactly the diagnostic
        // the test came to see.
        var broken = results.InputDiagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id.StartsWith("CS", StringComparison.Ordinal))
            .ToArray();

        if (broken.Length > 0)
        {
            throw new InvalidOperationException(
                "The source under analysis does not compile, so nothing the analyzer reported can be trusted: "
                + string.Join("; ", broken.Select(static diagnostic => diagnostic.ToString())));
        }

        return results.TryGetAnalyzerResult<BoundaryAnalyzer>(out var analyzed) ? analyzed.Diagnostics : [];
    }

    /// <summary>
    /// The source text the diagnostic's span covers — what the reader's caret lands on.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to read.</param>
    /// <returns>The text at the diagnostic's location.</returns>
    internal static string TextAt(this Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree is { } tree
            ? tree.GetText().ToString(diagnostic.Location.SourceSpan)
            : string.Empty;
}
