using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Expressions;
using Utils.String;

namespace UtilsTest.String;

[TestClass]
public class StringUtilsTests
{
    // ------------------------------------------------------------------ basic splitting

    [TestMethod]
    public void ParseCommandLineSplitsUnquotedArgumentsTest()
    {
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, StringUtils.ParseCommandLine("a b c"));
    }

    [TestMethod]
    public void ParseCommandLineKeepsSpacesInsideQuotesTest()
    {
        CollectionAssert.AreEqual(new[] { "a b", "c" }, StringUtils.ParseCommandLine("\"a b\" c"));
    }

    [TestMethod]
    public void ParseCommandLine_MultipleSpaces_ProduceSingleSeparator()
    {
        // Consecutive unquoted spaces must not produce empty tokens (#57).
        CollectionAssert.AreEqual(new[] { "a", "b" }, StringUtils.ParseCommandLine("a   b"));
    }

    [TestMethod]
    public void ParseCommandLine_LeadingAndTrailingSpaces_AreIgnored()
    {
        CollectionAssert.AreEqual(new[] { "a", "b" }, StringUtils.ParseCommandLine("  a b  "));
    }

    // ------------------------------------------------------------------ #57 empty quoted arguments

    [TestMethod]
    public void ParseCommandLine_EmptyQuotedArgument_IsPreserved()
    {
        // An explicitly quoted empty value must be preserved (#57).
        string[] result = StringUtils.ParseCommandLine("a \"\" b");
        CollectionAssert.AreEqual(new[] { "a", "", "b" }, result);
    }

    [TestMethod]
    public void ParseCommandLine_QuotedWhitespace_IsPreserved()
    {
        // Quoted whitespace is a legitimate non-empty argument (#57).
        string[] result = StringUtils.ParseCommandLine("a \" \" b");
        CollectionAssert.AreEqual(new[] { "a", " ", "b" }, result);
    }

    [TestMethod]
    public void ParseCommandLine_UnterminatedQuote_ThrowsFormatException()
    {
        // An unterminated quote must throw FormatException with a useful message (#57).
        Assert.ThrowsExactly<FormatException>(() => StringUtils.ParseCommandLine("\"abc"));
    }

    [TestMethod]
    public void ParseCommandLine_UnterminatedQuote_MessageContainsIndex()
    {
        // The exception message must expose the position of the opening quote (#57).
        try
        {
            StringUtils.ParseCommandLine("abc \"def");
            Assert.Fail("Expected FormatException.");
        }
        catch (FormatException ex)
        {
            StringAssert.Contains(ex.Message, "4"); // quote is at index 4
        }
    }

    [TestMethod]
    public void ParseCommandLine_NullInput_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => StringUtils.ParseCommandLine(null));
    }

    // ------------------------------------------------------------------ #58 doubled-quote escape

    [TestMethod]
    public void ParseCommandLineUnescapesDoubledQuotesConsistentlyTest()
    {
        // Both arguments contain an escaped internal quote ("" -> "). The doubled quote is
        // consumed atomically inside the quoted state (#58).
        string[] result = StringUtils.ParseCommandLine("\"a\"\"b\" \"c\"\"d\"");
        CollectionAssert.AreEqual(new[] { "a\"b", "c\"d" }, result);
    }

    [TestMethod]
    public void ParseCommandLineUnescapesDoubledQuotesInLastArgumentTest()
    {
        string[] result = StringUtils.ParseCommandLine("\"c\"\"d\"");
        CollectionAssert.AreEqual(new[] { "c\"d" }, result);
    }

    [TestMethod]
    public void ParseCommandLine_DoubledQuote_DoesNotSplitOnSpaceAfterSecondQuote()
    {
        // "a""b" c — the doubled quote is an escape; c is a separate token.
        string[] result = StringUtils.ParseCommandLine("\"a\"\"b\" c");
        CollectionAssert.AreEqual(new[] { "a\"b", "c" }, result);
    }

    [TestMethod]
    public void ParseCommandLine_QuoteFollowedBySpace_ClosesTokenCorrectly()
    {
        // "hello" world — quote closes at the space and world is a new token.
        string[] result = StringUtils.ParseCommandLine("\"hello\" world");
        CollectionAssert.AreEqual(new[] { "hello", "world" }, result);
    }

    [TestMethod]
    public void ParseCommandLine_EmptyInput_ReturnsEmptyArray()
    {
        CollectionAssert.AreEqual(new string[0], StringUtils.ParseCommandLine(""));
    }
}

