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

    /// <summary>Keys that differ by exactly one of their six fields are distinct entries, packed or not.</summary>
    [TestMethod]
    public void ParserLookaheadCache_KeysDifferingByOneFieldAreDistinct()
    {
        var cache = new ParserLookaheadCache();
        var baseKey = new ParserLookaheadKey("root", 5, 2, 1, ScheduledAlternativeCursorKinds.Alternation, 3);
        ParserLookaheadKey[] keys =
        [
            baseKey,
            baseKey with { RuleName = "other" },
            baseKey with { OriginPosition = 6 },
            baseKey with { AlternativeIndex = 3 },
            baseKey with { MinimumPrecedence = 2 },
            baseKey with { CursorKind = ScheduledAlternativeCursorKinds.AlternativeRoot },
            baseKey with { CursorIndex = 4 },
            baseKey with { CursorKind = "custom" },
            baseKey with { AlternativeIndex = 256 },
        ];

        for (var i = 0; i < keys.Length; i++)
        {
            Assert.IsTrue(cache.TryAdd(keys[i], new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, "ID", "t" + i)), keys[i].ToString());
        }

        Assert.AreEqual(keys.Length, cache.Count);
        Assert.AreEqual(2, cache.OverflowCount, "the unusual cursor kind and the out-of-range alternative are stored unchanged");
        for (var i = 0; i < keys.Length; i++)
        {
            Assert.IsTrue(cache.TryGet(keys[i], out var result));
            Assert.AreEqual("t" + i, result.TokenText);
        }
    }

    /// <summary>Rule names and cursor kinds are compared by value: another instance of the same text finds the entry.</summary>
    [TestMethod]
    public void ParserLookaheadCache_EqualNamesFromOtherInstancesShareTheEntry()
    {
        var cache = new ParserLookaheadCache();
        var key = new ParserLookaheadKey("root", 0, 1, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        var copy = new ParserLookaheadKey(new string("root".ToCharArray()), 0, 1, 0, new string(ScheduledAlternativeCursorKinds.RuleRoot.ToCharArray()), -1);
        var result = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id");

        Assert.IsTrue(cache.TryAdd(key, result));
        Assert.IsFalse(cache.TryAdd(copy, result));
        Assert.IsTrue(cache.TryGet(copy, out var found));
        Assert.AreEqual(result, found);
    }

    /// <summary>A later insertion under an existing key never replaces the first result.</summary>
    [TestMethod]
    public void ParserLookaheadCache_KeepsTheFirstResult()
    {
        var cache = new ParserLookaheadCache();
        var key = new ParserLookaheadKey("root", 0, 0, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1);
        var first = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, "ID", "id", ["ID"]);
        var second = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "NUM", "1");

        Assert.IsTrue(cache.TryAdd(key, first));
        Assert.IsFalse(cache.TryAdd(key, second));
        Assert.IsTrue(cache.TryGet(key, out var found));
        Assert.AreEqual(first, found);
        Assert.AreSame(first.ExpectedTokenNames, found.ExpectedTokenNames);
    }

    /// <summary>
    /// The stored result comes back with the same token strings and the same expected-name collection, which keeps the
    /// difference between absent and empty names, their order and their repetitions, even when entries of one origin
    /// observed different token instances.
    /// </summary>
    [TestMethod]
    public void ParserLookaheadCache_ReturnsTheStoredInstances()
    {
        var cache = new ParserLookaheadCache();
        string text = "id";
        string otherText = new("id".ToCharArray());
        ParserLookaheadProbeResult[] results =
        [
            new(ParserLookaheadProbeKind.ImmediateReject, "ID", text),
            new(ParserLookaheadProbeKind.ImmediateReject, "ID", text, []),
            new(ParserLookaheadProbeKind.RequiresParse, "ID", text, ["B", "A", "B"]),
            new(ParserLookaheadProbeKind.EpsilonPossible, "ID", otherText, ["A"]),
            new(ParserLookaheadProbeKind.Unknown, null, null),
            new((ParserLookaheadProbeKind)1000, "ID", text),
        ];

        for (var i = 0; i < results.Length; i++)
        {
            Assert.IsTrue(cache.TryAdd(new ParserLookaheadKey("root", 7, i, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1), results[i]));
        }

        Assert.AreEqual(1, cache.OverflowCount, "a kind outside the enumeration is stored unchanged");
        for (var i = 0; i < results.Length; i++)
        {
            Assert.IsTrue(cache.TryGet(new ParserLookaheadKey("root", 7, i, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1), out var found));
            Assert.AreEqual(results[i], found);
            Assert.AreSame(results[i].TokenRuleName, found.TokenRuleName);
            Assert.AreSame(results[i].TokenText, found.TokenText);
            Assert.AreSame(results[i].ExpectedTokenNames, found.ExpectedTokenNames);
        }

        Assert.IsNull(Lookup(cache, 0).ExpectedTokenNames);
        Assert.AreEqual(0, Lookup(cache, 1).ExpectedTokenNames!.Count);
        CollectionAssert.AreEqual(new[] { "B", "A", "B" }, Lookup(cache, 2).ExpectedTokenNames!.ToArray());
        Assert.AreNotSame(Lookup(cache, 0).TokenText, Lookup(cache, 3).TokenText);
    }

    /// <summary>Enumeration lists every entry once, packed and overflow alike, with equal keys and the stored results.</summary>
    [TestMethod]
    public void ParserLookaheadCache_EnumeratesEveryEntry()
    {
        var cache = new ParserLookaheadCache();
        var packed = new ParserLookaheadKey("root", 3, 1, 2, ScheduledAlternativeCursorKinds.LeftRecursiveSeed, 62);
        var overflow = new ParserLookaheadKey("root", -4, 1, 2, ScheduledAlternativeCursorKinds.LeftRecursiveSeed, 63);
        var result = new ParserLookaheadProbeResult(ParserLookaheadProbeKind.ImmediateReject, "ID", "id", ["ID"]);
        cache.TryAdd(packed, result);
        cache.TryAdd(overflow, result);

        var entries = cache.EnumerateEntries().ToDictionary(entry => entry.Key, entry => entry.Value);

        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual(1, cache.OverflowCount);
        Assert.AreEqual(result, entries[packed]);
        Assert.AreEqual(result, entries[overflow]);
    }

    /// <summary>Clearing a large list releases its capacity; clearing a small one keeps it.</summary>
    [TestMethod]
    public void ParserStateRetention_ClearsListsLikeOtherCollections()
    {
        var large = Enumerable.Range(0, ParserStateRetention.MaxRetainedEntries + 1).ToList();
        var small = Enumerable.Range(0, 10).ToList();
        int smallCapacity = small.Capacity;

        ParserStateRetention.Clear(large);
        ParserStateRetention.Clear(small);

        Assert.AreEqual(0, large.Count);
        Assert.AreEqual(0, large.Capacity);
        Assert.AreEqual(0, small.Count);
        Assert.AreEqual(smallCapacity, small.Capacity);
    }

    /// <summary>
    /// Retrieves a cached probe result for an existing alternative in the fixed test context.
    /// </summary>
    /// <param name="cache">The cache containing the expected entry.</param>
    /// <param name="alternativeIndex">The alternative index to look up.</param>
    /// <returns>The cached probe result.</returns>
    private static ParserLookaheadProbeResult Lookup(ParserLookaheadCache cache, int alternativeIndex)
    {
        Assert.IsTrue(cache.TryGet(new ParserLookaheadKey("root", 7, alternativeIndex, 0, ScheduledAlternativeCursorKinds.RuleRoot, -1), out var result));
        return result;
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

        Assert.AreEqual(1, failingMisses,
            "Exactly one memo-miss for 'failing': the first branch pays the full parse cost.");
        Assert.AreEqual(1, failingHits,
            "Exactly one memo-hit for 'failing': the second branch ('failing Y') reuses the cached rejection.");
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
