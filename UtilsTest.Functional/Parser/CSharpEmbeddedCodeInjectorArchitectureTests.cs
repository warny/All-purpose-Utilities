using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UtilsTest.Parser;

/// <summary>
/// Validates that generated C# embedded-code injection stays centralized in CSharpEmbeddedCodeInjector.
/// </summary>
[TestClass]
public sealed class CSharpEmbeddedCodeInjectorArchitectureTests
{
    private static readonly string InjectorPath = NormalizePath(Path.Combine("Utils.Parser.Generators", "Internal", "CSharpEmbeddedCodeInjector.cs"));

    /// <summary>
    /// Ensures production generator sources do not append embedded-code text directly outside the injector.
    /// </summary>
    [TestMethod]
    public void GeneratorProductionSources_WhenAppendingEmbeddedCode_DoSoOnlyThroughInjector()
    {
        string repositoryRoot = FindRepositoryRoot();
        string generatorRoot = Path.Combine(repositoryRoot, "Utils.Parser.Generators");

        string[] violations = FindForbiddenEmbeddedCodeWrites(CreateProductionGeneratorScans(repositoryRoot, generatorRoot));

        Assert.AreEqual(0, violations.Length, string.Join(Environment.NewLine, violations));
    }

    /// <summary>Ensures transformed text cannot be hidden in an innocently named local before appending.</summary>
    [TestMethod]
    public void EmbeddedCodeWriteGuard_WhenTransformedTextIsStoredInLocalWithoutCodeName_FlagsAppendLine()
    {
        const string Source = """
            using System.Text;
            using Utils.Parser.Diagnostics.EmbeddedCode;

            internal sealed class Sample
            {
                private static void Emit(StringBuilder builder, TransformedEmbeddedCode transformed)
                {
                    string text = transformed.Text;
                    builder.AppendLine(text);
                }
            }
            """;

        string[] violations = FindForbiddenEmbeddedCodeWrites("Sample.cs", Source);

        Assert.IsTrue(
            violations.Any(static violation => violation.Contains("Transformed embedded-code text reaches AppendLine", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Ensures the targeted GrammarEmitter methods reference the centralized injector.
    /// </summary>
    [TestMethod]
    public void TargetedGrammarEmitterMethods_WhenInspected_UseCSharpEmbeddedCodeInjector()
    {
        string repositoryRoot = FindRepositoryRoot();
        string generatorRoot = Path.Combine(repositoryRoot, "Utils.Parser.Generators");
        string[] requiredMethods =
        [
            "EmitParserHeaders",
            "EmitParserMembers",
            "EmitParserFooters",
            "EmitLexerHeaders",
            "EmitLexerMembers",
            "EmitLexerFooters",
            "EmitGeneratedEmbeddedCodeBody"
        ];

        Dictionary<string, bool> methodUses = requiredMethods.ToDictionary(static method => method, static _ => false);
        foreach (string file in Directory.GetFiles(generatorRoot, "GrammarEmitter*.cs", SearchOption.AllDirectories))
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            foreach (MethodDeclarationSyntax method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                string name = method.Identifier.ValueText;
                if (methodUses.ContainsKey(name)
                    && method.DescendantNodes().OfType<IdentifierNameSyntax>().Any(static identifier => identifier.Identifier.ValueText == "CSharpEmbeddedCodeInjector"))
                {
                    methodUses[name] = true;
                }
            }
        }

        string[] missing = methodUses.Where(static pair => !pair.Value).Select(static pair => pair.Key).ToArray();
        Assert.AreEqual(0, missing.Length, string.Join(Environment.NewLine, missing));
    }

    /// <summary>Creates semantic scans for all production generator source files.</summary>
    private static IReadOnlyList<SourceScan> CreateProductionGeneratorScans(string repositoryRoot, string generatorRoot)
    {
        SyntaxTree[] trees = Directory.GetFiles(generatorRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static file => !ContainsDirectory(file, "bin") && !ContainsDirectory(file, "obj"))
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: NormalizePath(Path.GetRelativePath(repositoryRoot, file))))
            .ToArray();

        CSharpCompilation compilation = CSharpCompilation.Create(
            "Utils.Parser.Generators.ArchitectureScan",
            trees,
            CreateRuntimeReferences().Concat([MetadataReference.CreateFromFile(FindParserDiagnosticsAssembly(repositoryRoot))]),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return trees.Select(tree => new SourceScan(tree.FilePath, tree, compilation.GetSemanticModel(tree))).ToArray();
    }

    /// <summary>Finds forbidden embedded-code writes in a single source sample.</summary>
    private static string[] FindForbiddenEmbeddedCodeWrites(string relativePath, string source)
    {
        SyntaxTree sourceTree = CSharpSyntaxTree.ParseText(source, path: relativePath);
        SyntaxTree embeddedCodeStubTree = CSharpSyntaxTree.ParseText(
            """
            namespace Utils.Parser.Diagnostics.EmbeddedCode
            {
                public sealed class TransformedEmbeddedCode
                {
                    public string Text { get; } = string.Empty;
                }

                public sealed class RawEmbeddedCode
                {
                    public string Text { get; } = string.Empty;
                }
            }
            """,
            path: "EmbeddedCodeStubs.cs");

        CSharpCompilation compilation = CSharpCompilation.Create(
            "EmbeddedCodeArchitectureSample",
            [sourceTree, embeddedCodeStubTree],
            CreateRuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return FindForbiddenEmbeddedCodeWrites([new SourceScan(relativePath, sourceTree, compilation.GetSemanticModel(sourceTree))]);
    }

    /// <summary>Finds forbidden embedded-code writes in generator source scans.</summary>
    private static string[] FindForbiddenEmbeddedCodeWrites(IReadOnlyList<SourceScan> scans)
    {
        return scans.SelectMany(FindForbiddenEmbeddedCodeWrites).ToArray();
    }

    /// <summary>Finds forbidden embedded-code writes in one source scan.</summary>
    private static IEnumerable<string> FindForbiddenEmbeddedCodeWrites(SourceScan scan)
    {
        if (string.Equals(scan.RelativePath, InjectorPath, StringComparison.Ordinal))
        {
            yield break;
        }

        CompilationUnitSyntax root = scan.Tree.GetCompilationUnitRoot();
        Dictionary<ISymbol, EmbeddedCodeTextKind> taintedLocals = new(SymbolEqualityComparer.Default);
        foreach (VariableDeclaratorSyntax variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (variable.Initializer is not null
                && TryGetEmbeddedCodeTextKind(scan, variable.Initializer.Value, out EmbeddedCodeTextKind kind)
                && !IsAllowedEmbeddedCodeTextRead(scan.RelativePath, variable.Initializer.Value)
                && scan.SemanticModel.GetDeclaredSymbol(variable) is ILocalSymbol local)
            {
                taintedLocals[local] = kind;
            }
        }

        foreach (AssignmentExpressionSyntax assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && TryGetEmbeddedCodeTextKind(scan, assignment.Right, out EmbeddedCodeTextKind kind)
                && !IsAllowedEmbeddedCodeTextRead(scan.RelativePath, assignment.Right)
                && scan.SemanticModel.GetSymbolInfo(assignment.Left).Symbol is ILocalSymbol local)
            {
                taintedLocals[local] = kind;
            }
        }

        foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string? methodName = GetInvokedMethodName(invocation);
            if (methodName is not "Append" and not "AppendLine")
            {
                continue;
            }

            foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
            {
                EmbeddedCodeTextKind? directKind = FindDirectEmbeddedCodeTextRead(scan, argument.Expression);
                if (directKind is not null)
                {
                    yield return $"{scan.RelativePath}:{GetLine(invocation)}: {directKind.Value} embedded-code text reaches {methodName}: {invocation}";
                    continue;
                }

                EmbeddedCodeTextKind? taintedKind = FindTaintedLocalRead(scan, argument.Expression, taintedLocals);
                if (taintedKind is not null)
                {
                    yield return $"{scan.RelativePath}:{GetLine(invocation)}: {taintedKind.Value} embedded-code text reaches {methodName}: {invocation}";
                }
            }
        }

        foreach (MemberAccessExpressionSyntax access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (TryGetEmbeddedCodeTextKind(scan, access, out EmbeddedCodeTextKind kind)
                && kind == EmbeddedCodeTextKind.Transformed
                && !IsAllowedEmbeddedCodeTextRead(scan.RelativePath, access))
            {
                yield return $"{scan.RelativePath}:{GetLine(access)}: {kind} embedded-code text read outside CSharpEmbeddedCodeInjector: {access}";
            }
        }
    }

    /// <summary>Finds a direct embedded-code text read inside an append argument.</summary>
    private static EmbeddedCodeTextKind? FindDirectEmbeddedCodeTextRead(SourceScan scan, ExpressionSyntax expression)
    {
        foreach (MemberAccessExpressionSyntax access in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            if (TryGetEmbeddedCodeTextKind(scan, access, out EmbeddedCodeTextKind kind)
                && !IsAllowedEmbeddedCodeTextRead(scan.RelativePath, access))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>Finds a local whose value originated from embedded-code text inside an append argument.</summary>
    private static EmbeddedCodeTextKind? FindTaintedLocalRead(SourceScan scan, ExpressionSyntax expression, IReadOnlyDictionary<ISymbol, EmbeddedCodeTextKind> taintedLocals)
    {
        foreach (IdentifierNameSyntax identifier in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (scan.SemanticModel.GetSymbolInfo(identifier).Symbol is ISymbol symbol
                && taintedLocals.TryGetValue(symbol, out EmbeddedCodeTextKind kind))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>Determines whether an expression reads embedded-code text that must not be appended directly.</summary>
    private static bool TryGetEmbeddedCodeTextKind(SourceScan scan, ExpressionSyntax expression, out EmbeddedCodeTextKind kind)
    {
        kind = default;
        if (expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        string memberName = access.Name.Identifier.ValueText;
        if (memberName == "SourceText")
        {
            kind = EmbeddedCodeTextKind.Source;
            return true;
        }

        if (memberName != "Text")
        {
            return false;
        }

        ITypeSymbol? receiverType = scan.SemanticModel.GetTypeInfo(access.Expression).Type;
        string receiverTypeName = receiverType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        if (receiverTypeName == "Utils.Parser.Diagnostics.EmbeddedCode.TransformedEmbeddedCode")
        {
            kind = EmbeddedCodeTextKind.Transformed;
            return true;
        }

        if (receiverTypeName == "Utils.Parser.Diagnostics.EmbeddedCode.RawEmbeddedCode")
        {
            kind = EmbeddedCodeTextKind.Raw;
            return true;
        }

        return false;
    }

    /// <summary>Determines whether an embedded-code text read is classification-only rather than generated-source injection.</summary>
    private static bool IsAllowedEmbeddedCodeTextRead(string relativePath, SyntaxNode node)
    {
        MethodDeclarationSyntax? method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        return string.Equals(relativePath, NormalizePath(Path.Combine("Utils.Parser.Generators", "Internal", "GrammarEmitter.ExecutionContext.Hooks.cs")), StringComparison.Ordinal)
            && string.Equals(method?.Identifier.ValueText, "ForPredicate", StringComparison.Ordinal);
    }

    /// <summary>Creates metadata references for syntax-only architecture compilations.</summary>
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

    /// <summary>Gets an invoked member name from an invocation expression.</summary>
    private static string? GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };
    }

    /// <summary>Gets a one-based source line for a syntax node.</summary>
    private static int GetLine(SyntaxNode node) => node.SyntaxTree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

    /// <summary>Finds the repository root from the functional test output folder.</summary>
    private static string FindRepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "Utils.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return directory ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    /// <summary>Checks whether a path contains a named directory.</summary>
    private static bool ContainsDirectory(string file, string directoryName)
    {
        return file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(directoryName, StringComparer.Ordinal);
    }

    /// <summary>Finds the built parser diagnostics assembly required for semantic type checks.</summary>
    private static string FindParserDiagnosticsAssembly(string repositoryRoot)
    {
        string diagnosticsRoot = Path.Combine(repositoryRoot, "Utils.Parser.Diagnostics", "bin");
        string? assembly = Directory.Exists(diagnosticsRoot)
            ? Directory.GetFiles(diagnosticsRoot, "Utils.Parser.Diagnostics.dll", SearchOption.AllDirectories)
                .FirstOrDefault(static file => !ContainsDirectory(file, "ref") && !ContainsDirectory(file, "refint"))
            : null;

        return assembly ?? throw new FileNotFoundException("Utils.Parser.Diagnostics.dll was not found. Build Utils.Parser.Diagnostics before running the architecture test.");
    }

    /// <summary>Normalizes paths to slash-separated relative paths.</summary>
    private static string NormalizePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>Represents one source file with the semantic model used by the architecture guard.</summary>
    private sealed record SourceScan(string RelativePath, SyntaxTree Tree, SemanticModel SemanticModel);

    /// <summary>Identifies the embedded-code text source that reached generated-source writing.</summary>
    private enum EmbeddedCodeTextKind
    {
        /// <summary>Transformed embedded C# text.</summary>
        Transformed,

        /// <summary>Raw grammar embedded-code text.</summary>
        Raw,

        /// <summary>Raw source text carried by grammar model nodes.</summary>
        Source
    }
}
