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
        var key = new ParserLookaheadKey("root", 0, 0, 0, "rule-root", -1);
        var expected = new ParserLookaheadResult(false, "ID", "id");

        Assert.IsTrue(cache.TryAdd(key, expected));
        Assert.IsTrue(cache.TryGet(key, out var actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ParserLookaheadCache_IsolatesByAlternativeIndex()
    {
        var cache = new ParserLookaheadCache();
        var first = new ParserLookaheadKey("root", 0, 0, 0, "rule-root", -1);
        var second = new ParserLookaheadKey("root", 0, 1, 0, "rule-root", -1);

        cache.TryAdd(first, new ParserLookaheadResult(false, "ID", "id"));

        Assert.IsFalse(cache.TryGet(second, out _));
    }

    [TestMethod]
    public void ParserLookaheadCache_IsolatesByPrecedence()
    {
        var cache = new ParserLookaheadCache();
        var lower = new ParserLookaheadKey("root", 0, 0, 0, "rule-root", -1);
        var higher = new ParserLookaheadKey("root", 0, 0, 1, "rule-root", -1);

        cache.TryAdd(lower, new ParserLookaheadResult(false, "ID", "id"));

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
        var first = new ParserLookaheadKey("root", 0, 0, 0, "alternation", 1);
        var second = new ParserLookaheadKey("root", 0, 0, 0, "alternation", 2);

        cache.TryAdd(first, new ParserLookaheadResult(false, "ID", "id"));

        Assert.IsFalse(cache.TryGet(second, out _));
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
