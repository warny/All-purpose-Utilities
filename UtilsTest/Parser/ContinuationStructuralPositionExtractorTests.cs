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
            new Quantifier(new RuleRef("ID"), 0, null),
            new RuleRef("NUMBER")]), "A");

        var extractor = new ContinuationStructuralPositionExtractor();
        var position = extractor.ExtractSharedPrefixSequencePosition(alternative, "ID");

        Assert.AreEqual(0, position);
    }

    [TestMethod]
    public void ExtractSharedPrefixSequencePosition_ReturnsZero_ForNonSequenceAlternative()
    {
        var alternative = new Alternative(0, Associativity.Left, new LiteralMatch("id"), "A");

        var extractor = new ContinuationStructuralPositionExtractor();
        var position = extractor.ExtractSharedPrefixSequencePosition(alternative, "id");

        Assert.AreEqual(0, position);
    }

    [TestMethod]
    public void ExtractSharedPrefixSequencePosition_ReturnsZero_ForNonMatchingToken()
    {
        var alternative = new Alternative(0, Associativity.Left, new Sequence([
            new RuleRef("NUMBER"),
            new RuleRef("ID")]), "A");

        var extractor = new ContinuationStructuralPositionExtractor();
        var position = extractor.ExtractSharedPrefixSequencePosition(alternative, "ID");

        Assert.AreEqual(0, position);
    }

    [TestMethod]
    public void ExtractSharedPrefixSequencePosition_ReturnsOne_ForDirectMatchingRuleRef()
    {
        var alternative = new Alternative(0, Associativity.Left, new Sequence([
            new RuleRef("ID"),
            new RuleRef("NUMBER")]), "A");

        var extractor = new ContinuationStructuralPositionExtractor();
        var position = extractor.ExtractSharedPrefixSequencePosition(alternative, "ID");

        Assert.AreEqual(1, position);
    }

    [TestMethod]
    public void ExtractSharedPrefixSequencePosition_ReturnsOne_ForMatchingLiteralFirst()
    {
        var alternative = new Alternative(0, Associativity.Left, new Sequence([
            new LiteralMatch("id"),
            new LiteralMatch("+"),
            new RuleRef("expr")]));

        var extractor = new ContinuationStructuralPositionExtractor();
        var position = extractor.ExtractSharedPrefixSequencePosition(alternative, "id");

        Assert.AreEqual(1, position);
    }

    [TestMethod]
    public void ExtractSharedPrefixSequencePosition_SkipsActionsAndLexerCommandsBeforeSharedToken()
    {
        var alternative = new Alternative(0, Associativity.Left, new Sequence([
            new EmbeddedAction("x();", ActionContext.Alternative, ActionPosition.Inline, []),
            new LexerCommand(LexerCommandType.Skip, null),
            new RuleRef("ID"),
            new LiteralMatch("-")]));

        var extractor = new ContinuationStructuralPositionExtractor();
        var position = extractor.ExtractSharedPrefixSequencePosition(alternative, "ID");

        Assert.AreEqual(1, position);
    }

    [TestMethod]
    public void ExtractSharedPrefixSequencePosition_ReturnsZero_WhenRuleRefNameMismatches()
    {
        var alternative = new Alternative(0, Associativity.Left, new Sequence([
            new RuleRef("expr"),
            new RuleRef("tail")]));

        var extractor = new ContinuationStructuralPositionExtractor();
        var position = extractor.ExtractSharedPrefixSequencePosition(alternative, "ID");

        Assert.AreEqual(0, position);
    }
}
