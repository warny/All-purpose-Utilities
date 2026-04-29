using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.ProjectCompilation;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Validates ParserEngine integration and stress behavior on real repository grammars.
/// These tests target runtime robustness and regression protection, not full ANTLR compatibility certification.
/// </summary>
[TestClass]
public class ParserEngineIntegrationStressTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    /// <summary>
    /// Ensures C-like parser/lexer grammar assets bootstrap and parse nested constructs without safety regressions.
    /// </summary>
    [TestMethod]
    [Timeout(15000)]
    public void CSyntaxGrammar_IntegrationParse_CompletesWithoutSafetyDiagnostics()
    {
        var diagnostics = new DiagnosticBag();
        var parserGrammarPath = Path.Combine(RepositoryRoot, "Utils.Expressions.CSyntax", "Grammar", "CSyntaxParser.g4");
        var definition = Antlr4GrammarProjectCompiler.ParseFromFile(parserGrammarPath, diagnostics);

        Assert.IsNotNull(definition.RootRule);
        Assert.IsTrue(definition.AllRules.ContainsKey("instruction"));

        const string input = """
            for(i=0;i<10;i=i+1){
                if(i<5){
                    total=total+i;
                } else {
                    total=total-1;
                }
            }
            """;

        var stopwatch = Stopwatch.StartNew();
        var parseTree = ParseWithDefinition(definition, input, diagnostics, out var tokenCount);
        stopwatch.Stop();

        Assert.IsNotInstanceOfType<ErrorNode>(parseTree);
        Assert.AreEqual("instruction", ((ParserNode)parseTree).Rule.Name);
        Assert.IsTrue(tokenCount > 20, "Expected a non-trivial token stream for stress coverage.");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Parsing took too long: {stopwatch.Elapsed}.");
        AssertNoUnexpectedSafetyDiagnostics(diagnostics);
    }

    /// <summary>
    /// Ensures VB-like parser grammar with token vocabulary resolves and parses optional-heavy syntax without false safety failures.
    /// </summary>
    [TestMethod]
    [Timeout(15000)]
    public void VbSyntaxGrammar_IntegrationParse_CompletesWithoutSafetyDiagnostics()
    {
        var diagnostics = new DiagnosticBag();
        var parserGrammarPath = Path.Combine(RepositoryRoot, "Utils.Expressions.VBSyntax", "Grammar", "VBSyntaxParser.g4");
        var definition = Antlr4GrammarProjectCompiler.ParseFromFile(parserGrammarPath, diagnostics);

        Assert.IsNotNull(definition.RootRule);
        Assert.IsTrue(definition.AllRules.ContainsKey("instruction"));

        const string input = """
            If value > 10 Then
                For i = 0 To 3 Step 1
                    total = total + i
                Next i
            ElseIf value = 10 Then
                Return value
            Else
                Return 0
            End If
            """;

        var stopwatch = Stopwatch.StartNew();
        var parseTree = ParseWithDefinition(definition, input, diagnostics, out var tokenCount);
        stopwatch.Stop();

        Assert.IsNotInstanceOfType<ErrorNode>(parseTree);
        Assert.AreEqual("instruction", ((ParserNode)parseTree).Rule.Name);
        Assert.IsTrue(tokenCount > 20, "Expected a non-trivial token stream for stress coverage.");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Parsing took too long: {stopwatch.Elapsed}.");
        AssertNoUnexpectedSafetyDiagnostics(diagnostics);
    }

    /// <summary>
    /// Ensures repeated realistic constructs do not cause exponential behavior or invalid repeated-state rejection.
    /// </summary>
    [TestMethod]
    [Timeout(20000)]
    public void CSyntaxGrammar_RepeatedBlocks_ParsesWithinReasonableTime()
    {
        var diagnostics = new DiagnosticBag();
        var parserGrammarPath = Path.Combine(RepositoryRoot, "Utils.Expressions.CSyntax", "Grammar", "CSyntaxParser.g4");
        var definition = Antlr4GrammarProjectCompiler.ParseFromFile(parserGrammarPath, diagnostics);

        var repeatedAssignments = string.Join(Environment.NewLine, Enumerable.Range(0, 120)
            .Select(index => $"value{index}=value{index}+1;"));
        var input = "{" + Environment.NewLine + repeatedAssignments + Environment.NewLine + "}";

        var stopwatch = Stopwatch.StartNew();
        var parseTree = ParseWithDefinition(definition, input, diagnostics, out var tokenCount);
        stopwatch.Stop();

        Assert.IsNotInstanceOfType<ErrorNode>(parseTree);
        Assert.IsTrue(tokenCount > 400, "Expected stress input to produce a large token stream.");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Parsing took too long: {stopwatch.Elapsed}.");
        AssertNoUnexpectedSafetyDiagnostics(diagnostics);
    }

    /// <summary>
    /// Tokenizes and parses input from a compiled grammar definition while collecting diagnostics.
    /// </summary>
    private static ParseNode ParseWithDefinition(ParserDefinition definition, string input, DiagnosticBag diagnostics, out int tokenCount)
    {
        var lexer = new LexerEngine(definition);
        var tokens = lexer.Tokenize(new StringReader(input), diagnostics: diagnostics).ToList();
        tokenCount = tokens.Count;

        var parser = new ParserEngine(definition);
        return parser.Parse(tokens, diagnostics: diagnostics);
    }

    /// <summary>
    /// Asserts that no unexpected parser safety diagnostics were emitted for nominal grammars.
    /// </summary>
    private static void AssertNoUnexpectedSafetyDiagnostics(DiagnosticBag diagnostics)
    {
        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.ParserStateCycleDetected.Code));
        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.NonProgressiveQuantifierStopped.Code));
        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.NonProgressiveLeftRecursionStopped.Code));
        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.ParseFailure.Code));
        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "Unexpected error diagnostics: " + string.Join(" | ", diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic => diagnostic.Code)));
    }
}
