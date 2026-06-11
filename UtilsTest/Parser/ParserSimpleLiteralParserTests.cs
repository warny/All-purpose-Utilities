using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies the deliberately limited parser rule-call literal syntax.
/// </summary>
[TestClass]
public class ParserSimpleLiteralParserTests
{
    /// <summary>
    /// Verifies all supported scalar literal categories and surrounding whitespace handling.
    /// </summary>
    [TestMethod]
    public void TryParse_SupportedLiterals_ReturnExpectedValues()
    {
        AssertParsed("null", null);
        AssertParsed("true", true);
        AssertParsed("false", false);
        AssertParsed("42", 42);
        AssertParsed("-42", -42);
        AssertParsed("+42", 42);
        AssertParsed(int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), int.MaxValue);
        AssertParsed("2147483648", 2147483648L);
        AssertParsed(long.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture), long.MinValue);
        AssertParsed("1.5", 1.5d);
        AssertParsed("1e3", 1000d);
        AssertParsed("-1.5e-2", -0.015d);
        AssertParsed("\"hello\"", "hello");
        AssertParsed("\"\"", string.Empty);
        AssertParsed("\"a\\\"b\"", "a\"b");
        AssertParsed("\"a\\\\b\"", "a\\b");
        AssertParsed("\"line\\nbreak\\tend\"", "line\nbreak\tend");
        AssertParsed("'a'", 'a');
        AssertParsed("'\\n'", '\n');
        AssertParsed("'\\\''", '\'');
        AssertParsed("'\\\\'", '\\');
        AssertParsed("  42  ", 42);
    }

    /// <summary>
    /// Verifies unsupported expressions, suffixes, malformed quotes, overflow, and empty text are rejected.
    /// </summary>
    [TestMethod]
    public void TryParse_UnsupportedOrMalformedText_ReturnsFalse()
    {
        string[] unsupported =
        [
            string.Empty,
            "   ",
            "foo",
            "foo()",
            "1 + 2",
            "this.Member",
            "new object()",
            "SomeEnum.Value",
            "(int)42",
            "default",
            "default(int)",
            "nameof(x)",
            "$\"hello\"",
            "@\"hello\"",
            "42L",
            "1.0f",
            "9223372036854775808",
            "1e9999",
            "\"unterminated",
            "\"bad\\xescape\"",
            "\"line\nbreak\"",
            "''",
            "'ab'"
        ];

        foreach (string text in unsupported)
        {
            Assert.IsFalse(ParserSimpleLiteralParser.TryParse(text, out object? value), text);
            Assert.IsNull(value, text);
        }
    }

    /// <summary>
    /// Verifies one supported literal and its exact runtime value and type.
    /// </summary>
    /// <param name="rawText">Raw literal text.</param>
    /// <param name="expected">Expected parsed value.</param>
    private static void AssertParsed(string rawText, object? expected)
    {
        Assert.IsTrue(ParserSimpleLiteralParser.TryParse(rawText, out object? actual), rawText);
        Assert.AreEqual(expected, actual, rawText);
        Assert.AreEqual(expected?.GetType(), actual?.GetType(), rawText);
    }
}
