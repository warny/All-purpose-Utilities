using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}
