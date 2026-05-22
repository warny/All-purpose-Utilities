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
        var probes = DefaultProbes(rule.Content.Alternatives.Count);

        var result = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            0,
            0,
            new DiagnosticBag(),
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 2), Probe("ID")),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: DefaultContinuations(rule, rule.Content.Alternatives.Count),
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []);

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
        var probes = DefaultProbes(rule.Content.Alternatives.Count);

        _ = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            0,
            0,
            diagnostics,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 2), Probe("ID")),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: DefaultContinuations(rule, rule.Content.Alternatives.Count),
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []);

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
        var probes = new[] { Probe("ID"), Probe("ID") };
        var candidates = new ParserLookaheadSharedPrefixDetector().Detect(probes);
        var continuations = new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, candidates);

        var result = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 1), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: continuations,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: candidates);

        var planContinuations = result.Metadata.SharedPrefixPlans[0].Continuations;
        Assert.AreEqual(2, planContinuations.Count);
        Assert.IsTrue(planContinuations.All(static c => c.Key.SequencePosition == 1));
        Assert.AreEqual(1, result.Metadata.SharedPrefixPlans[0].Segment.Boundary.SequencePosition);
    }

    [TestMethod]
    public void Scheduler_Metadata_ProducesReadableSharedPrefixDryRunOutput()
    {
        var scheduler = new AlternativeScheduler();
        var formatter = new ParserSharedPrefixPlanFormatter();
        var alternatives = new[]
        {
            new Alternative(0, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("+"), new RuleRef("expr")]), "a"),
            new Alternative(1, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("-"), new RuleRef("expr")]), "b")
        };
        var rule = new Rule("expr", 0, false, new Alternation(alternatives));
        var probes = new[] { Probe("ID"), Probe("ID") };
        var candidates = new ParserLookaheadSharedPrefixDetector().Detect(probes);
        var continuations = new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, candidates);

        var result = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 1), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: continuations,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: candidates);

        Assert.AreEqual(1, result.Metadata.SharedPrefixPlans.Count);
        Assert.AreEqual("ID", result.Metadata.SharedPrefixPlans[0].SharedTokenName);
        Assert.IsTrue(result.Metadata.SharedPrefixPlans[0].Continuations.All(static c => c.Key.SequencePosition == 1));

        var lines = formatter.FormatPlans(result.Metadata.SharedPrefixPlans);

        Assert.AreEqual(1, lines.Count);
        StringAssert.Contains(lines[0], "shared segment: ID");
        StringAssert.Contains(lines[0], "boundary: position 1");
        StringAssert.Contains(lines[0], "continuations:");
        StringAssert.Contains(lines[0], "alt 0 -> position 1");
        StringAssert.Contains(lines[0], "alt 1 -> position 1");
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

        var sharedProbes = new[] { Probe("ID"), Probe("ID") };
        var sharedCandidates = new ParserLookaheadSharedPrefixDetector().Detect(sharedProbes);
        var sharedContinuations = new ContinuationMetadataPreparation().Prepare(rule, alternatives, sharedProbes, sharedCandidates);

        var withSharedPrefix = scheduler.Run(
            rule, alternatives, 0, 0, null,
            (_, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternatives[index], index, 1), sharedProbes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: sharedContinuations,
            precomputedLookaheadProbes: sharedProbes,
            precomputedSharedPrefixCandidates: sharedCandidates);

        var mixedProbes = new[] { Probe("ID"), Probe("NUMBER") };
        var mixedCandidates = new ParserLookaheadSharedPrefixDetector().Detect(mixedProbes);
        var mixedContinuations = new ContinuationMetadataPreparation().Prepare(rule, alternatives, mixedProbes, mixedCandidates);

        var withoutSharedPrefix = scheduler.Run(
            rule, alternatives, 0, 0, null,
            (_, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternatives[index], index, 1), mixedProbes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: mixedContinuations,
            precomputedLookaheadProbes: mixedProbes,
            precomputedSharedPrefixCandidates: mixedCandidates);

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
        var probes = new[] { Probe("ID"), Probe("ID") };
        var candidates = new ParserLookaheadSharedPrefixDetector().Detect(probes);
        var continuations = new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, candidates);

        var result = scheduler.Run(
            rule, alternatives, 0, 0, null,
            (alternative, index) => index == 0
                ? new ScheduledAlternativeExecutionResult(null, probes[index])
                : new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 1), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: continuations,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: candidates);

        Assert.AreEqual(1, result.Metadata.SharedPrefixPlans.Count);
        Assert.AreEqual("ID", result.Metadata.SharedPrefixPlans[0].SharedTokenName);
        Assert.AreEqual(1, result.CompletedStates.Count);
        Assert.AreEqual(1, result.FailedStates.Count);
    }

    [TestMethod]
    public void Scheduler_MetadataPresence_DoesNotChangeParseAcceptanceSelection()
    {
        var withSharedMetadata = Run(new[] { new[] { "ID" }, new[] { "ID" } });
        var withoutSharedMetadata = Run(new[] { new[] { "ID" }, new[] { "NUMBER" } });

        Assert.IsNotNull(withSharedMetadata.SelectedState);
        Assert.IsNotNull(withoutSharedMetadata.SelectedState);
        Assert.AreEqual(withSharedMetadata.SelectedState.AlternativeIndex, withoutSharedMetadata.SelectedState.AlternativeIndex);
        Assert.AreEqual(withSharedMetadata.SelectedState.CurrentInputPosition, withoutSharedMetadata.SelectedState.CurrentInputPosition);
    }

    [TestMethod]
    public void Scheduler_MetadataPresence_DoesNotSuppressPruningDiagnostics()
    {
        var scheduler = new AlternativeScheduler();
        var diagnostics = new DiagnosticBag();
        var rule = new Rule("r", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), "X"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "X")
        ]));
        var probes = DefaultProbes(rule.Content.Alternatives.Count);

        _ = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            0,
            0,
            diagnostics,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 2), Probe("ID")),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: DefaultContinuations(rule, rule.Content.Alternatives.Count),
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []);

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
    }

    [TestMethod]
    public void Scheduler_Metadata_DoesNotImplySemanticEquivalenceAcrossDistinctAlternatives()
    {
        var scheduler = new AlternativeScheduler();
        var rule = new Rule("r", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), "X"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "Y")
        ]));
        var probes = DefaultProbes(rule.Content.Alternatives.Count);

        var result = scheduler.Run(
            rule,
            rule.Content.Alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 2), Probe("ID")),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: DefaultContinuations(rule, rule.Content.Alternatives.Count),
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []);

        Assert.AreEqual(2, result.CompletedStates.Count);
        Assert.AreEqual(0, result.PrunedStates.Count);
    }

    [TestMethod]
    public void Scheduler_Metadata_DoesNotOwnBranchSelection()
    {
        var scheduler = new AlternativeScheduler();
        var alternatives = new[]
        {
            new Alternative(0, Associativity.Left, new RuleRef("ID"), "a"),
            new Alternative(1, Associativity.Left, new RuleRef("ID"), "b")
        };
        var rule = new Rule("expr", 0, false, new Alternation(alternatives));
        var probes = new[] { Probe("ID"), Probe("ID") };
        var candidates = new ParserLookaheadSharedPrefixDetector().Detect(probes);
        var continuations = new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, candidates);

        var result = scheduler.Run(
            rule, alternatives, 0, 0, null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, index == 0 ? 1 : 3), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: continuations,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: candidates);

        Assert.IsNotNull(result.SelectedState);
        Assert.AreEqual(1, result.SelectedState.AlternativeIndex);
    }

    [TestMethod]
    public void Scheduler_Metadata_CanExistWhenNoBranchIsAuthoritativelyAccepted()
    {
        var scheduler = new AlternativeScheduler();
        var alternatives = new[]
        {
            new Alternative(0, Associativity.Left, new RuleRef("ID"), "a"),
            new Alternative(1, Associativity.Left, new RuleRef("ID"), "b")
        };
        var rule = new Rule("expr", 0, false, new Alternation(alternatives));
        var probes = new[] { Probe("ID"), Probe("ID") };
        var candidates = new ParserLookaheadSharedPrefixDetector().Detect(probes);
        var continuations = new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, candidates);

        var result = scheduler.Run(
            rule, alternatives, 0, 0, null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(null, probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: continuations,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: candidates);

        Assert.IsNull(result.SelectedState);
        Assert.AreEqual(0, result.CompletedStates.Count);
        Assert.AreEqual(1, result.Metadata.SharedPrefixPlans.Count);
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
        var probes = expected.Select(static e => new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, null, null, e)).ToArray();
        var candidates = new ParserLookaheadSharedPrefixDetector().Detect(probes);
        var continuations = new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, candidates);
        return scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateState(rule, alternative, index, 1), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: continuations,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: candidates);
    }

    private static IReadOnlyList<ParserLookaheadProbeResult> DefaultProbes(int count)
    {
        return Enumerable.Range(0, count)
            .Select(static _ => new ParserLookaheadProbeResult(ParserLookaheadProbeKind.Unknown, null, null))
            .ToArray();
    }

    private static IReadOnlyList<ParserContinuationDescriptor> DefaultContinuations(Rule rule, int count)
    {
        return Enumerable.Range(0, count)
            .Select(index => new ParserContinuationDescriptor(
                new ParserContinuationKey(rule.Name, index, 0),
                ParserContinuationCategory.Sequential,
                null,
                false))
            .ToArray();
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
