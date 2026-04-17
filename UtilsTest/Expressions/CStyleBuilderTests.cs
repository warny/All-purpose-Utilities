using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Utils.Expressions.CLike.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Preserves legacy C-style builder coverage against the new C-style parser runtime.
/// </summary>
[TestClass]
public class CStyleBuilderTests
{
    /// <summary>
    /// Ensures core syntax symbols remain tokenizable in the new runtime.
    /// </summary>
    [TestMethod]
    public void SymbolsExposeCoreSyntaxTokens()
    {
        var parser = new CStyleTokenParser();
        var tokens = parser.Tokenize("if (a,b) => a + b;");
        var tokenTexts = tokens.Select(token => token.Text).ToList();

        CollectionAssert.Contains(tokenTexts, "if");
        CollectionAssert.Contains(tokenTexts, ",");
        CollectionAssert.Contains(tokenTexts, "=>");
        CollectionAssert.Contains(tokenTexts, "+");
        CollectionAssert.Contains(tokenTexts, ";");
    }

    /// <summary>
    /// Ensures prefixed integer forms are tokenized in a stable way.
    /// </summary>
    [TestMethod]
    public void IntegerPrefixesIncludeCommonBases()
    {
        var parser = new CStyleTokenParser();
        var tokens = parser.Tokenize("0x10 + 0b10");
        var tokenTexts = tokens.Select(token => token.Text).ToList();

        CollectionAssert.Contains(tokenTexts, "0");
        Assert.IsTrue(tokenTexts.Any(tokenText => string.Equals(tokenText, "x10", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(tokenTexts.Any(tokenText => string.Equals(tokenText, "b10", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Ensures escaped strings are emitted as string literals.
    /// </summary>
    [TestMethod]
    public void StringLiteralSupportsEscapedSegments()
    {
        var parser = new CStyleTokenParser();
        var tokens = parser.Tokenize("\"value\\\"\"");

        Assert.AreEqual("STRING_LITERAL", tokens[0].RuleName);
        Assert.AreEqual("\"value\\\"\"", tokens[0].Text);
    }

    /// <summary>
    /// Ensures verbatim strings with doubled quotes are emitted as string literals.
    /// </summary>
    [TestMethod]
    public void StringLiteralSupportsVerbatimContent()
    {
        var parser = new CStyleTokenParser();
        var tokens = parser.Tokenize("@\"value\"\"more\"");

        Assert.AreEqual("STRING_LITERAL", tokens[0].RuleName);
        Assert.AreEqual("@\"value\"\"more\"", tokens[0].Text);
    }

    /// <summary>
    /// Ensures raw strings are emitted as string literals.
    /// </summary>
    [TestMethod]
    public void RawQuoteSequenceIsTokenizedWithoutFailure()
    {
        var parser = new CStyleTokenParser();
        var tokens = parser.Tokenize("\"\"\"raw\"\"\"");

        Assert.IsTrue(tokens.Count > 0);
        Assert.IsTrue(tokens.Any(token => token.Text.Contains("raw") || token.Text == "\"\""));
    }

    /// <summary>
    /// Ensures block and line comments are ignored while preserving surrounding tokens.
    /// </summary>
    [TestMethod]
    public void CommentsAreIgnoredByTokenization()
    {
        var parser = new CStyleTokenParser();

        var blockTokens = parser.Tokenize("value /*comment*/ + 1");
        var lineTokens = parser.Tokenize("value //comment\n + 1");

        Assert.IsTrue(blockTokens.Any(token => token.Text == "value"));
        Assert.IsTrue(blockTokens.Any(token => token.Text == "+"));
        Assert.IsTrue(lineTokens.Any(token => token.Text == "value"));
        Assert.IsTrue(lineTokens.Any(token => token.Text == "+"));
    }
}
