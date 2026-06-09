using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Tests for <see cref="ParserRawArgumentSplitter.SplitTopLevel"/>.
/// Splitting is syntactic only: no argument is evaluated, no parameter is bound.
/// </summary>
[TestClass]
public class ParserRawArgumentSplitterTests
{
    [TestMethod]
    public void SplitTopLevel_Null_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ParserRawArgumentSplitter.SplitTopLevel(null!));
    }

    [TestMethod]
    public void SplitTopLevel_Empty_ReturnsEmptyList()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("");
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void SplitTopLevel_Whitespace_ReturnsEmptyList()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("   ");
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void SplitTopLevel_SingleArgument_ReturnsTrimmedSingle()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("42");
        CollectionAssert.AreEqual(new[] { "42" }, (List<string>)null! ?? ToList(result));
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("42", result[0]);
    }

    [TestMethod]
    public void SplitTopLevel_SimpleList_SplitsAtTopLevelCommas()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("""1, "x", foo()""");
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("1", result[0]);
        Assert.AreEqual("\"x\"", result[1]);
        Assert.AreEqual("foo()", result[2]);
    }

    [TestMethod]
    public void SplitTopLevel_NestedParentheses_DoesNotSplitInside()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("foo(1, 2), bar");
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("foo(1, 2)", result[0]);
        Assert.AreEqual("bar", result[1]);
    }

    [TestMethod]
    public void SplitTopLevel_NestedBracketsAndBraces_DoesNotSplitInside()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("[1, 2], {a, b}, c");
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("[1, 2]", result[0]);
        Assert.AreEqual("{a, b}", result[1]);
        Assert.AreEqual("c", result[2]);
    }

    [TestMethod]
    public void SplitTopLevel_QuotedCommas_NotSplit()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("\"a,b\", 'c,d', e");
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("\"a,b\"", result[0]);
        Assert.AreEqual("'c,d'", result[1]);
        Assert.AreEqual("e", result[2]);
    }

    [TestMethod]
    public void SplitTopLevel_EscapedQuotesInsideString_PreservedCorrectly()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("\"a,\\\"b\\\"\", c");
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("\"a,\\\"b\\\"\"", result[0]);
        Assert.AreEqual("c", result[1]);
    }

    [TestMethod]
    public void SplitTopLevel_WhitespaceAround_TrimmedFromEachSlice()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("  a  ,   b  ");
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("a", result[0]);
        Assert.AreEqual("b", result[1]);
    }

    [TestMethod]
    public void SplitTopLevel_EmptySegments_Preserved()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("a,,b,");
        Assert.AreEqual(4, result.Count);
        Assert.AreEqual("a", result[0]);
        Assert.AreEqual("", result[1]);
        Assert.AreEqual("b", result[2]);
        Assert.AreEqual("", result[3]);
    }

    [TestMethod]
    public void SplitTopLevel_DeeplyNestedMix_SplitsOnlyAtTopLevel()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("foo(bar, baz), [1, 2, 3], \"a,b\", '{x,y}'");
        Assert.AreEqual(4, result.Count);
        Assert.AreEqual("foo(bar, baz)", result[0]);
        Assert.AreEqual("[1, 2, 3]", result[1]);
        Assert.AreEqual("\"a,b\"", result[2]);
        Assert.AreEqual("'{x,y}'", result[3]);
    }

    [TestMethod]
    public void SplitTopLevel_UnbalancedOpenParen_ConservativelyAbsorbsRemainder()
    {
        // Unbalanced input: paren opened but never closed — absorb rest into current segment.
        var result = ParserRawArgumentSplitter.SplitTopLevel("foo(1, 2, bar");
        Assert.AreEqual(1, result.Count, "Unbalanced open paren: remainder goes into first segment.");
        Assert.AreEqual("foo(1, 2, bar", result[0]);
    }

    [TestMethod]
    public void SplitTopLevel_UnbalancedClosePane_TreatedAsZeroDepth()
    {
        // Depth is clamped to 0, so the close paren is appended and subsequent commas split.
        var result = ParserRawArgumentSplitter.SplitTopLevel("a), b");
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("a)", result[0]);
        Assert.AreEqual("b", result[1]);
    }

    [TestMethod]
    public void SplitTopLevel_EscapedBackslash_HandledCorrectly()
    {
        var result = ParserRawArgumentSplitter.SplitTopLevel("\"a\\\\b\", c");
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("\"a\\\\b\"", result[0]);
        Assert.AreEqual("c", result[1]);
    }

    private static List<string> ToList(IReadOnlyList<string> src)
    {
        var list = new List<string>(src.Count);
        foreach (var s in src) list.Add(s);
        return list;
    }
}
