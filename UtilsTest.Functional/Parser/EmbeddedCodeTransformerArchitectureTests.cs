using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UtilsTest.Parser;

/// <summary>
/// Validates repository-level embedded-code transformer architecture rules.
/// </summary>
[TestClass]
public sealed class EmbeddedCodeTransformerArchitectureTests
{
    /// <summary>
    /// Ensures production parser code does not bypass the central embedded-code transformation service.
    /// </summary>
    [TestMethod]
    public void ProductionParserCode_WhenEmbeddedCodeTransformerIsUsed_DoesNotCallTransformOutsideCentralService()
    {
        string repositoryRoot = FindRepositoryRoot();
        Regex directTransformCallPattern = new(@"\.\s*Transform\s*\(", RegexOptions.CultureInvariant);

        string[] violations = Directory.GetFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsProductionParserSource)
            .SelectMany(file => FindDirectTransformCalls(repositoryRoot, file, directTransformCallPattern))
            .Where(static occurrence => !IsCentralServiceOccurrence(occurrence))
            .Select(static occurrence => occurrence.ToString())
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    /// <summary>
    /// Finds the repository root from the functional test output folder.
    /// </summary>
    /// <returns>The absolute repository root path.</returns>
    private static string FindRepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;

        while (directory is not null && !File.Exists(Path.Combine(directory, "Utils.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return directory ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    /// <summary>
    /// Determines whether a file belongs to parser production sources scanned by this architecture test.
    /// </summary>
    /// <param name="file">Absolute source file path.</param>
    /// <returns><see langword="true" /> when the file is a production parser C# source file.</returns>
    private static bool IsProductionParserSource(string file)
    {
        return !ContainsDirectory(file, "bin")
            && !ContainsDirectory(file, "obj")
            && !ContainsDirectory(file, "UtilsTest")
            && !ContainsDirectory(file, "UtilsTest.Functional")
            && file.Contains($"{Path.DirectorySeparatorChar}Utils.Parser", StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds direct <c>.Transform(...)</c> call occurrences in a source file.
    /// </summary>
    /// <param name="repositoryRoot">Absolute repository root path.</param>
    /// <param name="file">Absolute source file path.</param>
    /// <param name="directTransformCallPattern">Pattern used to identify direct transform calls.</param>
    /// <returns>Direct transform call occurrences found in the file.</returns>
    private static IEnumerable<SourceOccurrence> FindDirectTransformCalls(string repositoryRoot, string file, Regex directTransformCallPattern)
    {
        string relativePath = Path.GetRelativePath(repositoryRoot, file);
        string[] lines = File.ReadAllLines(file);

        for (int index = 0; index < lines.Length; index++)
        {
            if (directTransformCallPattern.IsMatch(lines[index]))
            {
                yield return new SourceOccurrence(relativePath, index + 1, lines[index].Trim());
            }
        }
    }

    /// <summary>
    /// Determines whether a direct transform call is the single authorized central service invocation.
    /// </summary>
    /// <param name="occurrence">Source occurrence to classify.</param>
    /// <returns><see langword="true" /> when the occurrence is allowed.</returns>
    private static bool IsCentralServiceOccurrence(SourceOccurrence occurrence)
    {
        return occurrence.RelativePath == Path.Combine("Utils.Parser.Diagnostics", "EmbeddedCode", "EmbeddedCodeText.cs")
            && occurrence.SourceLine.Contains(".Transform(", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether an absolute path contains the specified directory segment.
    /// </summary>
    /// <param name="path">Absolute file path to inspect.</param>
    /// <param name="directoryName">Directory segment to search for.</param>
    /// <returns><see langword="true" /> when the path contains the directory segment.</returns>
    private static bool ContainsDirectory(string path, string directoryName)
    {
        return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(directoryName, StringComparer.Ordinal);
    }

    /// <summary>
    /// Describes a source-code occurrence found by the architecture scan.
    /// </summary>
    /// <param name="RelativePath">Source file path relative to the repository root.</param>
    /// <param name="LineNumber">One-based source line number.</param>
    /// <param name="SourceLine">Trimmed source line text.</param>
    private sealed record SourceOccurrence(string RelativePath, int LineNumber, string SourceLine)
    {
        /// <summary>
        /// Formats the occurrence for assertion messages.
        /// </summary>
        /// <returns>A readable source occurrence.</returns>
        public override string ToString() => $"{RelativePath}:{LineNumber}: {SourceLine}";
    }
}