// ------------------------------------------------------------------ #59, #60 SplitCommaSeparatedList

[TestClass]
public class SplitCommaSeparatedListTests
{
    private static readonly Parenthesis Round = new("(", ")");
    private static readonly Parenthesis Square = new("[", "]");
    private static readonly Parenthesis Angle = new("<", ">");

    [TestMethod]
    public void Split_SimpleList_ProducesTokens()
    {
        var result = "a,b,c".SplitCommaSeparatedList(',', Round);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, result.ToArray());
    }

    [TestMethod]
    public void Split_NestedMarkers_DoNotSplitInside()
    {
        var result = "a,(b,c),d".SplitCommaSeparatedList(',', Round);
        CollectionAssert.AreEqual(new[] { "a", "(b,c)", "d" }, result.ToArray());
    }

    [TestMethod]
    public void Split_UnmatchedClosingMarker_ThrowsFormatException()
    {
        // Stack underflow must throw FormatException, not InvalidOperationException (#59).
        Assert.ThrowsExactly<FormatException>(() =>
            "a,b),c".SplitCommaSeparatedList(',', Round).ToList());
    }

    [TestMethod]
    public void Split_UnclosedOpeningMarker_ThrowsFormatException()
    {
        // A marker left open at end of string must throw (#59).
        Assert.ThrowsExactly<FormatException>(() =>
            "a,(b,c".SplitCommaSeparatedList(',', Round).ToList());
    }

    [TestMethod]
    public void Split_MultiCharacterMarker_MatchesFullToken()
    {
        // Multi-character start/end must be matched as a complete unit, not just first char (#60).
        var result = "a,<b,c>,d".SplitCommaSeparatedList(',', Angle);
        CollectionAssert.AreEqual(new[] { "a", "<b,c>", "d" }, result.ToArray());
    }

    [TestMethod]
    public void Split_MismatchedMarkers_ThrowsFormatException()
    {
        // Opening with Round and closing with Square must be rejected (#60).
        Assert.ThrowsExactly<FormatException>(() =>
            "a,(b,c],d".SplitCommaSeparatedList(',', Round, Square).ToList());
    }

    [TestMethod]
    public void Split_SymmetricMarker_TogglesBehavior()
    {
        // A symmetric marker (start == end) opens and closes alternately.
        var quoteMarker = new Parenthesis("'", "'");
        var result = "a,'b,c',d".SplitCommaSeparatedList(',', quoteMarker);
        CollectionAssert.AreEqual(new[] { "a", "'b,c'", "d" }, result.ToArray());
    }

    [TestMethod]
    public void Split_RemoveEmptyEntries_DropsEmptyTokens()
    {
        var result = "a,,b".SplitCommaSeparatedList(',', true, Round);
        CollectionAssert.AreEqual(new[] { "a", "b" }, result.ToArray());
    }

    [TestMethod]
    public void Split_NestedDifferentMarkers_BothTracked()
    {
        var result = "a,(b,[c,d]),e".SplitCommaSeparatedList(',', Round, Square);
        CollectionAssert.AreEqual(new[] { "a", "(b,[c,d])", "e" }, result.ToArray());
    }
}

// ------------------------------------------------------------------ #61 Brackets class

[TestClass]
public class BracketsTests
{
    [TestMethod]
    public void Brackets_StringConstructor_AcceptsExactlyTwoChars()
    {
        var b = new Brackets("()");
        Assert.AreEqual('(', b.Open);
        Assert.AreEqual(')', b.Close);
    }

    [TestMethod]
    public void Brackets_StringConstructor_ThrowsOnNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Brackets((string)null));
    }

    [TestMethod]
    public void Brackets_StringConstructor_ThrowsOnEmptyString()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _ = new Brackets(""));
    }

    [TestMethod]
    public void Brackets_StringConstructor_ThrowsOnOneChar()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _ = new Brackets("("));
    }

    [TestMethod]
    public void Brackets_StringConstructor_ThrowsOnThreeChars()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _ = new Brackets("(ab)"));
    }

    [TestMethod]
    public void Brackets_All_ReturnsFreshCopyEachCall()
    {
        // Mutating one returned array must not affect the next call (#61).
        var first = Brackets.All;
        first[0] = null;  // mutate returned array

        var second = Brackets.All;
        Assert.IsNotNull(second[0], "Brackets.All must return a defensive copy; mutation of one call's result must not affect another.");
    }

    [TestMethod]
    public void Brackets_All_ContainsThreeBuiltinPairs()
    {
        Assert.AreEqual(3, Brackets.All.Length);
    }
}
