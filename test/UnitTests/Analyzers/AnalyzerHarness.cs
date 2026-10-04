using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Transponder.UnitTests.Analyzers;

/// <summary>
/// Compiles source text in process and runs an analyzer over it.
/// <para>
/// The seam under test is the source, so the harness takes it as a string. A file named
/// <c>*.g.cs</c> is generated code as far as Roslyn is concerned, which is how the
/// generated-code claim is arranged rather than asserted.
/// </para>
/// </summary>
internal static class AnalyzerHarness
{
    /// <summary>
    /// Runs an analyzer over one or more named sources and returns what it reported.
    /// </summary>
    /// <param name="analyzer">The analyzer to run.</param>
    /// <param name="sources">Each source, by file name.</param>
    /// <returns>The analyzer's diagnostics, in file and position order.</returns>
    internal static async Task<ImmutableArray<Diagnostic>> Analyze(
        DiagnosticAnalyzer analyzer,
        params (string FileName, string Source)[] sources)
    {
        var trees = sources
            .Select(static source => CSharpSyntaxTree.ParseText(
                source.Source,
                new CSharpParseOptions(LanguageVersion.Latest),
                source.FileName))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "Analyzed",
            trees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();

        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                "The source under analysis does not compile, so nothing the analyzer reported can be trusted: "
                + string.Join("; ", errors.Select(static error => error.ToString())));
        }

        return await compilation
            .WithAnalyzers(ImmutableArray.Create(analyzer))
            .GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>
    /// Reads a repository file by its path from the repository root, so a test can hold a
    /// specification to what it says.
    /// </summary>
    /// <param name="relativePath">The path, relative to the repository root.</param>
    /// <returns>The file's text.</returns>
    internal static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Value, relativePath));

    /// <summary>
    /// Reads the rows of one markdown table — the lines between a header separator and the first
    /// blank line — as their already-trimmed cells.
    /// </summary>
    /// <param name="markdown">The markdown to read.</param>
    /// <param name="rowPattern">A pattern the row must match to be taken.</param>
    /// <returns>One string array of cells per matching row.</returns>
    internal static IReadOnlyList<string[]> TableRows(string markdown, string rowPattern) =>
        markdown
            .Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith('|') && Regex.IsMatch(line, rowPattern))
            .Select(static line => line.Trim('|').Split('|').Select(static cell => cell.Trim()).ToArray())
            .ToList();

    private static readonly ImmutableArray<MetadataReference> References = ((string?) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
        is { } assemblies
        ? assemblies
            .Split(Path.PathSeparator)
            .Where(static path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(static path => (MetadataReference) MetadataReference.CreateFromFile(path))
            .ToImmutableArray()
        : [];

    private static readonly Lazy<string> RepositoryRoot = new(static () =>
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Transponder.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No Transponder.slnx above the test assembly, so the repository root cannot be found.");
    });
}
