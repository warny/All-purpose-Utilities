using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserSharedPrefixPlanFormatterTests
{
    [TestMethod]
    public void FormatPlans_ReturnsEmpty_WhenNoPlans()
    {
        var formatter = new ParserSharedPrefixPlanFormatter();

        var lines = formatter.FormatPlans([]);

        Assert.AreEqual(0, lines.Count);
    }

    [TestMethod]
    public void FormatPlans_FormatsSinglePlanAsStructuredBlock()
    {
        var formatter = new ParserSharedPrefixPlanFormatter();
        var plan = Plan("ID", 1, [Continuation(0, 1), Continuation(1, 1)]);

        var lines = formatter.FormatPlans([plan]);

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(
            "shared segment: ID\nboundary: position 1\ncontinuations:\n  alt 0 -> position 1\n  alt 1 -> position 1",
            lines[0]);
    }

    [TestMethod]
    public void FormatPlans_FormatsMultiplePlansInStableOrder()
    {
        var formatter = new ParserSharedPrefixPlanFormatter();
        var plans = new[]
        {
            Plan("ID", 1, [Continuation(0, 1), Continuation(1, 1)]),
            Plan("NUMBER", 2, [Continuation(2, 2), Continuation(3, 2)])
        };

        var lines = formatter.FormatPlans(plans);

        Assert.AreEqual(2, lines.Count);
        StringAssert.StartsWith(lines[0], "shared segment: ID");
        StringAssert.StartsWith(lines[1], "shared segment: NUMBER");
    }

    [TestMethod]
    public void FormatPlans_RendersFallbackBoundary_WhenContinuationPositionsDiverge()
    {
        var formatter = new ParserSharedPrefixPlanFormatter();
        var plan = Plan("ID", 0, [Continuation(0, 1), Continuation(1, 2)]);

        var lines = formatter.FormatPlans([plan]);

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(
            "shared segment: ID\nboundary: position 0 (fallback)\ncontinuations:\n  alt 0 -> position 1\n  alt 1 -> position 2",
            lines[0]);
    }

    [TestMethod]
    public void FormatPlans_PreservesContinuationOrdering()
    {
        var formatter = new ParserSharedPrefixPlanFormatter();
        var plan = Plan("ID", 1, [Continuation(2, 1), Continuation(0, 1), Continuation(1, 1)]);

        var lines = formatter.FormatPlans([plan]);

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(
            "shared segment: ID\nboundary: position 1\ncontinuations:\n  alt 2 -> position 1\n  alt 0 -> position 1\n  alt 1 -> position 1",
            lines[0]);
    }

    [TestMethod]
    public void FormatPlans_IsDeterministicAcrossInvocations()
    {
        var formatter = new ParserSharedPrefixPlanFormatter();
        var plan = Plan("ID", 1, [Continuation(0, 1), Continuation(1, 1)]);

        var first = formatter.FormatPlans([plan]);
        var second = formatter.FormatPlans([plan]);

        CollectionAssert.AreEqual(first.ToArray(), second.ToArray());
    }

    [TestMethod]
    public void FormatPlans_DoesNotAlterSchedulingMetadata()
    {
        var formatter = new ParserSharedPrefixPlanFormatter();
        var continuations = new[] { Continuation(0, 1), Continuation(1, 1) };
        var plan = Plan("ID", 1, continuations);

        _ = formatter.FormatPlans([plan]);

        Assert.AreEqual(2, plan.Continuations.Count);
        Assert.AreEqual(0, plan.Continuations[0].Key.AlternativeIndex);
        Assert.AreEqual(1, plan.Continuations[0].Key.SequencePosition);
        Assert.AreEqual(1, plan.Segment.Boundary.SequencePosition);
    }

    private static ParserSharedPrefixPlan Plan(
        string tokenName,
        int boundaryPosition,
        IReadOnlyList<ParserContinuationDescriptor> continuations)
    {
        var alternativeIndexes = continuations.Select(static c => c.Key.AlternativeIndex).ToArray();
        return new ParserSharedPrefixPlan(
            tokenName,
            alternativeIndexes,
            continuations,
            new ParserSharedPrefixSegment(tokenName, new ParserSharedPrefixBoundary(boundaryPosition, null)));
    }

    private static ParserContinuationDescriptor Continuation(int alternativeIndex, int sequencePosition)
    {
        return new ParserContinuationDescriptor(
            new ParserContinuationKey("expr", alternativeIndex, sequencePosition),
            ["ID"],
            true);
    }
}
