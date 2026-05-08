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
    public void FormatPlans_FormatsSinglePlan()
    {
        var formatter = new ParserSharedPrefixPlanFormatter();
        var plan = Plan("ID", 1, [Continuation(0, 1), Continuation(1, 1)]);

        var lines = formatter.FormatPlans([plan]);

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual("shared token: ID | alt 0 -> after position 1 | alt 1 -> after position 1", lines[0]);
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
        Assert.AreEqual("shared token: ID | alt 0 -> after position 1 | alt 1 -> after position 1", lines[0]);
        Assert.AreEqual("shared token: NUMBER | alt 2 -> after position 2 | alt 3 -> after position 2", lines[1]);
    }

    [TestMethod]
    public void FormatPlans_IncludesBoundaryFallback_WhenContinuationPositionsDiverge()
    {
        var formatter = new ParserSharedPrefixPlanFormatter();
        var plan = Plan("ID", 0, [Continuation(0, 1), Continuation(1, 2)]);

        var lines = formatter.FormatPlans([plan]);

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(
            "shared token: ID | boundary: position 0 | alt 0 -> after position 1 | alt 1 -> after position 2",
            lines[0]);
    }

    [TestMethod]
    public void FormatPlans_UsesContinuationPositions()
    {
        var formatter = new ParserSharedPrefixPlanFormatter();
        var plan = Plan("ID", 3, [Continuation(0, 3), Continuation(1, 3)]);

        var lines = formatter.FormatPlans([plan]);

        StringAssert.Contains(lines[0], "after position 3");
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
