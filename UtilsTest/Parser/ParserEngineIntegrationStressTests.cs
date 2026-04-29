using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Validates ParserEngine integration and stress behavior using dynamic grammar loading.
/// These tests target runtime robustness and regression protection, not full ANTLR compatibility certification.
/// </summary>
[TestClass]
public class ParserEngineIntegrationStressTests
{
    private static readonly string RepositoryRoot = ResolveRepositoryRoot();

    /// <summary>
    /// Ensures the dynamic parser pipeline handles real SQL grammar with nested clauses.
    /// </summary>
    [TestMethod]
    [Timeout(15000)]
    public void ExpressionGrammar_IntegrationParse_CompletesWithoutSafetyDiagnostics()
    {
        var diagnostics = new DiagnosticBag();
        var grammarPath = Path.Combine(RepositoryRoot, "UtilsTest", "Parser", "Exp.g4");
        Assert.IsTrue(File.Exists(grammarPath), $"Grammar file was not found: {grammarPath}");

        var definition = Antlr4GrammarConverter.Parse(File.ReadAllText(grammarPath), diagnostics);
        const string input = "(5+10)*3/(10+2)-4+(3/2*(8-1))";

        var stopwatch = Stopwatch.StartNew();
        var parseTree = ParseWithDefinition(definition, input, diagnostics, out var tokenCount);
        stopwatch.Stop();

        Assert.IsNotInstanceOfType<ErrorNode>(parseTree);
        Assert.AreEqual("eval", ((ParserNode)parseTree).Rule.Name);
        Assert.IsTrue(tokenCount > 12, "Expected a non-trivial token stream for integration coverage.");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Parsing took too long: {stopwatch.Elapsed}.");
        AssertNoUnexpectedSafetyDiagnostics(diagnostics);
    }

    /// <summary>
    /// Ensures the dynamic parser pipeline handles the existing expression grammar with nested operators.
    /// </summary>
    [TestMethod]
    [Timeout(15000)]
    public void ExpressionGrammar_DeeplyNestedParse_CompletesWithoutSafetyDiagnostics()
    {
        var diagnostics = new DiagnosticBag();
        var grammarPath = Path.Combine(RepositoryRoot, "UtilsTest", "Parser", "Exp.g4");
        Assert.IsTrue(File.Exists(grammarPath), $"Grammar file was not found: {grammarPath}");

        var definition = Antlr4GrammarConverter.Parse(File.ReadAllText(grammarPath), diagnostics);
        const string input = "((((1+2)*3)-4)+(5*(6-(7/8))))+(9*10)";

        var stopwatch = Stopwatch.StartNew();
        var parseTree = ParseWithDefinition(definition, input, diagnostics, out var tokenCount);
        stopwatch.Stop();

        Assert.IsNotInstanceOfType<ErrorNode>(parseTree);
        Assert.AreEqual("eval", ((ParserNode)parseTree).Rule.Name);
        Assert.IsTrue(tokenCount > 16, "Expected a non-trivial token stream for integration coverage.");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Parsing took too long: {stopwatch.Elapsed}.");
        AssertNoUnexpectedSafetyDiagnostics(diagnostics);
    }

    /// <summary>
    /// Ensures repeated realistic arithmetic expressions do not cause exponential behavior.
    /// </summary>
    [TestMethod]
    [Timeout(20000)]
    public void ExpressionGrammar_RepeatedTerms_ParsesWithinReasonableTime()
    {
        var diagnostics = new DiagnosticBag();
        var grammarPath = Path.Combine(RepositoryRoot, "UtilsTest", "Parser", "Exp.g4");
        Assert.IsTrue(File.Exists(grammarPath), $"Grammar file was not found: {grammarPath}");

        var definition = Antlr4GrammarConverter.Parse(File.ReadAllText(grammarPath), diagnostics);
        var terms = string.Join("+", Enumerable.Range(1, 220));
        var input = terms;

        var stopwatch = Stopwatch.StartNew();
        var parseTree = ParseWithDefinition(definition, input, diagnostics, out var tokenCount);
        stopwatch.Stop();

        Assert.IsNotInstanceOfType<ErrorNode>(parseTree);
        Assert.IsTrue(tokenCount > 400, "Expected stress input to produce a large token stream.");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Parsing took too long: {stopwatch.Elapsed}.");
        AssertNoUnexpectedSafetyDiagnostics(diagnostics);
    }

    private static ParseNode ParseWithDefinition(ParserDefinition definition, string input, DiagnosticBag diagnostics, out int tokenCount)
    {
        var lexer = new LexerEngine(definition);
        var tokens = lexer.Tokenize(new StringReader(input), diagnostics: diagnostics).ToList();
        tokenCount = tokens.Count;

        var parser = new ParserEngine(definition);
        return parser.Parse(tokens, diagnostics: diagnostics);
    }

    private static void AssertNoUnexpectedSafetyDiagnostics(DiagnosticBag diagnostics)
    {
        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.ParserStateCycleDetected.Code));
        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.NonProgressiveQuantifierStopped.Code));
        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.NonProgressiveLeftRecursionStopped.Code));
        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.ParseFailure.Code));
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var markerDirectory = Path.Combine(current.FullName, "Utils.Data");
            if (Directory.Exists(markerDirectory))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Unable to resolve repository root from '{AppContext.BaseDirectory}'.");
    }
}
