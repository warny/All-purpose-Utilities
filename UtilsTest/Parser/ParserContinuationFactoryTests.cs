using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserContinuationFactoryTests
{
    [TestMethod]
    public void Create_NonSequenceAlternative_UsesPositionZero()
    {
        var factory = new ParserContinuationFactory();
        var rule = new Rule("expr", 0, false, new Alternation([]));
        var alternative = new Alternative(2, Associativity.Left, new RuleRef("ID"));

        var descriptor = factory.Create(rule, alternative, 0, 3, null, false);

        Assert.AreEqual(0, descriptor.Key.SequencePosition);
    }

    [TestMethod]
    public void Create_SequenceAlternative_UsesMeaningfulSequenceIndex()
    {
        var factory = new ParserContinuationFactory();
        var rule = new Rule("stmt", 0, false, new Alternation([]));
        var sequence = new Sequence([
            new EmbeddedAction("var x = 0;", ActionContext.Alternative, ActionPosition.Inline, []),
            new LiteralMatch("if"),
            new RuleRef("ID")
        ]);
        var alternative = new Alternative(1, Associativity.Left, sequence);

        var literalDescriptor = factory.Create(rule, alternative, 0, 1, null, false);
        var ruleRefDescriptor = factory.Create(rule, alternative, 0, 2, null, false);

        Assert.AreEqual(0, literalDescriptor.Key.SequencePosition);
        Assert.AreEqual(1, ruleRefDescriptor.Key.SequencePosition);
    }

    [TestMethod]
    public void Create_PreservesExpectedTokenNames()
    {
        var factory = new ParserContinuationFactory();
        var rule = new Rule("expr", 0, false, new Alternation([]));
        var alternative = new Alternative(0, Associativity.Left, new RuleRef("ID"));
        IReadOnlyList<string> expectedTokenNames = ["ID", "NUMBER"];

        var descriptor = factory.Create(rule, alternative, 0, 0, expectedTokenNames, false);

        CollectionAssert.AreEqual(expectedTokenNames.ToArray(), descriptor.ExpectedTokenNames?.ToArray() ?? []);
    }

    [TestMethod]
    public void Create_PreservesSharedPrefixFlag()
    {
        var factory = new ParserContinuationFactory();
        var rule = new Rule("expr", 0, false, new Alternation([]));
        var alternative = new Alternative(0, Associativity.Left, new RuleRef("ID"));

        var descriptor = factory.Create(rule, alternative, 0, 0, null, true);

        Assert.IsTrue(descriptor.IsSharedPrefixCandidate);
    }

    [TestMethod]
    public void Create_AlternativeIndex_IsIndependentFromPriority()
    {
        var factory = new ParserContinuationFactory();
        var rule = new Rule("expr", 0, false, new Alternation([]));
        var firstAlternative = new Alternative(0, Associativity.Left, new RuleRef("ID"));
        var secondAlternative = new Alternative(0, Associativity.Left, new RuleRef("NUMBER"));

        var firstDescriptor = factory.Create(rule, firstAlternative, 0, 0, null, false);
        var secondDescriptor = factory.Create(rule, secondAlternative, 1, 0, null, false);

        Assert.AreNotEqual(firstDescriptor.Key, secondDescriptor.Key);
    }

    [TestMethod]
    public void ContinuationKey_EqualityUsesRuleAlternativeAndPosition()
    {
        var first = new ParserContinuationKey("expr", 1, 2);
        var second = new ParserContinuationKey("expr", 1, 2);

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void ContinuationKey_DifferentPositions_AreDistinct()
    {
        var first = new ParserContinuationKey("expr", 1, 1);
        var second = new ParserContinuationKey("expr", 1, 2);

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void ContinuationDescriptor_DoesNotContainRuntimeState()
    {
        var descriptorType = typeof(ParserContinuationDescriptor);

        Assert.IsNull(descriptorType.GetProperty("ParseNode"));
        Assert.IsNull(descriptorType.GetProperty("ParserContext"));
        Assert.IsNull(descriptorType.GetProperty("TokenReader"));
        Assert.IsNull(descriptorType.GetProperty("TokenStream"));
        Assert.AreEqual(0, descriptorType.GetProperties().Count(static p => typeof(Delegate).IsAssignableFrom(p.PropertyType)));
    }

    [TestMethod]
    public void ComputeSharedPrefixSequencePosition_SequenceWithSharedFirstToken_ReturnsOne()
    {
        var factory = new ParserContinuationFactory();
        var alternative = new Alternative(0, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("+"), new RuleRef("expr")]));

        var result = factory.ComputeSharedPrefixSequencePosition(alternative, "ID");

        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public void ComputeSharedPrefixSequencePosition_IgnoresEmbeddedActionAndLexerCommand()
    {
        var factory = new ParserContinuationFactory();
        var alternative = new Alternative(0, Associativity.Left, new Sequence([
            new EmbeddedAction("x();", ActionContext.Alternative, ActionPosition.Inline, []),
            new LexerCommand(LexerCommandType.Skip, null),
            new RuleRef("ID"),
            new LiteralMatch("-")
        ]));

        var result = factory.ComputeSharedPrefixSequencePosition(alternative, "ID");

        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public void ComputeSharedPrefixSequencePosition_UnsupportedStructure_FallsBackToZero()
    {
        var factory = new ParserContinuationFactory();
        var alternative = new Alternative(0, Associativity.Left, new RuleRef("expr"));

        var result = factory.ComputeSharedPrefixSequencePosition(alternative, "ID");

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void ComputeSharedPrefixSequencePosition_RuleRefNameMismatch_FallsBackToZero()
    {
        var factory = new ParserContinuationFactory();
        var alternative = new Alternative(0, Associativity.Left, new Sequence([new RuleRef("expr"), new RuleRef("tail")]));

        var result = factory.ComputeSharedPrefixSequencePosition(alternative, "ID");

        Assert.AreEqual(0, result);
    }
}
