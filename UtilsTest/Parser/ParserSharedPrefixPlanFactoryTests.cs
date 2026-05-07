using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserSharedPrefixPlanFactoryTests
{
    [TestMethod]
    public void CreatePlans_ReturnsEmpty_WhenNoCandidates()
    {
        var factory = new ParserSharedPrefixPlanFactory();

        var result = factory.CreatePlans([], [Continuation(0, ["ID"]), Continuation(1, ["ID"])]);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CreatePlans_ReturnsEmpty_WhenNoMatchingContinuations()
    {
        var factory = new ParserSharedPrefixPlanFactory();
        var candidates = new[] { Candidate("ID", [0, 1]) };

        var result = factory.CreatePlans(candidates, [Continuation(0, ["NUMBER"]), Continuation(1, ["NUMBER"])]);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CreatePlans_CreatesPlan_ForSharedAlternatives()
    {
        var factory = new ParserSharedPrefixPlanFactory();
        var candidates = new[] { Candidate("ID", [0, 1]) };

        var result = factory.CreatePlans(candidates, [Continuation(0, ["ID"]), Continuation(1, ["ID"])]);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("ID", result[0].SharedTokenName);
        CollectionAssert.AreEqual(new[] { 0, 1 }, result[0].AlternativeIndexes.ToArray());
        Assert.AreEqual(2, result[0].Continuations.Count);
    }

    [TestMethod]
    public void CreatePlans_PreservesCandidateOrdering()
    {
        var factory = new ParserSharedPrefixPlanFactory();
        var candidates = new[]
        {
            Candidate("ID", [0, 1]),
            Candidate("NUMBER", [2, 3])
        };

        var result = factory.CreatePlans(candidates,
        [
            Continuation(0, ["ID"]),
            Continuation(1, ["ID"]),
            Continuation(2, ["NUMBER"]),
            Continuation(3, ["NUMBER"])
        ]);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("ID", result[0].SharedTokenName);
        Assert.AreEqual("NUMBER", result[1].SharedTokenName);
    }

    [TestMethod]
    public void CreatePlans_FiltersNonMatchingContinuations()
    {
        var factory = new ParserSharedPrefixPlanFactory();
        var candidates = new[] { Candidate("ID", [0, 1, 2]) };

        var result = factory.CreatePlans(candidates,
        [
            Continuation(0, ["ID"]),
            Continuation(1, ["NUMBER"]),
            Continuation(2, ["ID"])
        ]);

        Assert.AreEqual(1, result.Count);
        CollectionAssert.AreEqual(new[] { 0, 2 }, result[0].AlternativeIndexes.ToArray());
    }

    [TestMethod]
    public void CreatePlans_SkipsPlansWithLessThanTwoContinuations()
    {
        var factory = new ParserSharedPrefixPlanFactory();
        var candidates = new[] { Candidate("ID", [0, 1, 2]) };

        var result = factory.CreatePlans(candidates,
        [
            Continuation(0, ["ID"]),
            Continuation(1, ["NUMBER"]),
            Continuation(2, ["NUMBER"])
        ]);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CreatePlans_PreservesContinuationOrderByAlternativeIndex()
    {
        var factory = new ParserSharedPrefixPlanFactory();
        var candidates = new[] { Candidate("ID", [0, 1, 2]) };

        var result = factory.CreatePlans(candidates,
        [
            Continuation(2, ["ID"]),
            Continuation(0, ["ID"]),
            Continuation(1, ["ID"])
        ]);

        Assert.AreEqual(1, result.Count);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, result[0].AlternativeIndexes.ToArray());
    }

    private static ParserLookaheadSharedPrefixCandidate Candidate(string tokenName, IReadOnlyList<int> alternatives)
    {
        return new ParserLookaheadSharedPrefixCandidate(tokenName, alternatives);
    }

    private static ParserContinuationDescriptor Continuation(int alternativeIndex, IReadOnlyList<string> expectedTokenNames)
    {
        return new ParserContinuationDescriptor(
            new ParserContinuationKey("expr", alternativeIndex, 0),
            expectedTokenNames,
            true);
    }
}
