using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.Bootstrap;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserLookaheadCacheTests
{
    [TestMethod]
    public void ParserLookaheadCache_StoresAndRetrievesResult()
    {
        var cache = new ParserLookaheadCache();
        var key = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        var expected = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, "ID", "id");

        Assert.IsTrue(cache.TryAdd(key, expected));
        Assert.IsTrue(cache.TryGet(key, out var actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ParserLookaheadCache_IsolatesByAlternativeIndex()
    {
        var cache = new ParserLookaheadCache();
        var first = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        var second = new ParserLookaheadKey("root", 0, 1, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);

        cache.TryAdd(first, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, "ID", "id"));

        Assert.IsFalse(cache.TryGet(second, out _));
    }

    [TestMethod]
    public void ParserLookaheadCache_IsolatesByPrecedence()
    {
        var cache = new ParserLookaheadCache();
        var lower = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        var higher = new ParserLookaheadKey("root", 0, 0, 1, ScheduledAlternativeCursorKinds.RuleRoot, -1);

        cache.TryAdd(lower, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, "ID", "id"));

        Assert.IsFalse(cache.TryGet(higher, out _));
    }

    [TestMethod]
    public void ScheduledAlternatives_PreservesParseTreeWithSharedPrefix()
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

        var treeX = Parse(grammar, "id X", out _);
        var treeY = Parse(grammar, "id Y", out _);

        Assert.IsNotInstanceOfType<ErrorNode>(treeX);
        Assert.IsNotInstanceOfType<ErrorNode>(treeY);
        Assert.AreEqual(1, CountTokenText(treeX, "X"));
        Assert.AreEqual(1, CountTokenText(treeY, "Y"));
    }

    [TestMethod]
    public void FailedAlternativeLookahead_DoesNotPoisonLaterSuccess()
    {
        const string grammar = """
            grammar G;
            root : bad | good ;
            bad : 'a' 'x' ;
            good : 'a' 'y' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var tree = Parse(grammar, "a y", out _);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.AreEqual(1, CountTokenText(tree, "y"));
    }

    [TestMethod]
    public void ExistingDiagnostics_AreNotLost()
    {
        const string grammar = """
            grammar G;
            root : ('a' | 'a') EOF ;
            EOF : '<EOF>' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        _ = Parse(grammar, "a <EOF>", out var diagnostics);

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
    }

    [TestMethod]
    public void ParserLookaheadCache_IsolatesByCursorContext()
    {
        var cache = new ParserLookaheadCache();
        var first = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.Alternation, 1);
        var second = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.Alternation, 2);

        cache.TryAdd(first, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, "ID", "id"));

        Assert.IsFalse(cache.TryGet(second, out _));
    }


    [TestMethod]
    public void ParserLookaheadCache_IsolatesRuleRootAndNestedAlternationContexts()
    {
        var cache = new ParserLookaheadCache();
        var ruleRoot = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        var nestedAlternation = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.Alternation, -1);

        cache.TryAdd(ruleRoot, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, "ID", "id"));

        Assert.IsFalse(cache.TryGet(nestedAlternation, out _));
    }

    [TestMethod]
    public void ParserLookaheadProbeResult_StoresImmediateReject()
    {
        var cache = new ParserLookaheadCache();
        var key = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        var expected = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, "ID", "id");

        Assert.IsTrue(cache.TryAdd(key, expected));
        Assert.IsTrue(cache.TryGet(key, out var actual));
        Assert.AreEqual(ParserLookaheadProbeKind.ImmediateReject, actual.Kind);
    }

    [TestMethod]
    public void ParserLookaheadProbeResult_StoresRequiresParse()
    {
        var cache = new ParserLookaheadCache();
        var key = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        var expected = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id");

        Assert.IsTrue(cache.TryAdd(key, expected));
        Assert.IsTrue(cache.TryGet(key, out var actual));
        Assert.AreEqual(ParserLookaheadProbeKind.RequiresParse, actual.Kind);
    }

    [TestMethod]
    public void DiagnosticsProvided_NegativeLookaheadDoesNotSkipRuleDiagnostics()
    {
        const string grammar = """
            grammar G;
            root : failing 'X'
                 | failing 'Y'
                 | ok
                 ;
            failing : 'z' ;
            ok : 'a' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var tree = Parse(grammar, "a", out var diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.AreEqual(1, CountTokenText(tree, "a"));

        var failingMisses = diagnostics.Count(d =>
            d.Code == ParserDiagnostics.ParseMemoMiss.Code
            && d.RuleName == "failing");
        var failingHits = diagnostics.Count(d =>
            d.Code == ParserDiagnostics.ParseMemoHit.Code
            && d.RuleName == "failing");

        Assert.AreEqual(1, failingMisses);
        Assert.IsTrue(failingHits >= 1);
    }

    private static ParseNode Parse(string grammarText, string input, out DiagnosticBag diagnostics)
    {
        diagnostics = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.Parse(grammarText, diagnostics);
        var lexer = new LexerEngine(definition);
        var parser = new ParserEngine(definition);
        var tokens = lexer.Tokenize(new System.IO.StringReader(input), null);
        return parser.Parse(tokens, diagnostics: diagnostics);
    }

    private static int CountTokenText(ParseNode node, string text)
    {
        var count = 0;
        CountTokenText(node, text, ref count);
        return count;
    }

    private static void CountTokenText(ParseNode node, string text, ref int count)
    {
        if (node is LexerNode lexerNode && lexerNode.Token.Text == text)
        {
            count++;
        }

        if (node is ParserNode parserNode)
        {
            foreach (var child in parserNode.Children)
            {
                CountTokenText(child, text, ref count);
            }
        }
    }
}
