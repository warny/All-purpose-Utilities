using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ArchitectureInvariantTests
{
    [TestMethod]
    public void Scheduler_DoesNotProduceContinuationMetadata()
    {
        var scheduler = new AlternativeScheduler();
        var rule = CreateRule();
        var alternatives = rule.Content.Alternatives;
        var probes = CreateProbes(alternatives.Count);
        var prepared = new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, []);
        var before = prepared.Select(static descriptor => descriptor with { }).ToArray();

        _ = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateCompletedState(rule, alternative, index), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: prepared,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []);

        AssertContinuationSetsEqual(before, prepared.ToArray());
    }

    [TestMethod]
    public void Scheduler_DoesNotMutatePreparedMetadata()
    {
        var scheduler = new AlternativeScheduler();
        var rule = CreateRule();
        var alternatives = rule.Content.Alternatives;
        var probes = CreateProbes(alternatives.Count);
        var prepared = new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, []);
        var before = prepared.ToArray();

        _ = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateCompletedState(rule, alternative, index), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: prepared,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []);

        AssertContinuationSetsEqual(before, prepared.ToArray());
    }

    [TestMethod]
    public void Scheduler_DoesNotOwnPreparation()
    {
        var scheduler = new AlternativeScheduler();
        var rule = CreateRule();
        var alternatives = rule.Content.Alternatives;
        var probes = CreateProbes(alternatives.Count);

        _ = Assert.ThrowsException<ArgumentException>(() => scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateCompletedState(rule, alternative, index), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: [],
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []));
    }


    [TestMethod]
    public void Scheduler_DoesNotRequireGrammarTraversal()
    {
        var scheduler = new AlternativeScheduler();
        var alternatives = new[]
        {
            new Alternative(0, Associativity.Left, new ThrowingRuleContent("alt0")),
            new Alternative(1, Associativity.Left, new ThrowingRuleContent("alt1"))
        };
        var rule = new Rule("expr", 0, false, new Alternation(alternatives));
        var probes = CreateProbes(alternatives.Length);
        var continuations = new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, []);

        var result = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateCompletedState(rule, alternative, index), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: continuations,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []);

        Assert.IsNotNull(result.SelectedState);
    }

    [TestMethod]
    public void Preparation_IsDeterministic()
    {
        var rule = new Rule("expr", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("+"), new RuleRef("expr")])) ,
            new Alternative(1, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("-"), new RuleRef("expr")]))
        ]));
        var alternatives = rule.Content.Alternatives;
        var probes = CreateProbes(alternatives.Count);
        var candidates = new ParserLookaheadSharedPrefixDetector().Detect(probes);
        var preparation = new ContinuationMetadataPreparation();

        var first = preparation.Prepare(rule, alternatives, probes, candidates).ToArray();
        var second = preparation.Prepare(rule, alternatives, probes, candidates).ToArray();
        var third = preparation.Prepare(rule, alternatives, probes, candidates).ToArray();

        AssertContinuationSetsEqual(first, second);
        AssertContinuationSetsEqual(second, third);
    }

    [TestMethod]
    public void Preparation_DoesNotModifyGrammar()
    {
        var rule = new Rule("expr", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("+")])) ,
            new Alternative(1, Associativity.Left, new Sequence([new RuleRef("ID"), new LiteralMatch("-")]))
        ]));
        var alternatives = rule.Content.Alternatives;
        var grammarSnapshot = SnapshotRule(rule);
        var probes = CreateProbes(alternatives.Count);
        var candidates = new ParserLookaheadSharedPrefixDetector().Detect(probes);
        var preparation = new ContinuationMetadataPreparation();

        _ = preparation.Prepare(rule, alternatives, probes, candidates);
        _ = preparation.Prepare(rule, alternatives, probes, candidates);
        _ = preparation.Prepare(rule, alternatives, probes, candidates);

        Assert.AreEqual(grammarSnapshot, SnapshotRule(rule));
    }

    [TestMethod]
    public void SharedPrefixMetadata_DoesNotChangeExecution()
    {
        var scheduler = new AlternativeScheduler();
        var rule = CreateRule();
        var alternatives = rule.Content.Alternatives;
        var probes = CreateProbes(alternatives.Count);

        var withoutShared = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            new DiagnosticBag(),
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateCompletedState(rule, alternative, index), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, []),
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: []);

        var shared = new[] { new ParserLookaheadSharedPrefixCandidate("ID", [0, 1]) };
        var withShared = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            new DiagnosticBag(),
            (alternative, index) => new ScheduledAlternativeExecutionResult(CreateCompletedState(rule, alternative, index), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: new ContinuationMetadataPreparation().Prepare(rule, alternatives, probes, shared),
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: shared);

        Assert.IsNotNull(withoutShared.SelectedState);
        Assert.IsNotNull(withShared.SelectedState);
        Assert.AreEqual(withoutShared.SelectedState.AlternativeIndex, withShared.SelectedState.AlternativeIndex);
        Assert.AreEqual(withoutShared.SelectedState.CurrentInputPosition, withShared.SelectedState.CurrentInputPosition);
    }

    [TestMethod]
    public void Execution_RemainsDecisionOwner_WithContradictoryMetadata()
    {
        var scheduler = new AlternativeScheduler();
        var rule = new Rule("expr", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new RuleRef("ID")),
            new Alternative(1, Associativity.Left, new RuleRef("NUMBER"))
        ]));
        var alternatives = rule.Content.Alternatives;
        var probes = CreateProbes(alternatives.Count);
        var contradictoryShared = new[] { new ParserLookaheadSharedPrefixCandidate("NUMBER", [0, 1]) };
        var contradictoryContinuations = new[]
        {
            new ParserContinuationDescriptor(new ParserContinuationKey(rule.Name, 0, 99), ParserContinuationCategory.SharedPrefixCandidate, ["NUMBER"], true),
            new ParserContinuationDescriptor(new ParserContinuationKey(rule.Name, 1, 99), ParserContinuationCategory.SharedPrefixCandidate, ["NUMBER"], true)
        };

        var result = scheduler.Run(
            rule,
            alternatives,
            0,
            0,
            null,
            (alternative, index) => index == 0
                ? new ScheduledAlternativeExecutionResult(CreateCompletedState(rule, alternative, index, 3), probes[index])
                : new ScheduledAlternativeExecutionResult(CreateCompletedState(rule, alternative, index, 1), probes[index]),
            precomputedDescriptors: null,
            precomputedContinuationMetadata: contradictoryContinuations,
            precomputedLookaheadProbes: probes,
            precomputedSharedPrefixCandidates: contradictoryShared);

        Assert.IsNotNull(result.SelectedState);
        Assert.AreEqual(0, result.SelectedState.AlternativeIndex);
        Assert.AreEqual(3, result.SelectedState.CurrentInputPosition);
    }

    [TestMethod]
    public void Observation_DoesNotChangeResult()
    {
        var definition = ExpGrammar.Build();
        var tokens = new[]
        {
            new Token(new SourceSpan(0, 1), "INT", "DEFAULT_MODE", "DEFAULT_CHANNEL", "1")
        };

        var defaultParser = new ParserEngine(definition);
        var observedParser = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { RuntimeObserver = new NoopObserver() });

        var defaultResult = defaultParser.Parse(tokens);
        var observedResult = observedParser.Parse(tokens);

        Assert.AreEqual(defaultResult.ToString(), observedResult.ToString());
        Assert.AreEqual(defaultResult is ErrorNode, observedResult is ErrorNode);
    }

    private static string SnapshotRule(Rule rule)
    {
        return string.Join("|", rule.Content.Alternatives.Select(static alternative => $"{alternative.Priority}:{alternative.Content}"));
    }

    private static Rule CreateRule()
    {
        return new Rule("expr", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new RuleRef("ID")),
            new Alternative(1, Associativity.Left, new RuleRef("NUMBER"))
        ]));
    }

    private static ParserLookaheadProbeResult[] CreateProbes(int count)
    {
        return Enumerable.Range(0, count)
            .Select(static _ => new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "ID", "id", ["ID"]))
            .ToArray();
    }

    private static ActiveParseState CreateCompletedState(Rule rule, Alternative alternative, int index, int endPosition = 1)
    {
        return new ActiveParseState
        {
            Rule = rule,
            Alternative = alternative,
            OriginInputPosition = 0,
            CurrentInputPosition = endPosition,
            AlternativeIndex = index,
            Cursor = new RuleContentCursor { Index = 0, Kind = ScheduledAlternativeCursorKinds.AlternativeRoot },
            PartialNode = new ParserNode(new SourceSpan(0, endPosition), "DEFAULT_MODE", rule, []),
            EndPosition = endPosition,
            Status = ActiveParseStateStatus.Completed,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };
    }

    private static void AssertContinuationSetsEqual(IReadOnlyList<ParserContinuationDescriptor> expected, IReadOnlyList<ParserContinuationDescriptor> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].Key, actual[index].Key);
            Assert.AreEqual(expected[index].Category, actual[index].Category);
            CollectionAssert.AreEqual(expected[index].ExpectedTokenNames.ToArray(), actual[index].ExpectedTokenNames.ToArray());
            Assert.AreEqual(expected[index].IsSharedPrefixCandidate, actual[index].IsSharedPrefixCandidate);
        }
    }


    private sealed record ThrowingRuleContent(string Name) : RuleContent
    {
        public override string ToString()
        {
            throw new InvalidOperationException($"Scheduler attempted to traverse grammar content: {Name}");
        }
    }

    private sealed class NoopObserver : IParserRuntimeObserver
    {
        public void OnAlternativeStarted(AlternativeRuntimeObservation observation) { }
        public void OnAlternativeCompleted(AlternativeRuntimeObservation observation) { }
        public void OnAlternativeFailed(AlternativeRuntimeObservation observation) { }
        public void OnAlternativePruned(AlternativeRuntimeObservation observation) { }
        public void OnAlternativeSelected(AlternativeRuntimeObservation observation) { }
    }
}
