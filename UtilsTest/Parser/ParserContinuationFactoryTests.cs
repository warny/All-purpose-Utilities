using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserContinuationFactoryTests
{
    [TestMethod]
    public void Create_PreservesExpectedTokenNames()
    {
        var factory = new ParserContinuationFactory();
        IReadOnlyList<string> expectedTokenNames = ["ID", "NUMBER"];

        var descriptor = factory.Create(new ParserContinuationPreparationInput("expr", 0, 0, expectedTokenNames, false));

        CollectionAssert.AreEqual(expectedTokenNames.ToArray(), descriptor.ExpectedTokenNames?.ToArray() ?? []);
    }

    [TestMethod]
    public void Create_PreservesSharedPrefixFlag()
    {
        var factory = new ParserContinuationFactory();

        var descriptor = factory.Create(new ParserContinuationPreparationInput("expr", 0, 0, null, true));

        Assert.IsTrue(descriptor.IsSharedPrefixCandidate);
    }

    [TestMethod]
    public void Create_AlternativeIndex_IsIndependentFromPriority()
    {
        var factory = new ParserContinuationFactory();

        var firstDescriptor = factory.Create(new ParserContinuationPreparationInput("expr", 0, 0, null, false));
        var secondDescriptor = factory.Create(new ParserContinuationPreparationInput("expr", 1, 0, null, false));

        Assert.AreNotEqual(firstDescriptor.Key, secondDescriptor.Key);
    }

    [TestMethod]
    public void Create_SequencePosition_IsStoredAsProvided()
    {
        var factory = new ParserContinuationFactory();

        var descriptor = factory.Create(new ParserContinuationPreparationInput("expr", 0, 3, null, false));

        Assert.AreEqual(3, descriptor.Key.SequencePosition);
    }

    [TestMethod]
    public void Create_RuleName_IsStoredAsProvided()
    {
        var factory = new ParserContinuationFactory();

        var descriptor = factory.Create(new ParserContinuationPreparationInput("stmt", 2, 1, null, false));

        Assert.AreEqual("stmt", descriptor.Key.RuleName);
        Assert.AreEqual(2, descriptor.Key.AlternativeIndex);
        Assert.AreEqual(1, descriptor.Key.SequencePosition);
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
}
