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
