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
    /// Ensures two alternatives invoking the same rule at the same position select
    /// the same observable parse result as an equivalent grammar with reversed alternative order.
    /// </summary>
    [TestMethod]
    public void SameRuleSamePositionAcrossAlternatives_MatchesBaselineSelection()
    {
        const string grammarPreferredOrder = """
            grammar G;
            root : common 'X'
                 | common 'Y'
                 ;
            common : ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;
        const string grammarReversedOrder = """
            grammar G;
            root : common 'Y'
                 | common 'X'
                 ;
            common : ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var diagnosticsA = new DiagnosticBag();
        var treeA = ParseWithDiagnostics(grammarPreferredOrder, "id X", diagnosticsA);

        var diagnosticsB = new DiagnosticBag();
        var treeB = ParseWithDiagnostics(grammarReversedOrder, "id X", diagnosticsB);

        Assert.IsNotInstanceOfType<ErrorNode>(treeA);
        Assert.IsNotInstanceOfType<ErrorNode>(treeB);
        Assert.AreEqual(1, CountTokenText(treeA, "X"));
        Assert.AreEqual(1, CountTokenText(treeB, "X"));
        Assert.AreEqual(treeA.ToString(), treeB.ToString());
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
        const string baselineGrammar = """
            grammar G;
            start : seq EOF ;
            seq : ('aa' | 'a')+ ;
            EOF : '<EOF>' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics(grammar, "aa aa <EOF>", diagnostics);
        var baselineDiagnostics = new DiagnosticBag();
        var baselineTree = ParseWithDiagnostics(baselineGrammar, "aa aa <EOF>", baselineDiagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.IsNotInstanceOfType<ErrorNode>(baselineTree);
        Assert.AreEqual(baselineTree.ToString(), tree.ToString(), "Ambiguous grammar should keep the same selected parse tree.");
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.BacktrackingUsed.Code));
        Assert.IsTrue(baselineDiagnostics.Any(d => d.Code == ParserDiagnostics.BacktrackingUsed.Code));
    }

    /// <summary>
    /// Ensures equivalent ambiguous alternatives are still pruned and diagnostics remain emitted.
    /// </summary>
    [TestMethod]
    public void EquivalentAmbiguousAlternatives_ArePruned_WithDiagnostic()
    {
        const string grammar = """
            grammar G;
            root : ('a' | 'a') EOF ;
            EOF : '<EOF>' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics(grammar, "a <EOF>", diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
    }

    /// <summary>
    /// Ensures observable behavior is preserved when the same parser rule is invoked
    /// from multiple alternatives at the same input position.
    /// </summary>
    [TestMethod]
    public void SameRuleSamePositionAcrossAlternatives_PreservesObservableBehavior()
    {
        const string grammar = """
            grammar G;
            root : common 'X'
                 | common 'Y'
                 ;
            common : ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var diagnosticsX = new DiagnosticBag();
        var treeX = ParseWithDiagnostics(grammar, "id X", diagnosticsX);

        var diagnosticsY = new DiagnosticBag();
        var treeY = ParseWithDiagnostics(grammar, "id Y", diagnosticsY);

        Assert.IsNotInstanceOfType<ErrorNode>(treeX);
        Assert.IsNotInstanceOfType<ErrorNode>(treeY);
        Assert.AreEqual(1, CountTokenText(treeX, "X"));
        Assert.AreEqual(1, CountTokenText(treeY, "Y"));
        Assert.IsFalse(diagnosticsX.Any(d => d.Code == ParserDiagnostics.ParserStateCycleDetected.Code));
        Assert.IsFalse(diagnosticsY.Any(d => d.Code == ParserDiagnostics.ParserStateCycleDetected.Code));
    }

    /// <summary>
    /// Ensures deterministic failures are memoized per invocation key and not re-evaluated for each backtracked alternative.
    /// </summary>
    [TestMethod]
    public void FailedInvocation_IsMemoizedAcrossBacktrackedAlternatives()
    {
        const string grammar = """
            grammar FailureMemoRegression;
            start : alt EOF ;
            alt
                : prefix failing suffix1
                | prefix failing suffix2
                | prefix failing suffix3
                | prefix ok
                ;
            prefix : 'a' ;
            failing : 'x' 'y' 'z' ;
            suffix1 : '1' ;
            suffix2 : '2' ;
            suffix3 : '3' ;
            ok : 'b' ;
            EOF : '<EOF>' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics(grammar, "a b <EOF>", diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.AreEqual(1, CountTokenText(tree, "b"));

        var failingMemoMisses = diagnostics.Count(d =>
            d.Code == ParserDiagnostics.ParseMemoMiss.Code
            && d.RuleName == "failing");
        Assert.AreEqual(1, failingMemoMisses, "failing rule should miss memo only once for identical invocation key.");

        var failingMemoHits = diagnostics.Count(d =>
            d.Code == ParserDiagnostics.ParseMemoHit.Code
            && d.RuleName == "failing");
        Assert.IsTrue(failingMemoHits >= 2, "backtracked alternatives should reuse memoized failure.");
    }

    /// <summary>
    /// Regression: a parser rule ref at EOF must not be rejected by the look-ahead probe.
    /// The probe must return Unknown for parser rule refs (they may consume no tokens),
    /// so the parser still attempts to parse the referenced rule.
    /// </summary>
    [TestMethod]
    public void ParserLookaheadProbe_DoesNotRejectParserRuleRefAtEof()
    {
        const string grammar = """
            grammar G;
            root : opt ;
            opt  : ID* ;
            ID   : ('a'..'z')+ ;
            WS   : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics(grammar, "", diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree, "parser rule ref at EOF should not be rejected by the look-ahead probe.");
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
