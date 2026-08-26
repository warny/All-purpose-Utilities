using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>Verifies functional contracts of parser runtime trace result records.</summary>
[TestClass]
public sealed class ParserRuntimeTraceContractTests
{
    /// <summary>Verifies positional trace records retain their public deconstruction contract.</summary>
    [TestMethod]
    public void RuntimeTraceResults_RetainDeconstructionContract()
    {
        var events = new Dictionary<ParserRuntimeObservationKind, int>();
        var statuses = new Dictionary<ParserRuntimeObservationStatus, int>();
        var rules = new Dictionary<string, int>();
        var alternatives = new Dictionary<int, int>();
        var summary = new RuntimeTraceSummary(3, events, statuses, rules, alternatives);
        var comparison = new RuntimeTraceComparison(true, false, true, 3, 2, events);

        var (total, deconstructedEvents, deconstructedStatuses, deconstructedRules, deconstructedAlternatives) = summary;
        var (equivalent, textIdentical, jsonIdentical, firstTotal, secondTotal, eventDelta) = comparison;

        Assert.AreEqual(3, total);
        Assert.AreSame(summary.EventDistribution, deconstructedEvents);
        Assert.AreSame(summary.StatusDistribution, deconstructedStatuses);
        Assert.AreSame(summary.RuleDistribution, deconstructedRules);
        Assert.AreSame(summary.AlternativeDistribution, deconstructedAlternatives);
        Assert.IsTrue(equivalent);
        Assert.IsFalse(textIdentical);
        Assert.IsTrue(jsonIdentical);
        Assert.AreEqual(3, firstTotal);
        Assert.AreEqual(2, secondTotal);
        Assert.AreSame(comparison.EventCountDelta, eventDelta);
    }
}
