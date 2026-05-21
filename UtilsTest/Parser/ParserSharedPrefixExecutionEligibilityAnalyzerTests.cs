using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserSharedPrefixExecutionEligibilityAnalyzerTests
{
    [TestMethod]
    public void Analyze_ReturnsEligible_ForSimpleOperatorShape()
    {
        var analyzer = new ParserSharedPrefixExecutionEligibilityAnalyzer();
        var plan = Plan("ID", 1, [Continuation(0, 1), Continuation(1, 1)]);

        var result = analyzer.Analyze(plan);

        Assert.AreEqual(ParserSharedPrefixExecutionEligibility.Eligible, result.Eligibility);
        Assert.AreEqual(0, result.Blockers.Count);
    }

    [TestMethod]
    public void Analyze_ReturnsRequiresFallback_WithSp001AndSp002_ForFallbackDivergence()
    {
        var analyzer = new ParserSharedPrefixExecutionEligibilityAnalyzer();
        var plan = Plan("ID", 0, [Continuation(0, 1), Continuation(1, 2)]);

        var result = analyzer.Analyze(plan);

        Assert.AreEqual(ParserSharedPrefixExecutionEligibility.RequiresFallback, result.Eligibility);
        Assert.IsTrue(result.Blockers.Any(static blocker => blocker.Code == "SP001"));
        Assert.IsTrue(result.Blockers.Any(static blocker => blocker.Code == "SP002"));
    }

    [TestMethod]
    public void Analyze_ReturnsNotEligible_ForInvalidMetadata()
    {
        var analyzer = new ParserSharedPrefixExecutionEligibilityAnalyzer();
        var plan = Plan(" ", 1, []);

        var result = analyzer.Analyze(plan);

        Assert.AreEqual(ParserSharedPrefixExecutionEligibility.NotEligible, result.Eligibility);
        Assert.IsTrue(result.Blockers.Count > 0);
    }

    [TestMethod]
    public void Analyze_ReturnsUnsafe_ForContradictoryMetadata()
    {
        var analyzer = new ParserSharedPrefixExecutionEligibilityAnalyzer();
        var plan = Plan("ID", 1, [Continuation(0, 2), Continuation(1, 3)]);

        var result = analyzer.Analyze(plan);

        Assert.AreEqual(ParserSharedPrefixExecutionEligibility.Unsafe, result.Eligibility);
        Assert.IsTrue(result.Blockers.Any(static blocker => blocker.Code == "SP002"));
    }

    [TestMethod]
    public void Analyze_ReturnsNotEligible_ForDuplicateAlternatives()
    {
        var analyzer = new ParserSharedPrefixExecutionEligibilityAnalyzer();
        var plan = Plan("ID", 1, [Continuation(0, 1), Continuation(0, 1)]);

        var result = analyzer.Analyze(plan);

        Assert.AreEqual(ParserSharedPrefixExecutionEligibility.NotEligible, result.Eligibility);
        Assert.IsTrue(result.Blockers.Any(static blocker => blocker.Code == "SP003"));
    }

    [TestMethod]
    public void Analyze_ReturnsUnsafe_ForNegativeBoundaryPosition()
    {
        var analyzer = new ParserSharedPrefixExecutionEligibilityAnalyzer();
        var plan = Plan("ID", -1, [Continuation(0, 1), Continuation(1, 1)]);

        var result = analyzer.Analyze(plan);

        Assert.AreEqual(ParserSharedPrefixExecutionEligibility.Unsafe, result.Eligibility);
        Assert.IsTrue(result.Blockers.Any(static blocker => blocker.Code == "SP004"));
    }

    [TestMethod]
    public void Analyze_ReturnsUnsafe_ForNegativeContinuationPosition()
    {
        var analyzer = new ParserSharedPrefixExecutionEligibilityAnalyzer();
        var plan = Plan("ID", 1, [Continuation(0, -1), Continuation(1, 1)]);

        var result = analyzer.Analyze(plan);

        Assert.AreEqual(ParserSharedPrefixExecutionEligibility.Unsafe, result.Eligibility);
        Assert.IsTrue(result.Blockers.Any(static blocker => blocker.Code == "SP008"));
    }

    private static ParserSharedPrefixPlan Plan(
        string tokenName,
        int boundaryPosition,
        IReadOnlyList<ParserContinuationDescriptor> continuations)
    {
        var alternativeIndexes = continuations.Select(static continuation => continuation.Key.AlternativeIndex).ToArray();
        return new ParserSharedPrefixPlan(
            tokenName,
            alternativeIndexes,
            continuations,
            new ParserSharedPrefixSegment(tokenName, [tokenName], new ParserSharedPrefixBoundary(boundaryPosition, null)));
    }

    private static ParserContinuationDescriptor Continuation(int alternativeIndex, int sequencePosition)
    {
        return new ParserContinuationDescriptor(
            new ParserContinuationKey("expr", alternativeIndex, sequencePosition),
            ParserContinuationCategory.SharedPrefixCandidate,
            ["ID"],
            true);
    }
}
