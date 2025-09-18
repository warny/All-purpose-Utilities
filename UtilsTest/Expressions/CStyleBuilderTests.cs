using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;
using Utils.Expressions.Builders;

namespace UtilsTest.Expressions;

[TestClass]
public class CStyleBuilderTests
{
    [TestMethod]
    public void SymbolsExposeCoreSyntaxTokens()
    {
        var builder = new CStyleBuilder();
        var symbols = builder.Symbols.ToArray();

        CollectionAssert.Contains(symbols, ";");
        CollectionAssert.Contains(symbols, ",");
        CollectionAssert.Contains(symbols, " ");
        CollectionAssert.Contains(symbols, "=>");
        CollectionAssert.Contains(symbols, "if");
        CollectionAssert.Contains(symbols, "+");
    }

    [TestMethod]
    public void TokenReadersMaintainExpectedOrder()
    {
        var builder = new CStyleBuilder();
        var readers = builder.TokenReaders.ToArray();

        Assert.AreEqual("TryReadInterpolatedString1", readers[0].Method.Name);
        Assert.AreEqual("TryReadInterpolatedString2", readers[1].Method.Name);
        Assert.AreEqual("TryReadInterpolatedString3", readers[2].Method.Name);
        Assert.AreEqual("TryReadName", readers[3].Method.Name);
    }

    [TestMethod]
    public void IntegerPrefixesIncludeCommonBases()
    {
        var builder = new CStyleBuilder();

        Assert.AreEqual(16, builder.IntegerPrefixes["0x"]);
        Assert.AreEqual(2, builder.IntegerPrefixes["0b"]);
        Assert.AreEqual(8, builder.IntegerPrefixes["0o"]);
    }

    [TestMethod]
    public void FollowUpExpressionBuildersIncludeAssignmentOperators()
    {
        var builder = new CStyleBuilder();

        Assert.IsTrue(builder.FollowUpExpressionBuilder.ContainsKey("+="));
        Assert.IsTrue(builder.FollowUpExpressionBuilder.ContainsKey("-="));
        Assert.IsNotNull(builder.FallbackBinaryOrTernaryBuilder);
    }

    /// <summary>
    /// Verifies that <c>TryReadString1</c> recognises escaped characters and reports the correct token length.
    /// </summary>
    [TestMethod]
    public void TryReadString1ReadsEscapedSegments()
    {
        bool matched = InvokeStringReader("TryReadString1", "\"value\\\"\"", 0, out int length);

        Assert.IsTrue(matched);
        Assert.AreEqual(9, length);
    }

    /// <summary>
    /// Ensures that <c>TryReadString1</c> rejects triple-quoted raw strings so they can be processed by the dedicated reader.
    /// </summary>
    [TestMethod]
    public void TryReadString1RejectsRawStrings()
    {
        bool matched = InvokeStringReader("TryReadString1", "\"\"\"raw\"\"\"", 0, out int length);

        Assert.IsFalse(matched);
        Assert.AreEqual(0, length);
    }

    /// <summary>
    /// Confirms that <c>TryReadString2</c> stops at the correct closing quote while allowing doubled quotes inside.
    /// </summary>
    [TestMethod]
    public void TryReadString2HandlesEmbeddedQuotes()
    {
        bool matched = InvokeStringReader("TryReadString2", "@\"value\"\"more\"", 0, out int length);

        Assert.IsTrue(matched);
        Assert.AreEqual(14, length);
    }

    /// <summary>
    /// Validates that <c>TryReadString3</c> honours longer raw string delimiters.
    /// </summary>
    [TestMethod]
    public void TryReadString3TracksVariableDelimiters()
    {
        bool matched = InvokeStringReader("TryReadString3", "\"\"\"\"quoted\"\"\"\"\"", 0, out int length);

        Assert.IsTrue(matched);
        Assert.AreEqual(14, length);
    }

    /// <summary>
    /// Invokes the requested string reader and returns its boolean result while exposing the parsed length.
    /// </summary>
    /// <param name="methodName">Name of the private reader to invoke.</param>
    /// <param name="content">Input source code.</param>
    /// <param name="index">Start index provided to the reader.</param>
    /// <param name="length">Receives the parsed token length.</param>
    /// <returns>The boolean result returned by the invoked reader.</returns>
    private static bool InvokeStringReader(string methodName, string content, int index, out int length)
    {
        MethodInfo method = typeof(CStyleBuilder).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Missing method {methodName}.");

        object[] parameters = [content, index, 0];
        try
        {
            bool result = (bool)method.Invoke(null, parameters)!;
            length = (int)parameters[2]!;
            return result;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

}
