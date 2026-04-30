using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Regression tests for registry-backed invocation completion and reuse behavior.
/// </summary>
[TestClass]
public class ParserEngineRegistryRegressionTests
{
    /// <summary>
    /// Ensures shared invocations at the same input position can reuse completed results
    /// without changing parse tree shape or diagnostics.
    /// </summary>
    [TestMethod]
    public void SharedInvocationReuse_PreservesTreeAndDiagnostics()
    {
        const string grammar = """
            grammar G;
            start : pair EOF ;
            pair : common common ;
            common : ID ;
            ID : ('a'..'z')+ ;
            EOF : '<EOF>' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var diagnosticsA = new DiagnosticBag();
        var treeA = ParseWithDiagnostics(grammar, "foo bar <EOF>", diagnosticsA);

        var diagnosticsB = new DiagnosticBag();
        var treeB = ParseWithDiagnostics(grammar, "foo bar <EOF>", diagnosticsB);

        Assert.IsNotInstanceOfType<ErrorNode>(treeA);
        Assert.IsNotInstanceOfType<ErrorNode>(treeB);
        Assert.AreEqual(treeA.ToString(), treeB.ToString());
        CollectionAssert.AreEqual(
            diagnosticsA.Select(static d => d.Code).ToArray(),
            diagnosticsB.Select(static d => d.Code).ToArray());
    }

    /// <summary>
    /// Ensures a failing invocation completion does not block a later successful path.
    /// </summary>
    [TestMethod]
    public void FailedInvocation_DoesNotPoisonLaterSuccess()
    {
        const string grammar = """
            grammar G;
            start : bad | good ;
            bad : common 'X' ;
            good : common 'Y' ;
            common : ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics(grammar, "id Y", diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.AreEqual(1, CountTokenText(tree, "Y"));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.ParserStateCycleDetected.Code));
    }

    /// <summary>
    /// Ensures precedence is part of invocation identity and prevents unsafe reuse collisions.
    /// </summary>
    [TestMethod]
    public void PrecedenceSensitiveReuse_DoesNotCollideAcrossPrecedenceLevels()
    {
        var diagnostics = new DiagnosticBag();
        var tree = ParseDefinitionWithDiagnostics(ExpGrammar.BuildDefinition(), "2 + 3 * 5", diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.IsNotNull(FindRule(tree, "additionExp"));
        Assert.IsNotNull(FindRule(tree, "multiplyExp"));
    }

    /// <summary>
    /// Ensures ambiguous backtracking behavior is preserved after registry centralization.
    /// </summary>
    [TestMethod]
    public void AmbiguityBacktracking_NonRegression()
    {
        const string grammar = """
            grammar G;
            start : seq EOF ;
            seq : ('a' | 'aa')+ ;
            EOF : '<EOF>' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics(grammar, "aa aa <EOF>", diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.BacktrackingUsed.Code));
    }

    private static ParseNode ParseWithDiagnostics(string grammarText, string input, DiagnosticBag diagnostics)
    {
        var definition = Antlr4GrammarConverter.Parse(grammarText, diagnostics);
        return ParseDefinitionWithDiagnostics(definition, input, diagnostics);
    }

    private static ParseNode ParseDefinitionWithDiagnostics(ParserDefinition definition, string input, DiagnosticBag diagnostics)
    {
        var lexer = new LexerEngine(definition);
        using var reader = new StringReader(input);
        var tokens = lexer.Tokenize(reader, diagnostics: diagnostics);
        var parser = new ParserEngine(definition);
        return parser.Parse(tokens, diagnostics: diagnostics);
    }

    private static int CountRule(ParseNode node, string ruleName)
    {
        var count = 0;
        if (node.Rule?.Name == ruleName)
        {
            count++;
        }

        if (node is ParserNode parserNode)
        {
            foreach (var child in parserNode.Children)
            {
                count += CountRule(child, ruleName);
            }
        }

        return count;
    }

    private static int CountTokenText(ParseNode node, string tokenText)
    {
        var count = node is LexerNode lexerNode && lexerNode.Token.Text == tokenText ? 1 : 0;

        if (node is ParserNode parserNode)
        {
            foreach (var child in parserNode.Children)
            {
                count += CountTokenText(child, tokenText);
            }
        }

        return count;
    }

    private static ParseNode? FindRule(ParseNode node, string ruleName)
    {
        if (node.Rule?.Name == ruleName)
        {
            return node;
        }

        if (node is ParserNode parserNode)
        {
            foreach (var child in parserNode.Children)
            {
                var found = FindRule(child, ruleName);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }
}
