using System.Text.RegularExpressions;

namespace Transponder.UnitTests.Analyzers;

/// <summary>
/// Reads a specification out of the repository so a test can hold it to what it says.
/// <para>
/// Not a test harness: the analyzer's own harness is
/// <c>Rocket.Surgery.Extensions.Testing.SourceGenerators</c>. These two helpers exist because
/// three claims are about documents rather than about code — the mapping table in
/// <c>features/boundary-analyzer/.spec/README.md</c> § 7 being the only store for which diagnostic
/// enforces which claim (B-019), the set it carries being exactly the assigned one (B-008), and
/// the test names <c>features/aircraft-source/.spec/README.md</c> § 9 fixes (B-016).
/// </para>
/// </summary>
internal static class SpecificationTables
{
    /// <summary>
    /// Reads a repository file by its path from the repository root.
    /// </summary>
    /// <param name="relativePath">The path, relative to the repository root.</param>
    /// <returns>The file's text.</returns>
    internal static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Value, relativePath));

    /// <summary>
    /// Reads the rows of a markdown table as their already-trimmed cells.
    /// </summary>
    /// <param name="markdown">The markdown to read.</param>
    /// <param name="rowPattern">A pattern the row must match to be taken.</param>
    /// <returns>One string array of cells per matching row.</returns>
    internal static IReadOnlyList<string[]> Rows(string markdown, string rowPattern) =>
        markdown
            .Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith('|') && Regex.IsMatch(line, rowPattern))
            .Select(static line => line.Trim('|').Split('|').Select(static cell => cell.Trim()).ToArray())
            .ToList();

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
