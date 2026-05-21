using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class RuntimeTraceAnalyzerTests
{
    /// <summary>
    /// Ensures identical observation sequences always produce identical deterministic summaries.
    /// </summary>
    [TestMethod]
    public void Summarize_IdenticalObservations_ProducesIdenticalSummaries()
    {
        var first = CreateTrace();
        var second = CreateTrace();

        var firstSummary = RuntimeTraceAnalyzer.Summarize(first);
        var secondSummary = RuntimeTraceAnalyzer.Summarize(second);

        Assert.AreEqual(firstSummary.TotalObservations, secondSummary.TotalObservations);
        CollectionAssert.AreEquivalent(firstSummary.EventDistribution.ToArray(), secondSummary.EventDistribution.ToArray());
        CollectionAssert.AreEquivalent(firstSummary.StatusDistribution.ToArray(), secondSummary.StatusDistribution.ToArray());
        CollectionAssert.AreEquivalent(firstSummary.RuleDistribution.ToArray(), secondSummary.RuleDistribution.ToArray());
        CollectionAssert.AreEquivalent(firstSummary.AlternativeDistribution.ToArray(), secondSummary.AlternativeDistribution.ToArray());
    }

    /// <summary>
    /// Ensures deterministic exports of identical observations produce identical comparison results.
    /// </summary>
    [TestMethod]
    public void Compare_IdenticalObservations_ProducesIdenticalExportFlags()
    {
        var first = CreateTrace();
        var second = CreateTrace();

        var comparison = RuntimeTraceAnalyzer.Compare(first, second);

        Assert.IsTrue(comparison.AreTextExportsIdentical);
        Assert.IsTrue(comparison.AreJsonExportsIdentical);
        Assert.AreEqual(first.Length, comparison.FirstTotalObservations);
        Assert.AreEqual(second.Length, comparison.SecondTotalObservations);
        Assert.IsTrue(comparison.EventCountDelta.Values.All(static value => value == 0));
    }

    /// <summary>
    /// Ensures comparison remains deterministic for different observation distributions.
    /// </summary>
    [TestMethod]
    public void Compare_DifferentObservations_ProducesDeterministicDeltas()
    {
        var first = CreateTrace();
        AlternativeRuntimeObservation[] second =
        [
            CreateObservation(ParserRuntimeObservationKind.AlternativeStarted, ParserRuntimeObservationStatus.Active, "entry", 0),
            CreateObservation(ParserRuntimeObservationKind.AlternativeFailed, ParserRuntimeObservationStatus.Failed, "entry", 0),
        ];

        var comparison = RuntimeTraceAnalyzer.Compare(first, second);

        Assert.IsFalse(comparison.AreTextExportsIdentical);
        Assert.IsFalse(comparison.AreJsonExportsIdentical);
        Assert.AreEqual(3, comparison.FirstTotalObservations);
        Assert.AreEqual(2, comparison.SecondTotalObservations);
        Assert.AreEqual(1, comparison.EventCountDelta[ParserRuntimeObservationKind.AlternativeCompleted]);
        Assert.AreEqual(1, comparison.EventCountDelta[ParserRuntimeObservationKind.AlternativeSelected]);
        Assert.AreEqual(-1, comparison.EventCountDelta[ParserRuntimeObservationKind.AlternativeFailed]);
    }

    private static AlternativeRuntimeObservation[] CreateTrace()
    {
        return
        [
            CreateObservation(ParserRuntimeObservationKind.AlternativeStarted, ParserRuntimeObservationStatus.Active, "entry", 0),
            CreateObservation(ParserRuntimeObservationKind.AlternativeCompleted, ParserRuntimeObservationStatus.Completed, "entry", 0),
            CreateObservation(ParserRuntimeObservationKind.AlternativeSelected, ParserRuntimeObservationStatus.Completed, "entry", 0),
        ];
    }

    private static AlternativeRuntimeObservation CreateObservation(
        ParserRuntimeObservationKind kind,
        ParserRuntimeObservationStatus status,
        string rule,
        int alternative)
    {
        return new AlternativeRuntimeObservation(kind, rule, alternative, 0, 0, 0, status);
    }
}
