using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Expressions;

namespace UtilsTest.Expressions;

/// <summary>
/// Unit tests for <see cref="InterpolatedStringParser"/> focused on parsing correctness,
/// alignment handling (#24), and full-input coverage (#25).
/// </summary>
[TestClass]
public class InterpolatedStringParserTests
{
    // ------------------------------------------------------------------ basic parsing

    [TestMethod]
    public void Parser_ParsesLiteralText()
    {
        var parser = new InterpolatedStringParser("hello world");
        var parts = parser.ToList();
        Assert.AreEqual(1, parts.Count);
        Assert.IsInstanceOfType<LiteralPart>(parts[0]);
        Assert.AreEqual("hello world", ((LiteralPart)parts[0]).Text);
    }

    [TestMethod]
    public void Parser_ParsesSimpleExpression()
    {
        var parser = new InterpolatedStringParser("{name}");
        var parts = parser.ToList();
        Assert.AreEqual(1, parts.Count);
        var fp = (FormattedPart)parts[0];
        Assert.AreEqual("name", fp.ExpressionText.Trim());
        Assert.IsNull(fp.Alignment);
        Assert.IsNull(fp.Format);
    }

    [TestMethod]
    public void Parser_ParsesExpressionWithFormat()
    {
        var parser = new InterpolatedStringParser("{value:F2}");
        var fp = (FormattedPart)parser.Single();
        Assert.AreEqual("F2", fp.Format);
    }

    [TestMethod]
    public void Parser_ParsesPositiveAlignment()
    {
        var parser = new InterpolatedStringParser("{value,10}");
        var fp = (FormattedPart)parser.Single();
        Assert.AreEqual(10, fp.Alignment);
    }

    [TestMethod]
    public void Parser_ParsesNegativeAlignment()
    {
        var parser = new InterpolatedStringParser("{value,-5}");
        var fp = (FormattedPart)parser.Single();
        Assert.AreEqual(-5, fp.Alignment);
    }

    [TestMethod]
    public void Parser_ParsesEscapedBraces()
    {
        var parser = new InterpolatedStringParser("{{}}");
        var parts = parser.ToList();
        // {{ → literal "{", }} → literal "}" — consecutive literals get merged into one part.
        Assert.AreEqual(1, parts.Count);
        Assert.AreEqual("{}", ((LiteralPart)parts[0]).Text);
    }

    [TestMethod]
    public void Parser_ThrowsFormatExceptionOnUnexpectedBrace()
    {
        Assert.ThrowsExactly<FormatException>(() => new InterpolatedStringParser("}bad"));
    }

    // ------------------------------------------------------------------ #24 alignment parsing — FormatException, not OverflowException

    [TestMethod]
    public void Parser_ThrowsFormatException_ForOversizedAlignment()
    {
        // 99999999999 overflows int.MaxValue; must produce FormatException, not OverflowException.
        var ex = Assert.ThrowsExactly<FormatException>(() =>
            new InterpolatedStringParser("{value,99999999999}"));
        StringAssert.Contains(ex.Message, "alignment");
    }

    // ------------------------------------------------------------------ #25 full input coverage

    [TestMethod]
    public void Parser_ParsesEmptyString()
    {
        var parser = new InterpolatedStringParser(string.Empty);
        Assert.AreEqual(0, parser.Count());
    }

    [TestMethod]
    public void Parser_ParsesMixedLiteralAndExpression()
    {
        var parser = new InterpolatedStringParser("Hello {name}, you are {age} years old.");
        var parts = parser.ToList();
        // Expected: literal "Hello ", expr "name", literal ", you are ", expr "age", literal " years old."
        Assert.AreEqual(5, parts.Count);
        Assert.IsInstanceOfType<LiteralPart>(parts[0]);
        Assert.IsInstanceOfType<FormattedPart>(parts[1]);
        Assert.IsInstanceOfType<LiteralPart>(parts[2]);
        Assert.IsInstanceOfType<FormattedPart>(parts[3]);
        Assert.IsInstanceOfType<LiteralPart>(parts[4]);
    }
}
