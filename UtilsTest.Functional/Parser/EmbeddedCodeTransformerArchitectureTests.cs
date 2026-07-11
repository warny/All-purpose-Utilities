using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        string[] violations = Directory.GetFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsProductionParserSource)
            .SelectMany(file => FindDirectTransformCalls(repositoryRoot, file))
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
    /// Finds direct <c>.Transform(...)</c> invocation expressions in a source file.
    /// </summary>
    /// <param name="repositoryRoot">Absolute repository root path.</param>
    /// <param name="file">Absolute source file path.</param>
    /// <returns>Direct transform invocation occurrences found in the file.</returns>
    private static IEnumerable<SourceOccurrence> FindDirectTransformCalls(string repositoryRoot, string file)
    {
        string relativePath = Path.GetRelativePath(repositoryRoot, file);
        string source = File.ReadAllText(file);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: file);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.ValueText == "Transform")
            {
                FileLinePositionSpan lineSpan = invocation.SyntaxTree.GetLineSpan(invocation.Span);
                yield return new SourceOccurrence(
                    relativePath,
                    lineSpan.StartLinePosition.Line + 1,
                    invocation.ToString(),
                    GetEnclosingMethodName(invocation),
                    GetEnclosingTypeName(invocation),
                    memberAccess.Expression.ToString());
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
            && occurrence.EnclosingTypeName == "ParserEmbeddedCodeTransformationService"
            && occurrence.EnclosingMethodName == "TransformOrThrow"
            && occurrence.ReceiverExpression == "transformer";
    }

    /// <summary>
    /// Gets the nearest enclosing method name for a syntax node.
    /// </summary>
    /// <param name="node">Syntax node to inspect.</param>
    /// <returns>The enclosing method name, or <c>null</c> when none exists.</returns>
    private static string? GetEnclosingMethodName(SyntaxNode node)
    {
        return node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
    }

    /// <summary>
    /// Gets the nearest enclosing type name for a syntax node.
    /// </summary>
    /// <param name="node">Syntax node to inspect.</param>
    /// <returns>The enclosing type name, or <c>null</c> when none exists.</returns>
    private static string? GetEnclosingTypeName(SyntaxNode node)
    {
        return node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
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
    /// <param name="SourceText">Invocation expression source text.</param>
    /// <param name="EnclosingMethodName">Nearest enclosing method name.</param>
    /// <param name="EnclosingTypeName">Nearest enclosing type name.</param>
    /// <param name="ReceiverExpression">Invocation receiver expression.</param>
    private sealed record SourceOccurrence(
        string RelativePath,
        int LineNumber,
        string SourceText,
        string? EnclosingMethodName,
        string? EnclosingTypeName,
        string ReceiverExpression)
    {
        /// <summary>
        /// Formats the occurrence for assertion messages.
        /// </summary>
        /// <returns>A readable source occurrence.</returns>
        public override string ToString() => $"{RelativePath}:{LineNumber}: {SourceText}";
    }
}
