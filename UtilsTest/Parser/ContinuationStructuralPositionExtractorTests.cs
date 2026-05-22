using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ContinuationStructuralPositionExtractorTests
{
    [TestMethod]
    public void ExtractSharedPrefixSequencePosition_ReturnsZero_ForQuantifierLeadingAlternative()
    {
        var alternative = new Alternative(0, Associativity.Left, new Sequence([
            new Quantifier(new RuleRef("ID"), QuantifierMode.ZeroOrMore),
            new RuleRef("NUMBER")]), "A");

        var extractor = new ContinuationStructuralPositionExtractor();
        var position = extractor.ExtractSharedPrefixSequencePosition(alternative, "ID");

        Assert.AreEqual(0, position);
    }
}
