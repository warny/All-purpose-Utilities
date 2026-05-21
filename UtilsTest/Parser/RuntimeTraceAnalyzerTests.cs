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
    /// Ensures deterministic analysis equivalence is computed from observation-level summaries.
    /// </summary>
    [TestMethod]
    public void Compare_IdenticalObservations_ProducesEquivalentSummaries()
    {
        var first = CreateTrace();
        var second = CreateTrace();

        var comparison = RuntimeTraceAnalyzer.Compare(first, second);

        Assert.IsTrue(comparison.AreSummariesEquivalent);
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

        Assert.IsFalse(comparison.AreSummariesEquivalent);
        Assert.AreEqual(3, comparison.FirstTotalObservations);
        Assert.AreEqual(2, comparison.SecondTotalObservations);
        Assert.AreEqual(1, comparison.EventCountDelta[ParserRuntimeObservationKind.AlternativeCompleted]);
        Assert.AreEqual(1, comparison.EventCountDelta[ParserRuntimeObservationKind.AlternativeSelected]);
        Assert.AreEqual(-1, comparison.EventCountDelta[ParserRuntimeObservationKind.AlternativeFailed]);
    }

    /// <summary>
    /// Ensures summary distributions are exposed as read-only wrappers.
    /// </summary>
    [TestMethod]
    public void Summarize_Distributions_AreReadOnly()
    {
        var summary = RuntimeTraceAnalyzer.Summarize(CreateTrace());

        Assert.ThrowsException<NotSupportedException>(() => ((IDictionary<ParserRuntimeObservationKind, int>)summary.EventDistribution).Add(ParserRuntimeObservationKind.Unknown, 1));
        Assert.ThrowsException<NotSupportedException>(() => ((IDictionary<ParserRuntimeObservationStatus, int>)summary.StatusDistribution).Add(ParserRuntimeObservationStatus.Unknown, 1));
        Assert.ThrowsException<NotSupportedException>(() => ((IDictionary<string, int>)summary.RuleDistribution).Add("other", 1));
        Assert.ThrowsException<NotSupportedException>(() => ((IDictionary<int, int>)summary.AlternativeDistribution).Add(99, 1));
    }

    /// <summary>
    /// Ensures comparison distributions are exposed as read-only wrappers.
    /// </summary>
    [TestMethod]
    public void Compare_EventCountDelta_IsReadOnly()
    {
        var comparison = RuntimeTraceAnalyzer.Compare(CreateTrace(), CreateTrace());

        Assert.ThrowsException<NotSupportedException>(() => ((IDictionary<ParserRuntimeObservationKind, int>)comparison.EventCountDelta).Add(ParserRuntimeObservationKind.Unknown, 1));
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
