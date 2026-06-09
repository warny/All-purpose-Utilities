using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>Tests for <see cref="ParserRawNamedArgumentSplitter.SplitNamedTopLevel"/>.</summary>
[TestClass]
public class ParserRawNamedArgumentSplitterTests
{
    [TestMethod]
    public void SplitNamed_Null_ThrowsArgumentNullException()
        => Assert.ThrowsException<ArgumentNullException>(() => ParserRawNamedArgumentSplitter.SplitNamedTopLevel(null!));

    [TestMethod]
    public void SplitNamed_Empty_ReturnsEmptyDictionary()
    {
        var r = ParserRawNamedArgumentSplitter.SplitNamedTopLevel("");
        Assert.AreEqual(0, r.Count);
    }

    [TestMethod]
    public void SplitNamed_Whitespace_ReturnsEmptyDictionary()
    {
        var r = ParserRawNamedArgumentSplitter.SplitNamedTopLevel("   ");
        Assert.AreEqual(0, r.Count);
    }

    [TestMethod]
    public void SplitNamed_ColonSeparator_ParsesCorrectly()
    {
        var r = ParserRawNamedArgumentSplitter.SplitNamedTopLevel("value: 42, text: \"hello\"");
        Assert.AreEqual(2, r.Count);
        Assert.AreEqual("42", r["value"]);
        Assert.AreEqual("\"hello\"", r["text"]);
    }

    [TestMethod]
    public void SplitNamed_EqualsSeparator_ParsesCorrectly()
    {
        var r = ParserRawNamedArgumentSplitter.SplitNamedTopLevel("value = 42, text = \"hello\"");
        Assert.AreEqual(2, r.Count);
        Assert.AreEqual("42", r["value"]);
        Assert.AreEqual("\"hello\"", r["text"]);
    }

    [TestMethod]
    public void SplitNamed_MixedSeparatorsColonOrEquals_ParsesCorrectly()
    {
        var r = ParserRawNamedArgumentSplitter.SplitNamedTopLevel("value: 42, text = \"hello\"");
        Assert.AreEqual(2, r.Count);
        Assert.AreEqual("42", r["value"]);
        Assert.AreEqual("\"hello\"", r["text"]);
    }

    [TestMethod]
    public void SplitNamed_ColonOnly_IgnoresEquals()
    {
        // "text = hello" has no colon — should throw FormatException in ColonOnly mode
        Assert.ThrowsException<FormatException>(() =>
            ParserRawNamedArgumentSplitter.SplitNamedTopLevel("value: 42, text = hello",
                ParserRawNamedArgumentSeparatorMode.ColonOnly));
    }

    [TestMethod]
    public void SplitNamed_EqualsOnly_IgnoresColon()
    {
        Assert.ThrowsException<FormatException>(() =>
            ParserRawNamedArgumentSplitter.SplitNamedTopLevel("value: 42, text = hello",
                ParserRawNamedArgumentSeparatorMode.EqualsOnly));
    }

    [TestMethod]
    public void SplitNamed_NestedSeparatorsIgnored()
    {
        var r = ParserRawNamedArgumentSplitter.SplitNamedTopLevel("value: foo(1:2, 3), text: \"a:b,c\"");
        Assert.AreEqual(2, r.Count);
        Assert.AreEqual("foo(1:2, 3)", r["value"]);
        Assert.AreEqual("\"a:b,c\"", r["text"]);
    }

    [TestMethod]
    public void SplitNamed_QuotedSeparators_NotSplit()
    {
        var r = ParserRawNamedArgumentSplitter.SplitNamedTopLevel("x: \"a=b\", y: 'c:d'");
        Assert.AreEqual(2, r.Count);
        Assert.AreEqual("\"a=b\"", r["x"]);
        Assert.AreEqual("'c:d'", r["y"]);
    }

    [TestMethod]
    public void SplitNamed_EscapedQuotes_PreservedInValue()
    {
        var r = ParserRawNamedArgumentSplitter.SplitNamedTopLevel("x: \"a\\\"b\"");
        Assert.AreEqual(1, r.Count);
        Assert.AreEqual("\"a\\\"b\"", r["x"]);
    }

    [TestMethod]
    public void SplitNamed_EmptyValue_Allowed()
    {
        var r = ParserRawNamedArgumentSplitter.SplitNamedTopLevel("value:");
        Assert.AreEqual(1, r.Count);
        Assert.AreEqual("", r["value"]);
    }

    [TestMethod]
    public void SplitNamed_MissingSeparator_ThrowsFormatException()
    {
        Assert.ThrowsException<FormatException>(() =>
            ParserRawNamedArgumentSplitter.SplitNamedTopLevel("justvalue"));
    }

    [TestMethod]
    public void SplitNamed_EmptyKey_ThrowsFormatException()
    {
        Assert.ThrowsException<FormatException>(() =>
            ParserRawNamedArgumentSplitter.SplitNamedTopLevel(": somevalue"));
    }

    [TestMethod]
    public void SplitNamed_DuplicateKeys_LastWins()
    {
        var r = ParserRawNamedArgumentSplitter.SplitNamedTopLevel("x: 1, x: 2");
        Assert.AreEqual(1, r.Count);
        Assert.AreEqual("2", r["x"]);
    }

    [TestMethod]
    public void SplitNamed_WhitespaceTrimmed()
    {
        var r = ParserRawNamedArgumentSplitter.SplitNamedTopLevel("  value  :  42  ");
        Assert.AreEqual(1, r.Count);
        Assert.AreEqual("42", r["value"]);
    }
}

/// <summary>Tests for <see cref="ParserRawNamedArgumentParameterMapping"/>.</summary>
[TestClass]
public class ParserRawNamedArgumentParameterMappingTests
{
    [TestMethod]
    public void Record_RequiredProperties_CanBeInitialized()
    {
        var m = new ParserRawNamedArgumentParameterMapping
        {
            ParameterName = "value",
            ArgumentName = "val",
            Map = s => int.Parse(s),
        };
        Assert.AreEqual("value", m.ParameterName);
        Assert.AreEqual("val", m.ArgumentName);
        Assert.AreEqual(42, m.Map("42"));
    }

    [TestMethod]
    public void Record_WithExpression_CreatesUpdatedCopy()
    {
        Func<string, object?> map = s => s;
        var original = new ParserRawNamedArgumentParameterMapping
        {
            ParameterName = "a", ArgumentName = "b", Map = map,
        };
        var copy = original with { ParameterName = "z" };
        Assert.AreEqual("z", copy.ParameterName);
        Assert.AreEqual("b", copy.ArgumentName);
        Assert.AreSame(map, copy.Map);
    }
}
