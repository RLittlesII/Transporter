using System.Text.RegularExpressions;

namespace Transponder.UnitTests.Analyzers;

/// <summary>Reads a specification out of the repository, so a test can hold it to what it says.</summary>
/// <remarks>Three claims are about documents: B-008, B-016 and B-019. The analyzer's own harness is the package.</remarks>
internal static class SpecificationTables
{
    /// <summary>Selects § 7's code-fix table. The Fix cell is the discriminator: the claim mapping's rows open with a TRN id too.</summary>
    internal const string CodeFixRow = @"^\|\s*`TRN\d{4}`[^|]*\|\s*(Yes|No)\s*\|";

    /// <summary>Reads a repository file by its path from the repository root.</summary>
    /// <param name="relativePath">The path, relative to the repository root.</param>
    /// <returns>The file's text.</returns>
    internal static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Value, relativePath));

    /// <summary>The diagnostic ids a cell names, expanding a range written with an en dash.</summary>
    /// <param name="cell">The cell to read.</param>
    /// <returns>Every id the cell covers.</returns>
    internal static IEnumerable<string> Ids(string cell)
    {
        var named = Regex.Matches(cell, @"TRN(\d{4})").Select(static match => int.Parse(match.Groups[1].Value)).ToList();

        return cell.Contains('–') && named.Count == 2
            ? Enumerable.Range(named[0], named[1] - named[0] + 1).Select(static number => $"TRN{number:D4}")
            : named.Select(static number => $"TRN{number:D4}");
    }

    /// <summary>Reads a markdown table's rows as trimmed cells.</summary>
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
