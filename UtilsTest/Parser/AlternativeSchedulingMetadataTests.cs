using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class AlternativeSchedulingMetadataTests
{
    [TestMethod]
    public void Scheduler_ProducesEmptyMetadata_WhenNoSharedPrefixes()
    {
        var result = Run(new[] { new[] { "ID" }, new[] { "NUMBER" } });
        Assert.AreEqual(0, result.Metadata.SharedPrefixPlans.Count);
    }

    [TestMethod]
    public void Scheduler_ProducesSharedPrefixPlans_ForSharedFirstTokens()
    {
        var result = Run(new[] { new[] { "ID" }, new[] { "ID" } });
        Assert.AreEqual(1, result.Metadata.SharedPrefixPlans.Count);
        var plan = result.Metadata.SharedPrefixPlans[0];
        Assert.AreEqual("ID", plan.SharedTokenName);
        CollectionAssert.AreEqual(new[] { 0, 1 }, plan.AlternativeIndexes.ToArray());
    }

    [TestMethod]
    public void Scheduler_Metadata_DoesNotChangeAlternativeOrdering()
    {
        var result = Run(new[] { new[] { "ID" }, new[] { "ID" } });
        CollectionAssert.AreEqual(new[] { 0, 1 }, result.CompletedStates.Select(static s => s.AlternativeIndex).ToArray());
    }

    [TestMethod]
    public void Scheduler_Metadata_DoesNotChangePruning()
    {
        var scheduler = new AlternativeScheduler();
        var rule = new Rule("r", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), "X"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "X")
        ]));

        var result = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            0,
            0,
            new DiagnosticBag(),
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 2), Probe("ID")));

        Assert.AreEqual(1, result.CompletedStates.Count);
        Assert.AreEqual(1, result.PrunedStates.Count);
    }

    [TestMethod]
    public void Scheduler_Metadata_DoesNotChangeDiagnostics()
    {
        var scheduler = new AlternativeScheduler();
        var diagnostics = new DiagnosticBag();
        var rule = new Rule("r", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), "X"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "X")
        ]));

        _ = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            0,
            0,
            diagnostics,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 2), Probe("ID")));

        Assert.AreEqual(1, diagnostics.Count(static d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
    }

    [TestMethod]
    public void Scheduler_Metadata_UsesExpectedTokenNames()
    {
        var result = Run(new[] { new[] { "ID" }, new[] { "NUMBER", "ID" } });
        Assert.AreEqual("ID", result.Metadata.SharedPrefixPlans[0].SharedTokenName);
    }

    [TestMethod]
    public void Scheduler_Metadata_CreatesContinuationDescriptors()
    {
        var scheduler = new AlternativeScheduler();
        var alternatives = new[]
        {
            new Alternative(0, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("+"), new RuleRef("expr")]), "a"),
            new Alternative(1, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("-"), new RuleRef("expr")]), "b")
        };
        var rule = new Rule("expr", 0, false, new Alternation(alternatives));
        var result = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 1), Probe("ID")));
        var continuations = result.Metadata.SharedPrefixPlans[0].Continuations;
        Assert.AreEqual(2, continuations.Count);
        Assert.IsTrue(continuations.All(static c => c.Key.SequencePosition == 1));
        Assert.AreEqual(1, result.Metadata.SharedPrefixPlans[0].Segment.Boundary.SequencePosition);
    }

    [TestMethod]
    public void Scheduler_Metadata_PreservesStableAlternativeIndexes()
    {
        var result = Run(new[] { new[] { "ID" }, new[] { "ID" } });
        var indexes = result.Metadata.SharedPrefixPlans[0].Continuations.Select(static c => c.Key.AlternativeIndex).ToArray();
        CollectionAssert.AreEqual(new[] { 0, 1 }, indexes);
    }



    [TestMethod]
    public void Scheduler_Metadata_DoesNotChangeSelection_WhenProbeMetadataDiffers()
    {
        var scheduler = new AlternativeScheduler();
        var alternatives = new[]
        {
            new Alternative(0, Associativity.Left, new RuleRef("ID"), "a"),
            new Alternative(1, Associativity.Left, new RuleRef("ID"), "b")
        };
        var rule = new Rule("expr", 0, false, new Alternation(alternatives));

        ScheduledAlternativeExecutionResult ParseWithProbe(IReadOnlyList<string> expected, int index)
        {
            return new ScheduledAlternativeExecutionResult(CreateState(rule, alternatives[index], index, 1), Probe(expected));
        }

        var withSharedPrefix = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (_, index) => ParseWithProbe(["ID"], index));

        var withoutSharedPrefix = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (_, index) => index == 0 ? ParseWithProbe(["ID"], index) : ParseWithProbe(["NUMBER"], index));

        Assert.IsNotNull(withSharedPrefix.SelectedState);
        Assert.IsNotNull(withoutSharedPrefix.SelectedState);
        Assert.AreEqual(withSharedPrefix.SelectedState.AlternativeIndex, withoutSharedPrefix.SelectedState.AlternativeIndex);
        Assert.AreEqual(withSharedPrefix.SelectedState.CurrentInputPosition, withoutSharedPrefix.SelectedState.CurrentInputPosition);
    }

    [TestMethod]
    public void Scheduler_Metadata_IncludesFailedAlternativesLookahead()
    {
        var scheduler = new AlternativeScheduler();
        var alternatives = new[]
        {
            new Alternative(0, Associativity.Left, new RuleRef("ID"), "a"),
            new Alternative(1, Associativity.Left, new RuleRef("ID"), "b")
        };
        var rule = new Rule("expr", 0, false, new Alternation(alternatives));

        var result = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => index == 0
                ? new ScheduledAlternativeExecutionResult(null, Probe("ID"))
                : new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 1), Probe("ID")));

        Assert.AreEqual(1, result.Metadata.SharedPrefixPlans.Count);
        Assert.AreEqual("ID", result.Metadata.SharedPrefixPlans[0].SharedTokenName);
        Assert.AreEqual(1, result.CompletedStates.Count);
        Assert.AreEqual(1, result.FailedStates.Count);
    }
    private static AlternativeSchedulingResult Run(IReadOnlyList<IReadOnlyList<string>> expected)
    {
        var scheduler = new AlternativeScheduler();
        var alternatives = new[]
        {
            new Alternative(0, Associativity.Left, new RuleRef("ID"), "a"),
            new Alternative(1, Associativity.Left, new RuleRef("ID"), "b")
        };
        var rule = new Rule("expr", 0, false, new Alternation(alternatives));
        return scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 1), Probe(expected[index])));
    }

    private static ParserLookaheadProbeResult Probe(IReadOnlyList<string> expected) =>
        new(ParserLookaheadProbeKind.RequiresParse, null, null, expected);

    private static ParserLookaheadProbeResult Probe(string expected) =>
        new(ParserLookaheadProbeKind.RequiresParse, null, null, [expected]);

    private static ActiveParseState CreateState(Rule rule, Alternative alternative, int index, int currentInputPosition)
    {
        return new ActiveParseState
        {
            Rule = rule,
            Alternative = alternative,
            OriginInputPosition = 0,
            CurrentInputPosition = currentInputPosition,
            AlternativeIndex = index,
            Cursor = new RuleContentCursor { Index = 0, Kind = "alternative-root" },
            PartialNode = new ParserNode(new SourceSpan(0, currentInputPosition), "DEFAULT_MODE", rule, []),
            EndPosition = currentInputPosition,
            Status = ActiveParseStateStatus.Completed,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };
    }
}
