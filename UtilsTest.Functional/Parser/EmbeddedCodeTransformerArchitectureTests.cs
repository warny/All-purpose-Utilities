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
    private static readonly string CentralServiceRelativePath = Path.Combine("Utils.Parser.Diagnostics", "EmbeddedCode", "EmbeddedCodeText.cs");

    /// <summary>
    /// Ensures production parser code does not bypass the central embedded-code transformation service.
    /// </summary>
    [TestMethod]
    public void ProductionParserCode_WhenEmbeddedCodeTransformerIsUsed_DoesNotCallTransformOutsideCentralService()
    {
        string repositoryRoot = FindRepositoryRoot();

        string[] violations = Directory.GetFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsProductionParserSource)
            .SelectMany(file => FindForbiddenDirectTransformCallsInFile(repositoryRoot, file))
            .Select(static occurrence => occurrence.ToString())
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    /// <summary>
    /// Ensures the syntax scan catches a direct transform invocation split across multiple lines.
    /// </summary>
    [TestMethod]
    public void DirectTransformScan_WhenInvocationIsSplitAcrossLines_ReportsViolation()
    {
        const string source = """
            namespace Sample;

            internal sealed class Caller
            {
                public void Execute(IParserEmbeddedCodeTransformer transformer, ParserEmbeddedCodeTransformationContext context)
                {
                    transformer.
                        Transform(context);
                }
            }
            """;

        string[] violations = FindForbiddenDirectTransformCalls("Utils.Parser.Generators/Internal/Caller.cs", source)
            .Select(static occurrence => occurrence.ToString())
            .ToArray();

        Assert.AreEqual(1, violations.Length);
        StringAssert.StartsWith(violations[0], "Utils.Parser.Generators/Internal/Caller.cs:7: transformer.");
        StringAssert.Contains(violations[0], "\\n");
        StringAssert.Contains(violations[0], "Transform(context)");
    }

    /// <summary>
    /// Ensures the syntax scan does not allow arbitrary transform calls in the central service file.
    /// </summary>
    [TestMethod]
    public void DirectTransformScan_WhenCentralFileContainsUnexpectedTransformInvocation_ReportsViolation()
    {
        const string source = """
            namespace Utils.Parser.Diagnostics.EmbeddedCode;

            public static class ParserEmbeddedCodeTransformationService
            {
                public static void TransformOrThrow(IParserEmbeddedCodeTransformer transformer, ParserEmbeddedCodeTransformationContext context)
                {
                    transformer.Transform(context);
                }

                public static void Bypass(IParserEmbeddedCodeTransformer transformer, ParserEmbeddedCodeTransformationContext context)
                {
                    transformer.Transform(context);
                }
            }
            """;

        string[] violations = FindForbiddenDirectTransformCalls(CentralServiceRelativePath, source)
            .Select(static occurrence => occurrence.ToString())
            .ToArray();

        CollectionAssert.AreEqual(new[] { $"{CentralServiceRelativePath}:12: transformer.Transform(context)" }, violations);
    }

    /// <summary>
    /// Ensures embedded-code preparers do not introduce a second direct expression-compiler path.
    /// </summary>
    [TestMethod]
    public void ProductionEmbeddedCodePreparers_WhenCompileIsCalled_OnlyExpressionEmbeddedCodePreparerCompiles()
    {
        string repositoryRoot = FindRepositoryRoot();

        string[] violations = FindForbiddenEmbeddedCodePreparerCompileCalls(CreateProductionParserScans(repositoryRoot));

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    /// <summary>
    /// Ensures the embedded-code preparer compiler scan reports a second direct compiler path.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodePreparerCompileScan_WhenCustomPreparerCompiles_ReportsViolation()
    {
        const string source = """
            namespace Sample;

            using Utils.Expressions;
            using Utils.Parser.EmbeddedCode;

            internal sealed class OtherEmbeddedCodePreparer : IEmbeddedCodePreparer<object, object>
            {
                public void Prepare(IExpressionCompiler compiler)
                {
                    compiler.Compile("raw");
                }
            }
            """;

        string[] violations = FindForbiddenEmbeddedCodePreparerCompileCalls(CreateSampleExpressionCompilerScans("Utils.Parser.Expressions/OtherEmbeddedCodePreparer.cs", source));

        Assert.AreEqual(1, violations.Length);
        StringAssert.StartsWith(violations[0], "Utils.Parser.Expressions/OtherEmbeddedCodePreparer.cs:10: compiler.Compile(\"raw\")");
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
    /// Creates semantic scans for parser production sources plus the shared expression compiler contract.
    /// </summary>
    /// <param name="repositoryRoot">Absolute repository root path.</param>
    /// <returns>Semantic scans used by the architecture guard.</returns>
    private static IReadOnlyList<SourceScan> CreateProductionParserScans(string repositoryRoot)
    {
        string expressionCompilerPath = Path.Combine(repositoryRoot, "Utils", "Expressions", "IExpressionCompiler.cs");
        SyntaxTree[] trees = Directory.GetFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsProductionParserSource)
            .Concat([expressionCompilerPath])
            .Distinct(StringComparer.Ordinal)
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: NormalizePath(Path.GetRelativePath(repositoryRoot, file))))
            .ToArray();

        CSharpCompilation compilation = CSharpCompilation.Create(
            "Utils.Parser.EmbeddedCodeTransformer.ArchitectureScan",
            trees,
            CreateRuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return trees.Select(tree => new SourceScan(tree.FilePath, tree, compilation.GetSemanticModel(tree))).ToArray();
    }

    /// <summary>
    /// Creates semantic scans for a sample that references the embedded-code preparer and expression compiler contracts.
    /// </summary>
    /// <param name="relativePath">Source file path relative to the repository root.</param>
    /// <param name="source">Source text to parse.</param>
    /// <returns>Semantic scans for the supplied sample source.</returns>
    private static IReadOnlyList<SourceScan> CreateSampleExpressionCompilerScans(string relativePath, string source)
    {
        SyntaxTree sourceTree = CSharpSyntaxTree.ParseText(source, path: relativePath);
        SyntaxTree contractsTree = CSharpSyntaxTree.ParseText(
            """
            namespace Utils.Expressions
            {
                public interface IExpressionCompiler
                {
                    object Compile(string content, System.Collections.Generic.IReadOnlyDictionary<string, object>? symbols = null);
                }
            }

            namespace Utils.Parser.EmbeddedCode
            {
                public interface IEmbeddedCodePreparer<TPredicateArtifact, TActionArtifact>
                {
                }
            }
            """,
            path: "EmbeddedCodePreparerContracts.cs");

        CSharpCompilation compilation = CSharpCompilation.Create(
            "Utils.Parser.EmbeddedCodeTransformer.SampleArchitectureScan",
            [sourceTree, contractsTree],
            CreateRuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return [new SourceScan(relativePath, sourceTree, compilation.GetSemanticModel(sourceTree))];
    }

    /// <summary>
    /// Finds forbidden direct <c>IExpressionCompiler.Compile(...)</c> calls inside embedded-code preparer implementations.
    /// </summary>
    /// <param name="scans">Semantic source scans to inspect.</param>
    /// <returns>Forbidden direct compile invocation occurrences found in embedded-code preparers.</returns>
    private static string[] FindForbiddenEmbeddedCodePreparerCompileCalls(IReadOnlyList<SourceScan> scans)
    {
        Compilation compilation = scans[0].SemanticModel.Compilation;
        INamedTypeSymbol? preparerContract = compilation.GetTypeByMetadataName("Utils.Parser.EmbeddedCode.IEmbeddedCodePreparer`2");
        INamedTypeSymbol? expressionCompilerContract = compilation.GetTypeByMetadataName("Utils.Expressions.IExpressionCompiler");
        if (preparerContract is null || expressionCompilerContract is null)
        {
            Assert.Fail("Required embedded-code preparer or expression compiler contracts were not resolved by the architecture scan.");
        }

        return scans.SelectMany(scan => FindForbiddenEmbeddedCodePreparerCompileCalls(scan, preparerContract!, expressionCompilerContract!)).ToArray();
    }

    /// <summary>
    /// Finds forbidden direct <c>IExpressionCompiler.Compile(...)</c> calls inside one semantic source scan.
    /// </summary>
    /// <param name="scan">Semantic source scan to inspect.</param>
    /// <param name="preparerContract">Resolved embedded-code preparer contract.</param>
    /// <param name="expressionCompilerContract">Resolved expression compiler contract.</param>
    /// <returns>Forbidden direct compile invocation occurrences found in embedded-code preparers.</returns>
    private static IEnumerable<string> FindForbiddenEmbeddedCodePreparerCompileCalls(
        SourceScan scan,
        INamedTypeSymbol preparerContract,
        INamedTypeSymbol expressionCompilerContract)
    {
        CompilationUnitSyntax root = scan.Tree.GetCompilationUnitRoot();

        foreach (ClassDeclarationSyntax type in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (scan.SemanticModel.GetDeclaredSymbol(type) is not INamedTypeSymbol typeSymbol
                || !ImplementsGenericContract(typeSymbol, preparerContract)
                || IsAllowedExpressionCompilerPreparer(typeSymbol))
            {
                continue;
            }

            foreach (InvocationExpressionSyntax invocation in type.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (scan.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
                    && method.Name == "Compile"
                    && SymbolEqualityComparer.Default.Equals(method.ContainingType, expressionCompilerContract))
                {
                    FileLinePositionSpan lineSpan = invocation.SyntaxTree.GetLineSpan(invocation.Span);
                    yield return $"{scan.RelativePath}:{lineSpan.StartLinePosition.Line + 1}: {invocation}";
                }
            }
        }
    }

    /// <summary>
    /// Determines whether a type implements the expected generic embedded-code preparer contract.
    /// </summary>
    /// <param name="type">Type symbol to classify.</param>
    /// <param name="genericContract">Generic preparer contract definition.</param>
    /// <returns><see langword="true" /> when the type implements the contract.</returns>
    private static bool ImplementsGenericContract(INamedTypeSymbol type, INamedTypeSymbol genericContract)
    {
        return type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, genericContract));
    }

    /// <summary>
    /// Determines whether a preparer type is the approved runtime transformation-to-compilation boundary.
    /// </summary>
    /// <param name="type">Preparer type symbol to classify.</param>
    /// <returns><see langword="true" /> when direct expression compilation is allowed.</returns>
    private static bool IsAllowedExpressionCompilerPreparer(INamedTypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) == "Utils.Parser.Expressions.ExpressionEmbeddedCodePreparer";
    }

    /// <summary>
    /// Finds forbidden direct <c>.Transform(...)</c> invocation expressions in a source file.
    /// </summary>
    /// <param name="repositoryRoot">Absolute repository root path.</param>
    /// <param name="file">Absolute source file path.</param>
    /// <returns>Forbidden direct transform invocation occurrences found in the file.</returns>
    private static IEnumerable<SourceOccurrence> FindForbiddenDirectTransformCallsInFile(string repositoryRoot, string file)
    {
        string relativePath = Path.GetRelativePath(repositoryRoot, file);
        string source = File.ReadAllText(file);

        return FindForbiddenDirectTransformCalls(relativePath, source);
    }

    /// <summary>
    /// Finds forbidden direct <c>.Transform(...)</c> invocation expressions in source text.
    /// </summary>
    /// <param name="relativePath">Source file path relative to the repository root.</param>
    /// <param name="source">Source text to parse.</param>
    /// <returns>Forbidden direct transform invocation occurrences found in the source text.</returns>
    private static IEnumerable<SourceOccurrence> FindForbiddenDirectTransformCalls(string relativePath, string source)
    {
        return FindDirectTransformCalls(relativePath, source)
            .Where(static occurrence => !IsCentralServiceOccurrence(occurrence));
    }

    /// <summary>
    /// Finds direct <c>.Transform(...)</c> invocation expressions in source text.
    /// </summary>
    /// <param name="relativePath">Source file path relative to the repository root.</param>
    /// <param name="source">Source text to parse.</param>
    /// <returns>Direct transform invocation occurrences found in the source text.</returns>
    private static IEnumerable<SourceOccurrence> FindDirectTransformCalls(string relativePath, string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
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
        return occurrence.RelativePath == CentralServiceRelativePath
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
    /// Creates metadata references for semantic architecture scans.
    /// </summary>
    /// <returns>Trusted platform assembly references available to Roslyn.</returns>
    private static IEnumerable<MetadataReference> CreateRuntimeReferences()
    {
        string? trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trustedPlatformAssemblies is null)
        {
            yield return MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            yield return MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
            yield break;
        }

        HashSet<string> emitted = new(StringComparer.Ordinal);
        foreach (string path in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            if (emitted.Add(path))
            {
                yield return MetadataReference.CreateFromFile(path);
            }
        }
    }

    /// <summary>
    /// Normalizes paths to slash-separated relative paths.
    /// </summary>
    /// <param name="path">Path to normalize.</param>
    /// <returns>A slash-separated path.</returns>
    private static string NormalizePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>
    /// Represents one source file with the semantic model used by the architecture guard.
    /// </summary>
    /// <param name="RelativePath">Source file path relative to the repository root.</param>
    /// <param name="Tree">Parsed syntax tree.</param>
    /// <param name="SemanticModel">Semantic model for the syntax tree.</param>
    private sealed record SourceScan(string RelativePath, SyntaxTree Tree, SemanticModel SemanticModel);

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
        public override string ToString() => $"{RelativePath}:{LineNumber}: {SourceText.Replace("\r\n", "\\n").Replace("\n", "\\n")}";
    }
}
