using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.String;

namespace UtilsTest.String;

[TestClass]
public class StringUtilsTests
{
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
    public void ParseCommandLineUnescapesDoubledQuotesConsistentlyTest()
    {
        // Both arguments contain an escaped internal quote ("" -> "). Previously the
        // non-last argument kept the doubled quote while only the last one was unescaped.
        string[] result = StringUtils.ParseCommandLine("\"a\"\"b\" \"c\"\"d\"");
        CollectionAssert.AreEqual(new[] { "a\"b", "c\"d" }, result);
    }

    [TestMethod]
    public void ParseCommandLineUnescapesDoubledQuotesInLastArgumentTest()
    {
        string[] result = StringUtils.ParseCommandLine("\"c\"\"d\"");
        CollectionAssert.AreEqual(new[] { "c\"d" }, result);
    }
}
