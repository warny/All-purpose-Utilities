using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Expressions;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>Verifies immutable snapshots used by parser preparation and analysis results.</summary>
[TestClass]
public sealed class ParserInternalImmutabilityTests
{
    /// <summary>Verifies scheduling descriptors defensively capture ordered source collections.</summary>
    [TestMethod]
    public void SchedulingSnapshots_DoNotAliasMutableSequences()
    {
        string[] tokens = ["first"];
        var descriptor = new AlternativeStructuralDescriptor(0, tokens);
        tokens[0] = "second";

        Assert.AreEqual("first", descriptor.StructuralTokens[0]);
        Assert.IsFalse(descriptor.StructuralTokens is string[]);

        int[] indexes = [0];
        var candidate = new ParserLookaheadSharedPrefixCandidate("first", indexes);
        indexes[0] = 1;

        Assert.AreEqual(0, candidate.AlternativeIndexes[0]);
        Assert.IsFalse(candidate.AlternativeIndexes is int[]);
    }

    /// <summary>Verifies runtime trace dictionaries are immutable construction-time snapshots.</summary>
    [TestMethod]
    public void RuntimeTraceResults_DoNotAliasMutableDictionaries()
    {
        var rules = new Dictionary<string, int>(StringComparer.Ordinal) { ["rule"] = 1 };
        var summary = new RuntimeTraceSummary(1, new Dictionary<ParserRuntimeObservationKind, int>(), new Dictionary<ParserRuntimeObservationStatus, int>(), rules, new Dictionary<int, int>());
        rules["rule"] = 42;

        Assert.AreEqual(1, summary.RuleDistribution["rule"]);
        Assert.IsFalse(summary.RuleDistribution is Dictionary<string, int>);

        var deltas = new Dictionary<ParserRuntimeObservationKind, int> { [ParserRuntimeObservationKind.AlternativeStarted] = 1 };
        var comparison = new RuntimeTraceComparison(false, false, false, 1, 0, deltas);
        deltas[ParserRuntimeObservationKind.AlternativeStarted] = 2;
        Assert.AreEqual(1, comparison.EventCountDelta[ParserRuntimeObservationKind.AlternativeStarted]);
    }

    /// <summary>Verifies record cloning normalizes mutable dictionaries assigned to trace results.</summary>
    [TestMethod]
    public void RuntimeTraceResults_WithExpressionsCaptureMutableDictionaries()
    {
        var summary = new RuntimeTraceSummary(0, new Dictionary<ParserRuntimeObservationKind, int>(), new Dictionary<ParserRuntimeObservationStatus, int>(), new Dictionary<string, int>(), new Dictionary<int, int>());
        var events = new Dictionary<ParserRuntimeObservationKind, int>
        {
            [ParserRuntimeObservationKind.AlternativeStarted] = 1
        };
        var summaryCopy = summary with { EventDistribution = events };

        var comparison = new RuntimeTraceComparison(false, false, false, 0, 0, new Dictionary<ParserRuntimeObservationKind, int>());
        var comparisonCopy = comparison with { EventCountDelta = events };
        events.Clear();

        Assert.AreEqual(1, summaryCopy.EventDistribution[ParserRuntimeObservationKind.AlternativeStarted]);
        Assert.AreEqual(1, comparisonCopy.EventCountDelta[ParserRuntimeObservationKind.AlternativeStarted]);
        Assert.IsFalse(summaryCopy.EventDistribution is Dictionary<ParserRuntimeObservationKind, int>);
        Assert.IsFalse(comparisonCopy.EventCountDelta is Dictionary<ParserRuntimeObservationKind, int>);
    }

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

    /// <summary>Verifies params diagnostic arguments are captured rather than retained.</summary>
    [TestMethod]
    public void ParserActionOutcome_CapturesParamsArguments()
    {
        object?[] arguments = ["first"];
        var outcome = ParserActionExecutionOutcome.NotExecuted(Utils.Parser.Diagnostics.ParserDiagnostics.EmbeddedCodeExecutionDisabled, null, arguments);
        arguments[0] = "second";

        Assert.AreEqual("first", outcome.DiagnosticArguments[0]);
        Assert.IsFalse(outcome.DiagnosticArguments is object?[]);
    }

    /// <summary>Verifies expression registry result sequences cannot expose or retain mutable lists.</summary>
    [TestMethod]
    public void ExpressionRegistryResult_CapturesListsAndDoesNotExposeArrays()
    {
        var entries = new List<PreparedExpressionEmbeddedCodeRegistryBuildEntry>();
        var result = new PreparedExpressionEmbeddedCodeRegistryBuildResult(new PreparedExpressionEmbeddedCodeRegistry(), entries, entries, entries, entries, entries, entries);
        entries.Add(null!);

        Assert.AreEqual(0, result.AllEntries.Count);
        Assert.IsFalse(result.AllEntries is PreparedExpressionEmbeddedCodeRegistryBuildEntry[]);
        Assert.IsFalse(result.AllEntries is List<PreparedExpressionEmbeddedCodeRegistryBuildEntry>);
    }
}
