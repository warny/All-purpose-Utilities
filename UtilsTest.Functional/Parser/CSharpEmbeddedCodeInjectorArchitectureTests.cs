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

        string[] violations = Directory.GetFiles(generatorRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static file => !ContainsDirectory(file, "bin") && !ContainsDirectory(file, "obj"))
            .SelectMany(file => FindForbiddenAppendUsages(repositoryRoot, file))
            .Concat(FindTransformedTextUsages(repositoryRoot, generatorRoot))
            .ToArray();

        Assert.AreEqual(0, violations.Length, string.Join(Environment.NewLine, violations));
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

    /// <summary>Finds forbidden Append/AppendLine calls using raw embedded-code source properties.</summary>
    private static IEnumerable<string> FindForbiddenAppendUsages(string repositoryRoot, string file)
    {
        string relativePath = NormalizePath(Path.GetRelativePath(repositoryRoot, file));
        if (string.Equals(relativePath, InjectorPath, StringComparison.Ordinal))
        {
            yield break;
        }

        SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
        foreach (InvocationExpressionSyntax invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string? methodName = GetInvokedMethodName(invocation);
            if (methodName is not "Append" and not "AppendLine")
            {
                continue;
            }

            foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
            {
                string expression = argument.Expression.ToString();
                if (expression.Contains("RawCode.Text", StringComparison.Ordinal)
                    || expression.Contains("RawCode", StringComparison.Ordinal)
                    || expression.Contains("SourceText", StringComparison.Ordinal))
                {
                    yield return $"{relativePath}:{GetLine(invocation)}: {invocation}";
                }
            }
        }
    }

    /// <summary>Finds transformed-code text reads outside the injector.</summary>
    private static IEnumerable<string> FindTransformedTextUsages(string repositoryRoot, string generatorRoot)
    {
        foreach (string file in Directory.GetFiles(generatorRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relativePath = NormalizePath(Path.GetRelativePath(repositoryRoot, file));
            if (string.Equals(relativePath, InjectorPath, StringComparison.Ordinal))
            {
                continue;
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            foreach (MemberAccessExpressionSyntax access in tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                if (access.Name.Identifier.ValueText == "Text"
                    && access.Expression.ToString().Contains("code", StringComparison.OrdinalIgnoreCase)
                    && !IsAllowedTransformedTextRead(relativePath, access))
                {
                    yield return $"{relativePath}:{GetLine(access)}: {access}";
                }
            }
        }
    }

    /// <summary>Determines whether a transformed-code text read is classification-only rather than generated-source injection.</summary>
    private static bool IsAllowedTransformedTextRead(string relativePath, MemberAccessExpressionSyntax access)
    {
        MethodDeclarationSyntax? method = access.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        return (string.Equals(relativePath, NormalizePath(Path.Combine("Utils.Parser.Generators", "Internal", "GrammarEmitter.ExecutionContext.Hooks.cs")), StringComparison.Ordinal)
                && string.Equals(method?.Identifier.ValueText, "ForPredicate", StringComparison.Ordinal))
            || (string.Equals(relativePath, NormalizePath(Path.Combine("Utils.Parser.Generators", "Internal", "GrammarEmitter.ExecutionContext.Policy.cs")), StringComparison.Ordinal)
                && string.Equals(method?.Identifier.ValueText, "GetRawEmbeddedCodeText", StringComparison.Ordinal));
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

    /// <summary>Normalizes paths to slash-separated relative paths.</summary>
    private static string NormalizePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');
}
