using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Expressions.VBSyntax.Runtime;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Validates ParserEngine integration and stress behavior on real compiled grammars.
/// Uses pre-compiled grammar definitions (generated from .g4 at build time) rather than
/// runtime .g4 parsing, which does not support all constructs used in production grammars.
/// </summary>
[TestClass]
public class ParserEngineIntegrationStressTests
{
    /// <summary>
    /// Ensures the C-like compiled grammar parses a realistic nested construct
    /// within the time limit and produces the expected root rule.
    /// </summary>
    [TestMethod]
    [Timeout(15000)]
    public void CSyntaxGrammar_IntegrationParse_CompletesWithoutErrors()
    {
        var tokenParser = new CSyntaxTokenParser();

        const string input = """
            for(i=0;i<10;i=i+1){
                if(i<5){
                    total=total+i;
                } else {
                    total=total-1;
                }
            }
            """;

        var tokenCount = tokenParser.Tokenize(input).Count;

        var stopwatch = Stopwatch.StartNew();
        var parseTree = tokenParser.Parse(input);
        stopwatch.Stop();

        Assert.IsNotInstanceOfType<ErrorNode>(parseTree);
        Assert.IsInstanceOfType<ParserNode>(parseTree);
        Assert.AreEqual("instruction", ((ParserNode)parseTree).Rule.Name);
        Assert.IsTrue(tokenCount > 20, "Expected a non-trivial token stream for stress coverage.");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Parsing took too long: {stopwatch.Elapsed}.");
    }

    /// <summary>
    /// Ensures the VB-like compiled grammar parses a realistic control-flow block
    /// within the time limit and produces the expected root rule.
    /// </summary>
    [TestMethod]
    [Timeout(15000)]
    public void VbSyntaxGrammar_IntegrationParse_CompletesWithoutErrors()
    {
        var tokenParser = new VBSyntaxTokenParser();

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

        var tokenCount = tokenParser.Tokenize(input).Count;

        var stopwatch = Stopwatch.StartNew();
        var parseTree = tokenParser.Parse(input);
        stopwatch.Stop();

        Assert.IsNotInstanceOfType<ErrorNode>(parseTree);
        Assert.IsInstanceOfType<ParserNode>(parseTree);
        Assert.AreEqual("instruction", ((ParserNode)parseTree).Rule.Name);
        Assert.IsTrue(tokenCount > 20, "Expected a non-trivial token stream for stress coverage.");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Parsing took too long: {stopwatch.Elapsed}.");
    }

    /// <summary>
    /// Ensures repeated realistic constructs (120 assignments in a block) do not cause
    /// exponential behavior or invalid repeated-state rejection.
    /// </summary>
    [TestMethod]
    [Timeout(20000)]
    public void CSyntaxGrammar_RepeatedBlocks_ParsesWithinReasonableTime()
    {
        var tokenParser = new CSyntaxTokenParser();

        var repeatedAssignments = string.Join(Environment.NewLine, Enumerable.Range(0, 120)
            .Select(index => $"value{index}=value{index}+1;"));
        var input = "{" + Environment.NewLine + repeatedAssignments + Environment.NewLine + "}";

        var tokenCount = tokenParser.Tokenize(input).Count;

        var stopwatch = Stopwatch.StartNew();
        var parseTree = tokenParser.Parse(input);
        stopwatch.Stop();

        Assert.IsNotInstanceOfType<ErrorNode>(parseTree);
        Assert.IsTrue(tokenCount > 400, "Expected stress input to produce a large token stream.");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(15),
            $"Parsing took too long: {stopwatch.Elapsed}.");
    }
}
